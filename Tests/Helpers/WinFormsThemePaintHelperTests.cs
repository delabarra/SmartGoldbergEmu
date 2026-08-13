using SmartGoldbergEmu.Helpers;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class WinFormsThemePaintHelperTests
    {
        [Fact]
        public void ExplorerThemeName_dark_uses_dark_mode_explorer()
        {
            Assert.Equal("DarkMode_Explorer", WinFormsThemePaintHelper.ExplorerThemeName(true));
            Assert.Null(WinFormsThemePaintHelper.ExplorerThemeName(false));
        }

        [Fact]
        public void DisabledVisualStyleThemeName_dark_disables_visual_styles()
        {
            Assert.Equal(string.Empty, WinFormsThemePaintHelper.DisabledVisualStyleThemeName(true));
            Assert.Null(WinFormsThemePaintHelper.DisabledVisualStyleThemeName(false));
        }
    }
}
