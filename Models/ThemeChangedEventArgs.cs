using System;

namespace SmartGoldbergEmu.Models
{
    public class ThemeChangedEventArgs : EventArgs
    {
        public ThemeMode ThemeMode { get; }

        // Light or Dark after resolving System.
        public ThemeMode EffectiveTheme { get; }

        public ThemeChangedEventArgs(ThemeMode themeMode, ThemeMode effectiveTheme)
        {
            ThemeMode = themeMode;
            EffectiveTheme = effectiveTheme;
        }
    }
}

