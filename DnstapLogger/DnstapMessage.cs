using DnstapLogger.Protocol;
using System;
using System.Net;
using System.Text;

namespace DnstapLogger
{
    public class DnstapMessage
    {
        internal Dnstap Payload { get; }

        internal DnstapMessage(Dnstap dnstap)
        {
            Payload = dnstap;
        }

        public DnstapMessage(string queryMessage,
            IPAddress queryAddress,
            uint queryPort = 53,
            MessageType queryType = MessageType.AuthQuery,
            SocketFamily socketFamily = SocketFamily.INET,
            SocketProtocol socketProtocol = SocketProtocol.TCP,
            string? queryZone = null,
            IPAddress? responseAddress = null,
            uint? responsePort = null,
            string? responseMessage = null)
        {
            var now = GetUnixTimestampParts();
            Payload = new Dnstap
            {
                Message = new Message
                {
                    Type = queryType,
                    SocketFamily = socketFamily,
                    SocketProtocol = socketProtocol,
                    QueryAddress = queryAddress.GetAddressBytes(),
                    QueryPort = queryPort,
                    QueryMessage = Encoding.UTF8.GetBytes(queryMessage),
                    QueryTimeSec = now.Seconds,
                    QueryTimeNsec = now.Nanoseconds,
                    QueryZone = queryZone != null ? Encoding.UTF8.GetBytes(queryZone) : null,
                    ResponseAddress = responseAddress?.GetAddressBytes(),
                    ResponsePort = responsePort,
                    ResponseMessage = responseMessage != null ? Encoding.UTF8.GetBytes(responseMessage) : null
                }
            };
        }

        private static (ulong Seconds, uint Nanoseconds) GetUnixTimestampParts()
        {
            var now = DateTime.UtcNow;

            long seconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            long ticksSinceEpoch = now.Ticks - DateTime.UnixEpoch.Ticks;
            long ticksIntoSecond = ticksSinceEpoch % TimeSpan.TicksPerSecond;

            uint nanos = (uint)(ticksIntoSecond * 100);

            return ((ulong)seconds, nanos);
        }

        public override string? ToString()
        {
            var name = nameof(Payload.Message.Type);
            var message = Encoding.UTF8.GetString(Payload.Message.QueryMessage);
            var host = new IPAddress(Payload.Message.QueryAddress).ToString();
            var port = Payload.Message.QueryPort;
            return $"{host}:{port}\t{name}\t{message}";
        }
    }
}