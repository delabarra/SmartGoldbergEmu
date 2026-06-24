using System;

namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// Event arguments for theme change events.
    /// </summary>
    public class ThemeChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the theme mode that was set.
        /// </summary>
        public ThemeMode ThemeMode { get; }

        /// <summary>
        /// Gets the effective theme that is actually applied.
        /// </summary>
        public ThemeMode EffectiveTheme { get; }

        /// <summary>
        /// Initializes a new instance of the ThemeChangedEventArgs class.
        /// </summary>
        /// <param name="themeMode">The theme mode that was set.</param>
        /// <param name="effectiveTheme">The effective theme that is applied.</param>
        public ThemeChangedEventArgs(ThemeMode themeMode, ThemeMode effectiveTheme)
        {
            ThemeMode = themeMode;
            EffectiveTheme = effectiveTheme;
        }
    }
}

