using System;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Helpers
{
    /// <summary>
    /// Helper for RGBA color conversion between overlay settings (0.0-1.0 floats) and System.Drawing.Color.
    /// </summary>
    public static class OverlayColorHelper
    {
        /// <summary>
        /// Converts RGBA float values (0.0-1.0) to a System.Drawing.Color.
        /// </summary>
        public static System.Drawing.Color RgbaToColor(float r, float g, float b, float a)
        {
            r = Math.Max(0.0f, Math.Min(1.0f, r));
            g = Math.Max(0.0f, Math.Min(1.0f, g));
            b = Math.Max(0.0f, Math.Min(1.0f, b));
            a = Math.Max(0.0f, Math.Min(1.0f, a));
            int red = (int)(r * 255.0f);
            int green = (int)(g * 255.0f);
            int blue = (int)(b * 255.0f);
            int alpha = (int)(a * 255.0f);
            return System.Drawing.Color.FromArgb(alpha, red, green, blue);
        }

        /// <summary>
        /// Converts a System.Drawing.Color to RGBA float values (0.0-1.0).
        /// </summary>
        public static float ColorToR(System.Drawing.Color color) => color.R / 255.0f;
        public static float ColorToG(System.Drawing.Color color) => color.G / 255.0f;
        public static float ColorToB(System.Drawing.Color color) => color.B / 255.0f;
        public static float ColorToA(System.Drawing.Color color) => color.A / 255.0f;

        /// <summary>
        /// Gets default overlay color by name from OverlaySettings defaults.
        /// </summary>
        public static System.Drawing.Color GetDefaultOverlayColor(OverlaySettings defaults, string colorName)
        {
            switch (colorName)
            {
                case "Notification":
                    return RgbaToColor(defaults.NotificationR, defaults.NotificationG, defaults.NotificationB, defaults.NotificationA);
                case "Background":
                    return RgbaToColor(defaults.BackgroundR, defaults.BackgroundG, defaults.BackgroundB, defaults.BackgroundA);
                case "Element":
                    return RgbaToColor(defaults.ElementR, defaults.ElementG, defaults.ElementB, defaults.ElementA);
                case "ElementHovered":
                    return RgbaToColor(defaults.ElementHoveredR, defaults.ElementHoveredG, defaults.ElementHoveredB, defaults.ElementHoveredA);
                case "ElementActive":
                    // -1.0 means "no override" — represent as Gray in the UI
                    return defaults.ElementActiveR < 0
                        ? System.Drawing.Color.Gray
                        : RgbaToColor(defaults.ElementActiveR, defaults.ElementActiveG, defaults.ElementActiveB, defaults.ElementActiveA);
                case "StatsBackground":
                    return RgbaToColor(defaults.StatsBackgroundR, defaults.StatsBackgroundG, defaults.StatsBackgroundB, defaults.StatsBackgroundA);
                case "StatsText":
                    return RgbaToColor(defaults.StatsTextR, defaults.StatsTextG, defaults.StatsTextB, defaults.StatsTextA);
                default:
                    return System.Drawing.Color.Gray;
            }
        }
    }
}
