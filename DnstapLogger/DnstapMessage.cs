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

        public DnstapMessage(
            string queryMessage,
            IPAddress queryAddress,
            uint queryPort = 53,
            MessageType queryType = MessageType.AuthQuery,
            SocketFamily socketFamily = SocketFamily.INET,
            SocketProtocol socketProtocol = SocketProtocol.TCP,
            string? queryZone = null,
            IPAddress? responseAddress = null,
            uint? responsePort = null,
            byte[]? responseMessageWire = null)
        {
            var (sec, nsec) = UnixTime.GetTimestamp();

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

                    QueryMessage = SimpleDnsQueryBuilder.BuildQuery(queryMessage),
                    QueryTimeSec = sec,
                    QueryTimeNsec = nsec,

                    QueryZone = queryZone != null ? ToDnsWireFormat(queryZone) : null,

                    ResponseAddress = responseAddress?.GetAddressBytes(),
                    ResponsePort = responsePort,
                    ResponseMessage = responseMessageWire
                }
            };
        }

        public override string? ToString()
        {
            const string name = nameof(_payload.Message.Type);
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

        public static byte[] ToDnsWireFormat(string name)
        {
            var labels = name.TrimEnd('.').Split('.');
            using var ms = new MemoryStream();

            foreach (var label in labels)
            {
                var bytes = Encoding.ASCII.GetBytes(label);
                ms.WriteByte((byte)bytes.Length);
                ms.Write(bytes, 0, bytes.Length);
            }

            ms.WriteByte(0);  // root terminator
            return ms.ToArray();
        }


        public byte[] Serialize()
        {
            using var ms = new MemoryStream();
            Serializer.Serialize(ms, _payload);
            return ms.ToArray();
        }
    }

    public static class SimpleDnsQueryBuilder
    {
        public static byte[] BuildQuery(string qname, ushort id = 0x1234,
                                        ushort qtype = 1,   // A record
                                        ushort qclass = 1)  // IN
        {
            using var ms = new MemoryStream();

            // --------------------------------------------------------------------
            // DNS HEADER (12 bytes)
            // --------------------------------------------------------------------
            // Transaction ID
            ms.WriteByte((byte)(id >> 8));
            ms.WriteByte((byte)(id & 0xFF));

            // Flags: standard query, recursion desired
            ms.WriteByte(0x01); // QR=0 | Opcode=0 | RD=1
            ms.WriteByte(0x00);

            // QDCOUNT = 1
            ms.WriteByte(0x00);
            ms.WriteByte(0x01);

            // ANCOUNT, NSCOUNT, ARCOUNT = 0
            ms.Write(new byte[6], 0, 6);

            // --------------------------------------------------------------------
            // QNAME
            // --------------------------------------------------------------------
            foreach (var label in qname.TrimEnd('.').Split('.'))
            {
                var bytes = Encoding.ASCII.GetBytes(label);
                if (bytes.Length > 63)
                    throw new ArgumentException("Label too long.");
                ms.WriteByte((byte)bytes.Length);
                ms.Write(bytes, 0, bytes.Length);
            }

            // root label
            ms.WriteByte(0x00);

            // --------------------------------------------------------------------
            // QTYPE (2 bytes)
            // --------------------------------------------------------------------
            ms.WriteByte((byte)(qtype >> 8));
            ms.WriteByte((byte)(qtype & 0xFF));

            // --------------------------------------------------------------------
            // QCLASS (2 bytes, IN = 1)
            // --------------------------------------------------------------------
            ms.WriteByte((byte)(qclass >> 8));
            ms.WriteByte((byte)(qclass & 0xFF));

            return ms.ToArray();
        }
    }

}