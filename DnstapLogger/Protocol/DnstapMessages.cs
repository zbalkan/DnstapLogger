using ProtoBuf;

namespace DnstapLogger.Protocol
{
    /// <summary>
    /// Top–level wrapper for DNSTAP messages.  Each Dnstap instance
    /// encapsulates exactly one DNS event and may include optional
    /// identity, version and extra fields.  The DNSTAP type is
    /// currently always MESSAGE.
    /// </summary>
    [ProtoContract(Name = "Dnstap")]
    internal class Dnstap
    {
        [ProtoMember(1, IsRequired = false)]
        public byte[]? Identity { get; set; }

        [ProtoMember(2, IsRequired = false)]
        public byte[]? Version { get; set; }

        [ProtoMember(3, IsRequired = false)]
        public byte[]? Extra { get; set; }

        [ProtoMember(15, IsRequired = true)]
        public DnstapType Type { get; set; }

        [ProtoMember(14, IsRequired = false)]
        public Message? Message { get; set; }
    }

    /// <summary>
    /// DNSTAP payload type.  Only Message is currently defined.
    /// </summary>
    public enum DnstapType
    {
        Message = 1
    }

    /// <summary>
    /// Encapsulates metadata and raw DNS data for a single DNS
    /// transaction.  Fields are optional to reduce overhead; only
    /// those relevant to the captured event need to be populated.
    /// </summary>
    [ProtoContract(Name = "Message")]
    public class Message
    {
        [ProtoMember(1, IsRequired = true)]
        public MessageType Type { get; set; }

        [ProtoMember(2, IsRequired = false)]
        public SocketFamily? SocketFamily { get; set; }

        [ProtoMember(3, IsRequired = false)]
        public SocketProtocol? SocketProtocol { get; set; }

        [ProtoMember(4, IsRequired = false)]
        public byte[]? QueryAddress { get; set; }

        [ProtoMember(5, IsRequired = false)]
        public byte[]? ResponseAddress { get; set; }

        [ProtoMember(6, IsRequired = false)]
        public uint? QueryPort { get; set; }

        [ProtoMember(7, IsRequired = false)]
        public uint? ResponsePort { get; set; }

        [ProtoMember(8, IsRequired = false)]
        public ulong? QueryTimeSec { get; set; }

        [ProtoMember(9, IsRequired = false)]
        public uint? QueryTimeNsec { get; set; }

        [ProtoMember(10, IsRequired = false)]
        public byte[]? QueryMessage { get; set; }

        [ProtoMember(11, IsRequired = false)]
        public byte[]? QueryZone { get; set; }

        [ProtoMember(12, IsRequired = false)]
        public ulong? ResponseTimeSec { get; set; }

        [ProtoMember(13, IsRequired = false)]
        public uint? ResponseTimeNsec { get; set; }

        [ProtoMember(14, IsRequired = false)]
        public byte[]? ResponseMessage { get; set; }

        [ProtoMember(15, IsRequired = false)]
        public Policy? Policy { get; set; }

        [ProtoMember(16, IsRequired = false)]
        public HttpProtocol? HttpProtocol { get; set; }
    }

    /// <summary>
    /// Enumerates the different DNS observation points defined by the
    /// DNSTAP specification.  Each value corresponds to one end of
    /// the arrows in the specification diagram.  See the protocol
    /// documentation for precise semantics.
    /// </summary>
    public enum MessageType
    {
        AuthQuery = 1,
        AuthResponse = 2,
        ResolverQuery = 3,
        ResolverResponse = 4,
        ClientQuery = 5,
        ClientResponse = 6,
        ForwarderQuery = 7,
        ForwarderResponse = 8,
        StubQuery = 9,
        StubResponse = 10,
        ToolQuery = 11,
        ToolResponse = 12,
        UpdateQuery = 13,
        UpdateResponse = 14
    }

    /// <summary>
    /// Specifies the IP version used to encode addresses in the
    /// QueryAddress and ResponseAddress fields.
    /// </summary>
    public enum SocketFamily
    {
        INET = 1,
        INET6 = 2
    }

    /// <summary>
    /// Identifies the transport protocol used for the DNS
    /// transaction.  In addition to the classic UDP and TCP this
    /// enumeration defines DOT (DNS over TLS), DOH (DNS over HTTPS),
    /// DNSCrypt over UDP and TCP, and DOQ (DNS over QUIC).
    /// </summary>
    public enum SocketProtocol
    {
        UDP = 1,
        TCP = 2,
        DOT = 3,
        DOH = 4,
        DNSCryptUDP = 5,
        DNSCryptTCP = 6,
        DOQ = 7
    }

    /// <summary>
    /// Represents the HTTP version used when DNS is transported over
    /// HTTPS (DOH).  This field should only be populated if the
    /// SocketProtocol is DOH; otherwise it is omitted.
    /// </summary>
    public enum HttpProtocol
    {
        HTTP1 = 1,
        HTTP2 = 2,
        HTTP3 = 3
    }

    /// <summary>
    /// Records information about any operator policy that was
    /// applied to a DNS message.  Policies may originate from
    /// Response Policy Zones (RPZ) or other mechanisms.  All
    /// properties are optional to minimise overhead when no policy
    /// applies.
    /// </summary>
    [ProtoContract(Name = "Policy")]
    public class Policy
    {
        /// <summary>
        /// What aspect of the DNS exchange triggered the policy.  This
        /// enumeration corresponds directly to the Policy.Match enum in
        /// the DNSTAP specification.
        /// </summary>
        public enum PolicyMatch
        {
            Qname = 1,
            ClientIp = 2,
            ResponseIp = 3,
            NsName = 4,
            NsIp = 5
        }

        /// <summary>
        /// The action taken as a result of the policy match.  This
        /// enumeration corresponds directly to the Policy.Action enum in
        /// the DNSTAP specification.
        /// </summary>
        public enum PolicyAction
        {
            NxDomain = 1,
            NoData = 2,
            Pass = 3,
            Drop = 4,
            Truncate = 5,
            LocalData = 6
        }

        [ProtoMember(1, IsRequired = false)]
        public string? Type { get; set; }

        [ProtoMember(2, IsRequired = false)]
        public byte[]? Rule { get; set; }

        [ProtoMember(3, IsRequired = false)]
        public PolicyAction? Action { get; set; }

        [ProtoMember(4, IsRequired = false)]
        public PolicyMatch? Match { get; set; }

        [ProtoMember(5, IsRequired = false)]
        public byte[]? Value { get; set; }
    }
}