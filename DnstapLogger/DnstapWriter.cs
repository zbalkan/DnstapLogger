using DnstapLogger.FrameStreams;
using ProtoBuf;
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DnstapLogger
{
    public sealed class DnstapWriter : IDisposable
    {
        private readonly FrameStreamWriter _writer;
        private readonly Stream _stream;
        private readonly bool _ownsStream;
        private readonly bool _noHandshake;
        private bool _started;
        private bool _stopped;
        private bool _disposed;

        private const string DnstapContentType = "protobuf:dnstap.Dnstap";

        private DnstapWriter(Stream stream, bool ownsStream, bool noHandshake = true)
        {
            _stream = stream;
            _writer = new FrameStreamWriter(stream, leaveOpen: !ownsStream);
            _ownsStream = ownsStream;
            _noHandshake = noHandshake;
        }

        public static DnstapWriter CreateFileWriter(string path, bool noHandshake = true)
        {
            ArgumentNullException.ThrowIfNull(path);
            var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            return new DnstapWriter(fs, ownsStream: true);
        }

        public static async Task<DnstapWriter> ConnectTcpAsync(string host, int port, bool noHandshake = true, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(host);
            var client = new TcpClient();
            await client.ConnectAsync(host, port, cancellationToken);

            var stream = client.GetStream();
            var writer = new DnstapWriter(stream, ownsStream: true);

            // AUTO-START IMMEDIATELY
            await writer.StartAsync(cancellationToken);

            return writer;
        }

        private async Task PerformHandshakeAsync(CancellationToken cancellationToken)
        {
            // 1️⃣ READ READY frame from server
            var ready = await _writer.ReadControlFrameAsync(cancellationToken).ConfigureAwait(false);

            if (ready.Type != FrameStreamConstants.CONTROL_READY)
                throw new InvalidOperationException($"Expected READY control frame, got {ready.Type}");

            // Make sure the server supports DNSTAP
            bool supported = false;
            var dnstapBytes = Encoding.ASCII.GetBytes(DnstapContentType);

            foreach (var ct in ready.ContentTypes)
            {
                if (Encoding.ASCII.GetString(ct) == DnstapContentType)
                {
                    supported = true;
                    break;
                }
            }

            if (!supported)
            {
                throw new InvalidOperationException(
                    $"Server does not advertise DNSTAP content-type '{DnstapContentType}'");
            }

            // 2️⃣ SEND ACCEPT
            await _writer.WriteControlFrameAsync(
                FrameStreamConstants.CONTROL_ACCEPT,
                new[] { dnstapBytes },
                cancellationToken
            ).ConfigureAwait(false);

            // 3️⃣ SEND START
            await _writer.WriteControlFrameAsync(
                FrameStreamConstants.CONTROL_START,
                null,
                cancellationToken
            ).ConfigureAwait(false);
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(DnstapWriter));
            if (_started)
                return;
            if (!_noHandshake)
            {
                bool isNetwork = _stream is NetworkStream;

                if (isNetwork)
                {
                    // Perform full handshake (WAIT READY → ACCEPT → START)
                    await PerformHandshakeAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            {
                // File writers send only START (unidirectional mode)
                await _writer.WriteControlFrameAsync(
                    FrameStreamConstants.CONTROL_START,
                    null,
                    cancellationToken
                ).ConfigureAwait(false);
            }

            _started = true;
        }

        public async Task WriteMessageAsync(DnstapMessage dnstapMessage, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dnstapMessage);
            ObjectDisposedException.ThrowIf(_disposed, nameof(DnstapWriter));

            if (!_started)
                await StartAsync(cancellationToken).ConfigureAwait(false);

            using var ms = new MemoryStream();
            Serializer.Serialize(ms, dnstapMessage.Payload);
            var payload = ms.ToArray();

            await _writer.WriteDataFrameAsync(payload, cancellationToken).ConfigureAwait(false);
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(DnstapWriter));
            if (_stopped)
                return;

            // Always write STOP, server will reply FINISH if applicable
            await _writer.WriteControlFrameAsync(
                FrameStreamConstants.CONTROL_STOP,
                null,
                cancellationToken
            ).ConfigureAwait(false);

            _stopped = true;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                if (!_stopped)
                    StopAsync().GetAwaiter().GetResult();
            }
            catch { }

            _writer.Dispose();
            if (_ownsStream)
                _stream.Dispose();

            _disposed = true;
        }
    }
}