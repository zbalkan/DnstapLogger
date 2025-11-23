using System;

namespace DnstapLogger
{
    internal static class UnixTime
    {
        /// <summary>
        /// Returns (seconds, nanoseconds) according to DNSTAP spec.
        /// Nanoseconds are derived from ticks (100 ns units).
        /// </summary>
        public static (ulong Seconds, uint Nanoseconds) GetTimestamp()
        {
            var now = DateTime.UtcNow;

            long seconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            long ticksSinceEpoch = now.Ticks - DateTime.UnixEpoch.Ticks;
            long ticksIntoSecond = ticksSinceEpoch % TimeSpan.TicksPerSecond;

            uint nanos = (uint)(ticksIntoSecond * 100);   // 1 tick = 100 ns

            return ((ulong)seconds, nanos);
        }
    }
}