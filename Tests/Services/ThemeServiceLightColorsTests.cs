using System.Drawing;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class ThemeServiceLightColorsTests
    {
        [Fact]
        public void GetThemeColors_light_uses_winforms_system_colors()
        {
            using (var theme = new ThemeService())
            {
                ThemeColors colors = theme.GetThemeColors(ThemeMode.Light);
                Assert.Equal(SystemColors.Control, colors.Background);
                Assert.Equal(SystemColors.ControlText, colors.Foreground);
                Assert.Equal(SystemColors.Window, colors.FieldBackground);
                Assert.Equal(SystemColors.WindowText, colors.FieldForeground);
                Assert.Equal(SystemColors.Control, colors.ControlBackground);
                Assert.Equal(SystemColors.ControlText, colors.ControlForeground);
                Assert.Equal(SystemColors.Window, colors.ListViewBackground);
                Assert.Equal(SystemColors.WindowText, colors.ListViewForeground);
                Assert.Equal(SystemColors.Window, colors.ListViewAlternate);
                Assert.Equal(SystemColors.Control, colors.ListViewColumnHeaderBackground);
                Assert.Equal(SystemColors.Control, colors.MenuBackground);
                Assert.Equal(SystemColors.Control, colors.StatusStripBackground);
                Assert.Equal(SystemColors.GrayText, colors.StatusTextSecondary);
                Assert.Equal(SystemColors.Highlight, colors.StatusTextAccent);
                Assert.Equal(SystemColors.ControlDark, colors.Border);
                Assert.Equal(SystemColors.Highlight, colors.Highlight);
                Assert.Equal(SystemColors.HighlightText, colors.HighlightText);
                Assert.Equal(SystemColors.HotTrack, colors.LinkColor);
                Assert.Equal(SystemColors.ControlLight, colors.ImageMarginBackground);
                Assert.Equal(SystemColors.Control, colors.DisabledBackground);
                Assert.Equal(SystemColors.GrayText, colors.DisabledForeground);
                Assert.Equal(SystemColors.HotTrack, colors.InfoColor);
            }
        }

        [Fact]
        public void GetFallbackMosaicArtColors_light_matches_list_view_theme()
        {
            using (var theme = new ThemeService())
            {
                ThemeColors colors = theme.GetThemeColors(ThemeMode.Light);
                theme.GetFallbackMosaicArtColors(ThemeMode.Light, out Color background, out Color foreground);
                Assert.Equal(colors.ListViewBackground, background);
                Assert.Equal(colors.ListViewForeground, foreground);
            }
        }
    }
}
