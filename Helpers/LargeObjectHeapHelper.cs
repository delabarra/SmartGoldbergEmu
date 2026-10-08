using System;
using System.Runtime;

namespace SmartGoldbergEmu.Helpers
{
    // The LOH is not compacted by default, so large VZip/7z decode buffers keep the working set high.
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
