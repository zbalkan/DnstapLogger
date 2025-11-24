using DnstapLogger.Protocol;
using Microsoft.Extensions.Logging;
using System;
using System.Text;

namespace DnstapLogger
{
    /// <summary>
    /// An implementation of <see cref="ILogger"/> that sends log
    /// events as DNSTAP messages.  Each call to <see cref="Log"/>
    /// enqueues a DNSTAP message for asynchronous writing via a
    /// shared <see cref="DnstapLogDispatcher"/>.
    /// </summary>
    internal sealed class DnstapLogger : ILogger
    {
        private readonly DnstapLogDispatcher _dispatcher;
        private readonly byte[]? _identity;
        private readonly byte[]? _version;
        private readonly LogLevel _minLevel;
        private readonly string _category;

        public DnstapLogger(
            DnstapLogDispatcher dispatcher,
            byte[]? identity,
            byte[]? version,
            LogLevel minLevel,
            string category)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _identity = identity;
            _version = version;
            _minLevel = minLevel;
            _category = category ?? throw new ArgumentNullException(nameof(category));
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            // Scopes are ignored in this implementation
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= _minLevel;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            ArgumentNullException.ThrowIfNull(formatter);

            var messageText = formatter(state, exception);

            var (sec, nsec) = UnixTime.GetTimestamp();

            // Build DNSTAP message
            var dnstap = new DnstapMessage(new Dnstap
            {
                Type = DnstapType.Message,
                Identity = _identity,
                Version = _version,
                Extra = messageText != null ? Encoding.UTF8.GetBytes(messageText) : null,
            });

            // Enqueue for asynchronous writing. This is non-blocking and
            // does not synchronously wait on I/O.
            _dispatcher.Enqueue(dnstap);
        }

        private class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new NullScope();

            public void Dispose()
            {
            }
        }
    }
}
