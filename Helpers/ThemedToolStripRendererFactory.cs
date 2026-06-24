using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Helpers
{
    /// <summary>
    /// Factory for creating ThemedToolStripRenderer instances.
    /// Centralizes renderer creation to ensure consistent initialization.
    /// </summary>
    internal static class ThemedToolStripRendererFactory
    {
        /// <summary>
        /// Creates a new ThemedToolStripRenderer instance for the specified theme mode and colors.
        /// </summary>
        /// <param name="themeMode">The theme mode.</param>
        /// <param name="colors">The theme colors.</param>
        /// <returns>A new ThemedToolStripRenderer instance.</returns>
        public static ThemedToolStripRenderer GetRenderer(ThemeMode themeMode, ThemeColors colors)
        {
            return new ThemedToolStripRenderer(themeMode, colors);
        }
    }
}

