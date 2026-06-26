using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class GoldbergReleaseResolveHelperTests
    {
        private const string SampleRepackJson =
            "{\"tag_name\":\"repack-2026_05_30-2026_02_16-420\",\"body\":\"notes\",\"assets\":[" +
            "{\"name\":\"Detanup01-2026_05_30-win.7z\",\"browser_download_url\":\"https://example/detanup.7z\"}," +
            "{\"name\":\"alex47exe-2026_02_16-win.7z\",\"browser_download_url\":\"https://example/alex.7z\"}" +
            "]}";

        private const string SampleRepackJsonBothFormats =
            "{\"tag_name\":\"repack-2026_05_30-2026_02_16-420\",\"body\":\"notes\",\"assets\":[" +
            "{\"name\":\"Detanup01-2026_05_30-win.zip\",\"browser_download_url\":\"https://example/detanup.zip\"}," +
            "{\"name\":\"Detanup01-2026_05_30-win.7z\",\"browser_download_url\":\"https://example/detanup.7z\"}" +
            "]}";

        private const string SampleUpstreamJson =
            "{\"tag_name\":\"release-2026_05_19\",\"body\":\"upstream notes\",\"assets\":[" +
            "{\"name\":\"emu-win-release.7z\",\"browser_download_url\":\"https://example/upstream.7z\"}" +
            "]}";

        [Fact]
        public void TryParseRepackRelease_selects_detanup_asset()
        {
            var result = new GoldbergResolvedRelease();
            Assert.True(GoldbergReleaseResolveHelper.TryParseRepackRelease(SampleRepackJson, GoldbergForkSource.Detanup, result));
            Assert.True(result.FromRepack);
            Assert.Equal("2026_05_30", result.LatestVersion);
            Assert.Equal("https://example/detanup.7z", result.DownloadUrl);
            Assert.Equal("Detanup01-2026_05_30-win.7z", result.ArchiveFileName);
        }

        [Fact]
        public void TryParseRepackRelease_selects_alex_asset()
        {
            var result = new GoldbergResolvedRelease();
            Assert.True(GoldbergReleaseResolveHelper.TryParseRepackRelease(SampleRepackJson, GoldbergForkSource.Alex, result));
            Assert.Equal("2026_02_16", result.LatestVersion);
            Assert.Equal("https://example/alex.7z", result.DownloadUrl);
        }

        [Fact]
        public void TryParseRepackRelease_prefers_7z_when_both_formats_present()
        {
            var result = new GoldbergResolvedRelease();
            Assert.True(GoldbergReleaseResolveHelper.TryParseRepackRelease(SampleRepackJsonBothFormats, GoldbergForkSource.Detanup, result));
            Assert.Equal("https://example/detanup.7z", result.DownloadUrl);
            Assert.Equal("Detanup01-2026_05_30-win.7z", result.ArchiveFileName);
        }

        [Fact]
        public void TryParseUpstreamRelease_reads_emu_win_release_asset()
        {
            var result = new GoldbergResolvedRelease();
            Assert.True(GoldbergReleaseResolveHelper.TryParseUpstreamRelease(SampleUpstreamJson, result));
            Assert.False(result.FromRepack);
            Assert.Equal("2026_05_19", result.LatestVersion);
            Assert.Equal("https://example/upstream.7z", result.DownloadUrl);
        }
    }
}
