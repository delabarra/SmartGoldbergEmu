using SmartGoldbergEmu.Helpers;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class ApplicationVersionHelperTests
    {
        [Theory]
        [InlineData(2, 4, 0, "2.4")]
        [InlineData(2, 4, 1, "2.4.1")]
        public void FormatVersionLabel_omits_zero_patch(int major, int minor, int patch, string expected)
        {
            Assert.Equal(expected, ApplicationVersionHelper.FormatVersionLabel(major, minor, patch));
        }

        [Theory]
        [InlineData(2, 4, 0, 0, null, "2.4")]
        [InlineData(2, 4, 1, 0, null, "2.4.1")]
        [InlineData(2, 4, 1, 42, null, "2.4.1")]
        [InlineData(3, 0, 0, 7, null, "3.0")]
        [InlineData(2, 4, 0, 0, "preview", "2.4")]
        [InlineData(2, 4, 1, 16, "preview", "2.4.1 (build 16, preview)")]
        public void FormatDisplayVersion_wraps_build_and_preview_in_parentheses(
            int major, int minor, int patch, int build, string previewLabel, string expected)
        {
            Assert.Equal(expected, ApplicationVersionHelper.FormatDisplayVersion(major, minor, patch, build, previewLabel));
        }

        [Fact]
        public void GetWindowTitle_includes_base_title_and_version_segment()
        {
            string title = ApplicationVersionHelper.GetWindowTitle();

            Assert.StartsWith("SmartGoldbergEmu Launcher - ", title);
            Assert.False(string.IsNullOrWhiteSpace(title.Substring("SmartGoldbergEmu Launcher - ".Length)));
        }

        [Theory]
        [InlineData("v2.4.1-preview+build.16", "2.4.1 (build 16, preview)")]
        [InlineData("v2.4.1+build.16-preview", "2.4.1 (build 16, preview)")]
        [InlineData("v2.4.1+build.16", "2.4.1")]
        [InlineData("v2.4.0-preview", "2.4")]
        [InlineData("v2.4.0", "2.4")]
        public void TryParseProductVersion_formats_display_version_from_product_strings(string productVersion, string expected)
        {
            Assert.True(ApplicationVersionHelper.TryParseProductVersion(
                productVersion,
                out int major,
                out int minor,
                out int patch,
                out int build,
                out string previewLabel));

            Assert.Equal(expected, ApplicationVersionHelper.FormatDisplayVersion(major, minor, patch, build, previewLabel));
        }
    }
}
