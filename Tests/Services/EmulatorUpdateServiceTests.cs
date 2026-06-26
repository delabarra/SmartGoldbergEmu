using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using SmartGoldbergEmu.Tests.TestSupport;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    [Collection("StaticServiceHooks")]
    public sealed class EmulatorUpdateServiceTests
    {
        private const string SampleRepackJson =
            "{\"tag_name\":\"repack-2026_05_30-2026_02_16-420\",\"body\":\"repack notes\",\"assets\":[" +
            "{\"name\":\"Detanup01-2026_05_30-win.7z\",\"browser_download_url\":\"https://example/detanup.7z\"}," +
            "{\"name\":\"alex47exe-2026_02_16-win.7z\",\"browser_download_url\":\"https://example/alex.7z\"}" +
            "]}";

        private const string SampleRepackJsonLegacyZip =
            "{\"tag_name\":\"repack-2026_05_19-2026_02_16-1\",\"body\":\"repack notes\",\"assets\":[" +
            "{\"name\":\"Detanup01-2026_05_19-win.zip\",\"browser_download_url\":\"https://example/detanup.zip\"}," +
            "{\"name\":\"alex47exe-2026_02_16-win.zip\",\"browser_download_url\":\"https://example/alex.zip\"}" +
            "]}";

        private const string SampleRepackJsonAlexOnly =
            "{\"tag_name\":\"repack-2026_05_30-2026_02_16-420\",\"body\":\"repack notes\",\"assets\":[" +
            "{\"name\":\"alex47exe-2026_02_16-win.7z\",\"browser_download_url\":\"https://example/alex.7z\"}" +
            "]}";

        private const string SampleUpstreamJson =
            "{\"tag_name\":\"release-2026_05_19\",\"body\":\"upstream notes\",\"assets\":[" +
            "{\"name\":\"emu-win-release.7z\",\"browser_download_url\":\"https://example/upstream.7z\"}" +
            "]}";

        [Fact]
        public async Task CheckForUpdatesAsync_succeeds_from_repack_and_reports_update_when_newer()
        {
            using (var appScope = new ServiceLocatorTestScope("sge-emu-update-newer-"))
            using (var httpScope = new HttpServiceTestScope())
            {
                appScope.WriteEmulatorConfig(GoldbergForkSource.Detanup, "2026_02_16");
                httpScope.HttpService.SetJsonResponse(GoldbergForkConstants.RepackReleasesApiUrl, SampleRepackJson);

                UpdateCheckResult result = await EmulatorUpdateService.CheckForUpdatesAsync();

                Assert.True(result.Success);
                Assert.True(result.UpdateAvailable);
                Assert.Equal("2026_02_16", result.CurrentVersion);
                Assert.Equal("2026_05_30", result.LatestVersion);
                Assert.Equal("https://example/detanup.7z", result.DownloadUrl);
                Assert.True(result.FromRepack);
                Assert.Equal("repack notes", result.ReleaseNotes);
            }
        }

        [Fact]
        public async Task CheckForUpdatesAsync_reports_no_update_when_current_matches_latest()
        {
            using (var appScope = new ServiceLocatorTestScope("sge-emu-update-current-"))
            using (var httpScope = new HttpServiceTestScope())
            {
                appScope.WriteEmulatorConfig(GoldbergForkSource.Detanup, "2026_05_30");
                httpScope.HttpService.SetJsonResponse(GoldbergForkConstants.RepackReleasesApiUrl, SampleRepackJson);

                UpdateCheckResult result = await EmulatorUpdateService.CheckForUpdatesAsync();

                Assert.True(result.Success);
                Assert.False(result.UpdateAvailable);
                Assert.Equal("2026_05_30", result.CurrentVersion);
                Assert.Equal("2026_05_30", result.LatestVersion);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("pre-existent")]
        public async Task CheckForUpdatesAsync_reports_update_when_current_version_unknown(string currentVersion)
        {
            using (var appScope = new ServiceLocatorTestScope("sge-emu-update-unknown-"))
            using (var httpScope = new HttpServiceTestScope())
            {
                if (currentVersion != null)
                    appScope.WriteEmulatorConfig(GoldbergForkSource.Detanup, currentVersion);
                else
                    appScope.WriteEmulatorConfig(GoldbergForkSource.Detanup);

                httpScope.HttpService.SetJsonResponse(GoldbergForkConstants.RepackReleasesApiUrl, SampleRepackJson);

                UpdateCheckResult result = await EmulatorUpdateService.CheckForUpdatesAsync();

                Assert.True(result.Success);
                Assert.True(result.UpdateAvailable);
                Assert.Equal(currentVersion, result.CurrentVersion);
            }
        }

        [Fact]
        public async Task CheckForUpdatesAsync_succeeds_from_legacy_repack_zip()
        {
            using (var appScope = new ServiceLocatorTestScope("sge-emu-update-legacy-zip-"))
            using (var httpScope = new HttpServiceTestScope())
            {
                appScope.WriteEmulatorConfig(GoldbergForkSource.Detanup, "2026_02_16");
                httpScope.HttpService.SetJsonResponse(GoldbergForkConstants.RepackReleasesApiUrl, SampleRepackJsonLegacyZip);

                UpdateCheckResult result = await EmulatorUpdateService.CheckForUpdatesAsync();

                Assert.True(result.Success);
                Assert.True(result.UpdateAvailable);
                Assert.Equal("2026_05_19", result.LatestVersion);
                Assert.Equal("https://example/detanup.zip", result.DownloadUrl);
                Assert.True(result.FromRepack);
            }
        }

        [Fact]
        public async Task CheckForUpdatesAsync_falls_back_to_upstream_when_repack_lacks_fork_asset()
        {
            using (var appScope = new ServiceLocatorTestScope("sge-emu-update-fallback-"))
            using (var httpScope = new HttpServiceTestScope())
            {
                appScope.WriteEmulatorConfig(GoldbergForkSource.Detanup, "2026_02_16");
                httpScope.HttpService.SetJsonResponse(GoldbergForkConstants.RepackReleasesApiUrl, SampleRepackJsonAlexOnly);
                httpScope.HttpService.SetJsonResponse(
                    GoldbergForkConstants.GetUpstreamReleasesApiUrl(GoldbergForkSource.Detanup),
                    SampleUpstreamJson);

                UpdateCheckResult result = await EmulatorUpdateService.CheckForUpdatesAsync();

                Assert.True(result.Success);
                Assert.True(result.UpdateAvailable);
                Assert.Equal("2026_05_19", result.LatestVersion);
                Assert.Equal("https://example/upstream.7z", result.DownloadUrl);
                Assert.False(result.FromRepack);
            }
        }

        [Fact]
        public async Task CheckForUpdatesAsync_surfaces_github_rate_limit_without_upstream_fallback()
        {
            using (var appScope = new ServiceLocatorTestScope("sge-emu-update-ratelimit-"))
            using (var httpScope = new HttpServiceTestScope())
            {
                appScope.WriteEmulatorConfig(GoldbergForkSource.Detanup, "2026_02_16");
                httpScope.HttpService.SetResponse(
                    GoldbergForkConstants.RepackReleasesApiUrl,
                    () => new HttpResponseMessage(HttpStatusCode.Forbidden)
                    {
                        Content = new StringContent("{\"message\":\"API rate limit exceeded\"}")
                    });

                UpdateCheckResult result = await EmulatorUpdateService.CheckForUpdatesAsync();

                Assert.False(result.Success);
                Assert.Contains("rate limit", result.ErrorMessage, System.StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void GetCurrentVersion_reads_stored_goldberg_version()
        {
            using (var scope = new ServiceLocatorTestScope("sge-emu-version-read-"))
            {
                scope.WriteEmulatorConfig(GoldbergForkSource.Detanup, "2026_05_19");

                Assert.Equal("2026_05_19", EmulatorUpdateService.GetCurrentVersion());
            }
        }

        [Fact]
        public void SaveCurrentVersion_persists_goldberg_version()
        {
            using (var scope = new ServiceLocatorTestScope("sge-emu-version-save-"))
            {
                EmulatorUpdateService.SaveCurrentVersion("2026_05_19");

                Assert.Equal("2026_05_19", scope.AppDataService.GetGoldbergVersion());
                Assert.Equal("2026_05_19", EmulatorUpdateService.GetCurrentVersion());
            }
        }
    }
}
