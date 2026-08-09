using System;

namespace SmartGoldbergEmu.ExtractKit
{
    // Maps archive extract byte progress into UI percentage ranges.
    public static class ArchiveExtractProgress
    {
        public static int MapToPercent(long completedBytes, long totalBytes, int rangeStart, int rangeEnd)
        {
            if (totalBytes <= 0)
                return rangeStart;

            int span = Math.Max(0, rangeEnd - rangeStart);
            double ratio = completedBytes / (double)totalBytes;
            if (ratio < 0)
                ratio = 0;
            if (ratio > 1)
                ratio = 1;

            int percentage = rangeStart + (int)(ratio * span);
            if (percentage > rangeEnd)
                percentage = rangeEnd;
            return percentage;
        }
    }
}
