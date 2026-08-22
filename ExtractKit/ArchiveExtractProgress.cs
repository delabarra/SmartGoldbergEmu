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

        public static long ScaleBytes(long fileWeight, long decodedBytes, long folderUnpackBytes)
        {
            if (fileWeight <= 0)
                return 0;
            if (folderUnpackBytes <= 0)
                return fileWeight;
            if (decodedBytes >= folderUnpackBytes)
                return fileWeight;

            long partial = (long)(fileWeight * (decodedBytes / (double)folderUnpackBytes));
            if (partial < 0)
                return 0;
            if (partial > fileWeight)
                return fileWeight;
            return partial;
        }

        // Solid 7z folders decode every file in the block at once. Weight that work against
        // remaining extract bytes so the first file does not race to 100% then snap back.
        public static long DecodeWeight(long entryWeight, long folderUnpackBytes, long remainingBytes)
        {
            if (entryWeight < 0)
                entryWeight = 0;
            if (folderUnpackBytes > entryWeight && remainingBytes > entryWeight)
            {
                if (folderUnpackBytes < remainingBytes)
                    return folderUnpackBytes;
                return remainingBytes;
            }

            return entryWeight;
        }

        // After extract: if a solid folder was decoded, credit that unpack (capped to remaining)
        // so later cache-hit files do not rewind the byte total.
        public static long CompletedDelta(long entryWeight, long folderUnpackBytes, long remainingBytes)
        {
            if (entryWeight < 0)
                entryWeight = 0;
            if (folderUnpackBytes <= 0 || folderUnpackBytes <= entryWeight)
                return entryWeight;
            if (remainingBytes < entryWeight)
                return entryWeight;

            if (folderUnpackBytes < remainingBytes)
                return folderUnpackBytes;
            return remainingBytes;
        }
    }
}
