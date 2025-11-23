using DnstapLogger.Protocol;
using ProtoBuf;
using System;
using System.IO;
using System.Net;
using System.Text;

namespace DnstapLogger
{
    public class DnstapMessage
    {
        private readonly Dnstap _payload;

        internal DnstapMessage(Dnstap dnstap)
        {
            _payload = dnstap;
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
            _payload = new Dnstap
            {
                Type = DnstapType.Message,
                Identity = IntToBytes(1),
                Version = IntToBytes(2),
                Extra = IntToBytes(3),
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
            var name = nameof(_payload.Message.Type);
            var message = Encoding.UTF8.GetString(_payload.Message.QueryMessage);
            var host = new IPAddress(_payload.Message.QueryAddress).ToString();
            var port = _payload.Message.QueryPort;
            return $"{host}:{port}\t{name}\t{message}";
        }

        private static byte[] IntToBytes(int value)
        {
            byte[] intBytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(intBytes);
            return intBytes;
        }

        public byte[] Serialize()
        {
            using var ms = new MemoryStream();
            Serializer.Serialize(ms, _payload);
            return ms.ToArray();
        }
    }
}