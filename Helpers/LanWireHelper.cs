using System;
using System.Collections.Generic;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Helpers
{
    // Shared LAN UDP wire primitives for presence + chat.
    public static class LanWireHelper
    {
        public static string SanitizeSteamId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            value = value.Trim();
            if (value.Length > LanPresenceConstants.MaxSteamIdChars)
                return string.Empty;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] < '0' || value[i] > '9')
                    return string.Empty;
            }

            return value;
        }

        // Sliding-window rate limit; caller must hold its own lock around map.
        public static bool TryConsumeRate(
            Dictionary<string, List<long>> map,
            string key,
            int maxPerWindow,
            int windowMs)
        {
            int unused;
            return TryConsumeRate(map, key, maxPerWindow, windowMs, out unused);
        }

        // When false, remainingMs is time until the oldest stamp leaves the window (at least 1).
        public static bool TryConsumeRate(
            Dictionary<string, List<long>> map,
            string key,
            int maxPerWindow,
            int windowMs,
            out int remainingMs)
        {
            remainingMs = 0;
            if (map == null || string.IsNullOrEmpty(key) || maxPerWindow <= 0 || windowMs <= 0)
                return false;

            long nowTicks = DateTime.UtcNow.Ticks;
            long cutoff = nowTicks - ((long)windowMs * TimeSpan.TicksPerMillisecond);

            List<long> stamps;
            if (!map.TryGetValue(key, out stamps))
            {
                stamps = new List<long>(maxPerWindow);
                map[key] = stamps;
            }

            int write = 0;
            for (int i = 0; i < stamps.Count; i++)
            {
                if (stamps[i] >= cutoff)
                    stamps[write++] = stamps[i];
            }

            if (write < stamps.Count)
                stamps.RemoveRange(write, stamps.Count - write);

            if (stamps.Count >= maxPerWindow)
            {
                long oldest = stamps[0];
                for (int i = 1; i < stamps.Count; i++)
                {
                    if (stamps[i] < oldest)
                        oldest = stamps[i];
                }

                long expireAt = oldest + ((long)windowMs * TimeSpan.TicksPerMillisecond);
                long remTicks = expireAt - nowTicks;
                if (remTicks <= 0)
                    remainingMs = 1;
                else
                {
                    long ms = (remTicks + TimeSpan.TicksPerMillisecond - 1) / TimeSpan.TicksPerMillisecond;
                    remainingMs = ms > int.MaxValue ? int.MaxValue : (int)ms;
                    if (remainingMs < 1)
                        remainingMs = 1;
                }

                return false;
            }

            stamps.Add(nowTicks);
            return true;
        }
    }
}
