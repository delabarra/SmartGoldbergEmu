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

        private const string SampleUpstreamJsonVsToolchains =
            "{\"tag_name\":\"release-2026_09_16_2\",\"body\":\"VS26 FIX\",\"assets\":[" +
            "{\"name\":\"emu-win-release-vs22.7z\",\"browser_download_url\":\"https://example/vs22.7z\"}," +
            "{\"name\":\"emu-win-release-vs26.7z\",\"browser_download_url\":\"https://example/vs26.7z\"}" +
            "]}";

        private const string SampleUpstreamJsonVs22Only =
            "{\"tag_name\":\"release-2026_09_16_2\",\"body\":\"notes\",\"assets\":[" +
            "{\"name\":\"emu-win-release-vs22.7z\",\"browser_download_url\":\"https://example/vs22.7z\"}" +
            "]}";

        private const string SampleUpstreamJsonLegacyWithVs22 =
            "{\"tag_name\":\"release-2026_08_23\",\"body\":\"notes\",\"assets\":[" +
            "{\"name\":\"emu-win-release-vs22.7z\",\"browser_download_url\":\"https://example/vs22.7z\"}," +
            "{\"name\":\"emu-win-release.7z\",\"browser_download_url\":\"https://example/legacy.7z\"}" +
            "]}";

        private const string SampleRepackJsonSameDayRebuild =
            "{\"tag_name\":\"repack-2026_09_16_2-2026_02_16-2428\",\"body\":\"notes\",\"assets\":[" +
            "{\"name\":\"Detanup01-2026_09_16_2-win.7z\",\"browser_download_url\":\"https://example/detanup2.7z\"}," +
            "{\"name\":\"alex47exe-2026_02_16-win.7z\",\"browser_download_url\":\"https://example/alex.7z\"}" +
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
        public void TryParseRepackRelease_accepts_same_day_rebuild_suffix()
        {
            var result = new GoldbergResolvedRelease();
            Assert.True(GoldbergReleaseResolveHelper.TryParseRepackRelease(
                SampleRepackJsonSameDayRebuild, GoldbergForkSource.Detanup, result));
            Assert.Equal("2026_09_16_2", result.LatestVersion);
            Assert.Equal("https://example/detanup2.7z", result.DownloadUrl);
            Assert.Equal("Detanup01-2026_09_16_2-win.7z", result.ArchiveFileName);
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
            Assert.Equal("emu-win-release.7z", result.ArchiveFileName);
        }

        [Fact]
        public void TryParseUpstreamRelease_prefers_vs26_over_vs22()
        {
            var result = new GoldbergResolvedRelease();
            Assert.True(GoldbergReleaseResolveHelper.TryParseUpstreamRelease(SampleUpstreamJsonVsToolchains, result));
            Assert.Equal("2026_09_16_2", result.LatestVersion);
            Assert.Equal("https://example/vs26.7z", result.DownloadUrl);
            Assert.Equal("emu-win-release-vs26.7z", result.ArchiveFileName);
        }

        [Fact]
        public void TryParseUpstreamRelease_accepts_vs22_when_only_option()
        {
            var result = new GoldbergResolvedRelease();
            Assert.True(GoldbergReleaseResolveHelper.TryParseUpstreamRelease(SampleUpstreamJsonVs22Only, result));
            Assert.Equal("https://example/vs22.7z", result.DownloadUrl);
            Assert.Equal("emu-win-release-vs22.7z", result.ArchiveFileName);
        }

        [Fact]
        public void TryParseUpstreamRelease_prefers_legacy_bare_name_over_vs22()
        {
            var result = new GoldbergResolvedRelease();
            Assert.True(GoldbergReleaseResolveHelper.TryParseUpstreamRelease(SampleUpstreamJsonLegacyWithVs22, result));
            Assert.Equal("https://example/legacy.7z", result.DownloadUrl);
            Assert.Equal("emu-win-release.7z", result.ArchiveFileName);
        }
    }
}
