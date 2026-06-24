using System.ComponentModel;

namespace SmartGoldbergEmu.Helpers
{
    internal static class DesignTimeHelper
    {
        internal static bool IsDesignTime =>
            LicenseManager.UsageMode == LicenseUsageMode.Designtime;
    }
}
