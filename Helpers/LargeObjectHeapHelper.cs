using System;
using System.Runtime;

namespace SmartGoldbergEmu.Helpers
{
    // .NET Framework LOH is not compacted by default. Large transient byte[] from VZip/7z decode
    // become unreachable but the process working set often stays high until a compacting GC.
    // Call only after known large update/self-heal work — not on routine UI paths.
    public static class LargeObjectHeapHelper
    {
        public static void CompactAfterLargeTransientAllocation()
        {
            try
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            }
            catch
            {
            }
        }
    }
}
