using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Helpers
{
    internal static class ThemedToolStripRendererFactory
    {
        public static ThemedToolStripRenderer GetRenderer(ThemeMode themeMode, ThemeColors colors)
        {
            return new ThemedToolStripRenderer(themeMode, colors);
        }
    }
}

