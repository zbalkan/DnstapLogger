using DnstapLogger.Protocol;
using Microsoft.Extensions.Logging;
using System;

namespace DnstapLogger
{
    /// <summary>
    /// An implementation of <see cref="ILogger"/> that sends log
    /// events as DNSTAP messages.  Each call to <see
    /// cref="Log"/> serialises a DNSTAP message and writes it via
    /// the shared <see cref="DnstapWriter"/>.  The mapping from
    /// log level to DNSTAP message type is deliberately simple:
    /// messages are represented as TOOL_QUERY events with the log
    /// message stored verbatim in the QueryMessage field.  The
    /// timestamp is taken at the moment of logging.  Only events
    /// with severity greater than or equal to the configured
    /// minimum level are sent.
    /// </summary>
    internal sealed class DnstapLogger : ILogger
    {
        private readonly DnstapWriter _writer;
        private readonly byte[] _identity;
        private readonly byte[] _version;
        private readonly LogLevel _minLevel;
        private readonly string _category;

        public DnstapLogger(DnstapWriter writer, byte[] identity, byte[] version, LogLevel minLevel, string category)
        {
            _writer = writer;
            _identity = identity;
            _version = version;
            _minLevel = minLevel;
            _category = category;
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

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }
            ArgumentNullException.ThrowIfNull(formatter);
            var messageText = formatter(state, exception);
            // Build DNSTAP message
            var dnstap = new DnstapMessage(new Dnstap
            {
                Type = DnstapType.Message,
                Identity = _identity,
                Version = _version,
                Message = new Message
                {
                    Type = MessageType.ToolQuery,
                    QueryTimeSec = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    QueryTimeNsec = (uint)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 1000 * 1_000_000),
                    QueryMessage = messageText != null ? System.Text.Encoding.UTF8.GetBytes(messageText) : null,
                }
            });
            // Write asynchronously but wait synchronously to preserve
            // logger contract.  Exceptions thrown during writing will
            // propagate to the caller.
            _writer.WriteMessageAsync(dnstap).GetAwaiter().GetResult();
        }

        private class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new NullScope();

            public void Dispose()
            { }
        }
    }
}