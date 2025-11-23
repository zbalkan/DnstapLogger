using Microsoft.Extensions.Logging;
using System;

namespace DnstapLogger
{
    /// <summary>
    /// Factory for creating <see cref="DnstapLogger"/> instances. It
    /// holds common configuration such as server identity, version and
    /// a shared <see cref="DnstapLogDispatcher"/> that is responsible
    /// for asynchronously sending messages via a single DNSTAP writer.
    /// </summary>
    public sealed class DnstapLoggerProvider : ILoggerProvider
    {
        private readonly DnstapLogDispatcher _dispatcher;
        private readonly LogLevel _minLevel;
        private readonly byte[]? _identity;
        private readonly byte[]? _version;
        private bool _disposed;

        /// <summary>
        /// Constructs a provider that writes DNSTAP messages using
        /// the specified writer. Identity and version strings are
        /// optional and will be encoded as UTF-8. Only log events
        /// with a severity at or above <paramref name="minLevel"/>
        /// will be exported.
        /// </summary>
        public DnstapLoggerProvider(
            DnstapWriter writer,
            string? identity = null,
            string? version = null,
            LogLevel minLevel = LogLevel.Information)
        {
            if (writer is null)
                throw new ArgumentNullException(nameof(writer));

            _dispatcher = new DnstapLogDispatcher(writer);
            _minLevel = minLevel;
            _identity = identity != null ? System.Text.Encoding.UTF8.GetBytes(identity) : null;
            _version = version != null ? System.Text.Encoding.UTF8.GetBytes(version) : null;
        }

        public ILogger CreateLogger(string categoryName)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(DnstapLoggerProvider));

            return new DnstapLogger(_dispatcher, _identity, _version, _minLevel, categoryName);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _dispatcher.Dispose();
            _disposed = true;
        }
    }
}
