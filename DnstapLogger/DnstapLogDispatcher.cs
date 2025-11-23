using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DnstapLogger
{
    /// <summary>
    /// Asynchronous dispatcher that decouples synchronous logging calls
    /// from the underlying DNSTAP writer. Loggers enqueue messages and a
    /// single background task performs the actual I/O.
    /// </summary>
    internal sealed class DnstapLogDispatcher : IDisposable
    {
        private readonly DnstapWriter _writer;
        private readonly Channel<DnstapMessage> _channel;
        private readonly CancellationTokenSource _cts = new();
        private readonly object _startLock = new();

        private Task? _processingTask;
        private bool _disposed;

        public DnstapLogDispatcher(DnstapWriter writer)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));

            // Unbounded channel: avoids blocking the logging call path.
            // We use a single reader and multiple writers (loggers).
            var options = new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            };

            _channel = Channel.CreateUnbounded<DnstapMessage>(options);
        }

        /// <summary>
        /// Enqueue a DNSTAP message for asynchronous writing. This method
        /// is non-blocking and safe to call from ILogger.Log.
        /// </summary>
        public void Enqueue(DnstapMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);
            ObjectDisposedException.ThrowIf(_disposed, nameof(DnstapLogDispatcher));

            EnsureStarted();

            // Unbounded channel: TryWrite should always succeed; if it
            // fails for any reason, we drop the message rather than
            // blocking the caller.
            _channel.Writer.TryWrite(message);
        }

        private void EnsureStarted()
        {
            if (_processingTask != null)
                return;

            lock (_startLock)
            {
                if (_processingTask != null)
                    return;

                _processingTask = Task.Run(ProcessLoopAsync, _cts.Token);
            }
        }

        private async Task ProcessLoopAsync()
        {
            try
            {
                // Ensure the underlying writer is started once before
                // processing messages.
                await _writer.StartAsync(_cts.Token).ConfigureAwait(false);

                while (await _channel.Reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false))
                {
                    while (_channel.Reader.TryRead(out var msg))
                    {
                        try
                        {
                            await _writer.WriteMessageAsync(msg, _cts.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            // Shutdown requested: exit the loop.
                            return;
                        }
                        catch
                        {
                            // Per-message write error; swallow here to avoid
                            // crashing the background loop. Consider adding
                            // diagnostics/logging if desired.
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown path.
            }
            catch
            {
                // Consider logging this in the future; we deliberately do
                // not rethrow to avoid bringing down the process.
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Signal shutdown and complete the channel.
            _cts.Cancel();
            _channel.Writer.TryComplete();

            try
            {
                // Wait for the background worker to finish flushing any
                // remaining messages.
                _processingTask?.GetAwaiter().GetResult();
            }
            catch
            {
                // Ignore background errors during shutdown. A future PR
                // could expose "strict" disposal behavior if desired.
            }

            _writer.Dispose();
            _cts.Dispose();
        }
    }
}
