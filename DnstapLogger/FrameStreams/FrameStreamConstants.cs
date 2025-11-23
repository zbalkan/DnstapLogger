namespace DnstapLogger.FrameStreams
{
    /// <summary>
    /// Contains constants defined by the Frame Streams specification.
    /// Control frame types and field identifiers are encoded as
    /// 32‑bit unsigned integers.  See the specification for
    /// semantics.
    /// </summary>
    public static class FrameStreamConstants
    {
        // Control frame types
        public const uint CONTROL_ACCEPT = 0x01;

        public const uint CONTROL_START = 0x02;
        public const uint CONTROL_STOP = 0x03;
        public const uint CONTROL_READY = 0x04;
        public const uint CONTROL_FINISH = 0x05;

        // Control field types
        public const uint CONTROL_FIELD_CONTENT_TYPE = 0x01;
    }
}