using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DnstapLogger.FrameStreams
{
    internal static class FrameStreamWriterHelpers
    {
        // ---------------------------------------------------
        // EXACT READ HELPER
        // ---------------------------------------------------
        public static async Task ReadExactlyAsync(this Stream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            int offset = 0;
            int remaining = buffer.Length;

            while (remaining > 0)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(offset, remaining), cancellationToken);
                if (read == 0)
                    throw new EndOfStreamException("Stream closed while reading Frame Streams control frame.");

                offset += read;
                remaining -= read;
            }
        }
    }
}