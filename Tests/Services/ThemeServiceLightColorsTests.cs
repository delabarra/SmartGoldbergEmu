using System.Drawing;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class ThemeServiceLightColorsTests
    {
        [Fact]
        public void GetThemeColors_light_uses_classic_winforms_gray()
        {
            Color classicControl = Color.FromArgb(224, 224, 224);
            using (var theme = new ThemeService())
            {
                ThemeColors colors = theme.GetThemeColors(ThemeMode.Light);
                Assert.Equal(classicControl, colors.Background);
                Assert.Equal(SystemColors.ControlText, colors.Foreground);
                Assert.Equal(SystemColors.Window, colors.FieldBackground);
                Assert.Equal(SystemColors.WindowText, colors.FieldForeground);
                Assert.Equal(classicControl, colors.ControlBackground);
                Assert.Equal(SystemColors.ControlText, colors.ControlForeground);
                Assert.Equal(classicControl, colors.ListViewBackground);
                Assert.Equal(SystemColors.ControlText, colors.ListViewForeground);
                Assert.Equal(classicControl, colors.ListViewAlternate);
                Assert.Equal(Color.FromArgb(216, 216, 216), colors.ListViewColumnHeaderBackground);
                Assert.Equal(classicControl, colors.MenuBackground);
                Assert.Equal(classicControl, colors.StatusStripBackground);
                Assert.Equal(SystemColors.GrayText, colors.StatusTextSecondary);
                Assert.Equal(SystemColors.Highlight, colors.StatusTextAccent);
                Assert.Equal(Color.FromArgb(45, 45, 48), colors.OfflineColor);
                Assert.Equal(Color.FromArgb(160, 160, 160), colors.Border);
                Assert.Equal(SystemColors.Highlight, colors.Highlight);
                Assert.Equal(SystemColors.HighlightText, colors.HighlightText);
                Assert.Equal(SystemColors.HotTrack, colors.LinkColor);
                Assert.Equal(Color.FromArgb(232, 232, 232), colors.ImageMarginBackground);
                Assert.Equal(classicControl, colors.DisabledBackground);
                Assert.Equal(SystemColors.GrayText, colors.DisabledForeground);
                Assert.Equal(SystemColors.HotTrack, colors.InfoColor);
            }
        }

        [Fact]
        public void GetFallbackMosaicArtColors_light_uses_soft_dark_steam_logo()
        {
            using (var theme = new ThemeService())
            {
                ThemeColors colors = theme.GetThemeColors(ThemeMode.Light);
                theme.GetFallbackMosaicArtColors(ThemeMode.Light, out Color background, out Color foreground);
                Assert.Equal(colors.ListViewBackground, background);
                Assert.Equal(Color.FromArgb(90, 90, 94), foreground);
            }
        }
    }
}
