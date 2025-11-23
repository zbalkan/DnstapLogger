using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DnstapLogger.FrameStreams
{
    /// <summary>
    /// Implements the low-level Frame Streams wire protocol for writing frames.
    /// Adds optional control-frame reading support for DNSTAP TCP handshake.
    /// </summary>
    public class FrameStreamWriter : IDisposable
    {
        private readonly Stream _stream;
        private readonly bool _leaveOpen;
        private bool _disposed;

        public FrameStreamWriter(Stream stream, bool leaveOpen = false)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _leaveOpen = leaveOpen;
        }

        // ---------------------------------------------------
        // DATA FRAME WRITER
        // ---------------------------------------------------
        public async Task WriteDataFrameAsync(byte[] payload, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(payload);
            ObjectDisposedException.ThrowIf(_disposed, this);

            uint len = (uint)payload.Length;

            Span<byte> header =
            [
                (byte)((len >> 24) & 0xFF),
                (byte)((len >> 16) & 0xFF),
                (byte)((len >> 8) & 0xFF),
                (byte)(len & 0xFF),
            ];

            await _stream.WriteAsync(header.ToArray(), 0, 4, cancellationToken)
                .ConfigureAwait(false);

            if (len > 0)
            {
                await _stream.WriteAsync(payload, 0, payload.Length, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        // ---------------------------------------------------
        // CONTROL FRAME WRITER
        // ---------------------------------------------------
        public async Task WriteControlFrameAsync(
            uint controlType,
            IEnumerable<byte[]>? contentTypes = null,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            using var ms = new MemoryStream();
            // Type
            ms.WriteByte((byte)((controlType >> 24) & 0xFF));
            ms.WriteByte((byte)((controlType >> 16) & 0xFF));
            ms.WriteByte((byte)((controlType >> 8) & 0xFF));
            ms.WriteByte((byte)(controlType & 0xFF));

            // Optional CONTENT_TYPE fields
            if (contentTypes != null)
            {
                foreach (var ctBytes in contentTypes)
                {
                    if (ctBytes == null)
                        continue;

                    const uint FIELD_TYPE = 1;

                    // Field type
                    ms.WriteByte((byte)((FIELD_TYPE >> 24) & 0xFF));
                    ms.WriteByte((byte)((FIELD_TYPE >> 16) & 0xFF));
                    ms.WriteByte((byte)((FIELD_TYPE >> 8) & 0xFF));
                    ms.WriteByte((byte)(FIELD_TYPE & 0xFF));

                    // Field length
                    uint fLen = (uint)ctBytes.Length;
                    ms.WriteByte((byte)((fLen >> 24) & 0xFF));
                    ms.WriteByte((byte)((fLen >> 16) & 0xFF));
                    ms.WriteByte((byte)((fLen >> 8) & 0xFF));
                    ms.WriteByte((byte)(fLen & 0xFF));

                    // Field data
                    ms.Write(ctBytes, 0, ctBytes.Length);
                }
            }

            byte[] controlPayload = ms.ToArray();
            uint payloadLength = (uint)controlPayload.Length;

            // ESCAPE (zero length)
            Span<byte> header = stackalloc byte[8];
            // escape = 0x00000000 (already zero)
            header[4] = (byte)((payloadLength >> 24) & 0xFF);
            header[5] = (byte)((payloadLength >> 16) & 0xFF);
            header[6] = (byte)((payloadLength >> 8) & 0xFF);
            header[7] = (byte)(payloadLength & 0xFF);

            await _stream.WriteAsync(header.ToArray(), 0, 8, cancellationToken)
                .ConfigureAwait(false);

            if (payloadLength > 0)
            {
                await _stream.WriteAsync(controlPayload, 0, controlPayload.Length, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        // ---------------------------------------------------
        // CONTROL FRAME READER (Blocking)
        // ---------------------------------------------------
        public sealed class FrameStreamControlFrame
        {
            public uint Type { get; set; }
            public List<byte[]> ContentTypes { get; set; } = new();
        }

        public async Task<FrameStreamControlFrame> ReadControlFrameAsync(CancellationToken cancellationToken)
        {
            byte[] escapeBuf = new byte[4];
            await _stream.ReadExactlyAsync(escapeBuf, cancellationToken).ConfigureAwait(false);

            uint escape = ReadUInt32BE(escapeBuf, 0);
            if (escape != 0)
                throw new InvalidDataException($"Expected control escape (0), got {escape}");

            byte[] lenBuf = new byte[4];
            await _stream.ReadExactlyAsync(lenBuf, cancellationToken).ConfigureAwait(false);

            uint frameLength = ReadUInt32BE(lenBuf, 0);
            if (frameLength < 4)
                throw new InvalidDataException($"Invalid control frame length {frameLength}");

            byte[] typeBuf = new byte[4];
            await _stream.ReadExactlyAsync(typeBuf, cancellationToken).ConfigureAwait(false);

            uint ctrlType = ReadUInt32BE(typeBuf, 0);

            var ctList = new List<byte[]>();
            int remaining = (int)frameLength - 4;

            while (remaining > 0)
            {
                byte[] tBuf = new byte[4];
                await _stream.ReadExactlyAsync(tBuf, cancellationToken).ConfigureAwait(false);
                uint fieldId = ReadUInt32BE(tBuf, 0);

                byte[] flBuf = new byte[4];
                await _stream.ReadExactlyAsync(flBuf, cancellationToken).ConfigureAwait(false);
                uint fieldLength = ReadUInt32BE(flBuf, 0);

                byte[] data = new byte[fieldLength];
                await _stream.ReadExactlyAsync(data, cancellationToken).ConfigureAwait(false);

                if (fieldId == FrameStreamConstants.CONTROL_FIELD_CONTENT_TYPE)
                    ctList.Add(data);

                remaining -= (int)(8 + fieldLength);
            }

            return new FrameStreamControlFrame
            {
                Type = ctrlType,
                ContentTypes = ctList
            };
        }

        // ---------------------------------------------------
        // NON-BLOCKING CONTROL FRAME TRY-READ (NEW)
        // ---------------------------------------------------
        public async Task<FrameStreamControlFrame?> TryReadControlFrameAsync(CancellationToken cancellationToken)
        {
            if (_stream is not NetworkStream ns)
                return null;

            // Nothing available → no READY sent → unidirectional collector
            if (!ns.DataAvailable)
                return null;

            try
            {
                return await ReadControlFrameAsync(cancellationToken);
            }
            catch
            {
                // Not a valid control frame → treat as unidirectional
                return null;
            }
        }

        // ---------------------------------------------------
        private static uint ReadUInt32BE(byte[] buf, int offset)
        {
            return (uint)(
                (buf[offset] << 24) |
                (buf[offset + 1] << 16) |
                (buf[offset + 2] << 8) |
                 buf[offset + 3]);
        }

        // ---------------------------------------------------
        // Dispose
        // ---------------------------------------------------
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing && !_leaveOpen)
                    _stream.Dispose();

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}