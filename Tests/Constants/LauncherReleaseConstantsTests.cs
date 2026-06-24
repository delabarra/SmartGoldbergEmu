using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Tests.TestSupport;
using Xunit;

namespace SmartGoldbergEmu.Tests.Constants
{
    [Collection("StaticServiceHooks")]
    public sealed class LauncherReleaseConstantsTests
    {
        [Fact]
        public void TryGetReleasesApiUrl_returns_default_github_api_when_no_override()
        {
            using (var scope = new ServiceLocatorTestScope("sge-launcher-api-default-"))
            {
                Assert.True(LauncherReleaseConstants.TryGetReleasesApiUrl(out string apiUrl));
                Assert.Equal(
                    string.Format(
                        LauncherReleaseConstants.ReleasesApiUrlFormat,
                        LauncherReleaseConstants.GitHubOwner,
                        LauncherReleaseConstants.GitHubRepo),
                    apiUrl);
            }
        }

        [Fact]
        public void TryGetReleasesApiUrl_returns_trimmed_override_when_safe_https_url_configured()
        {
            using (var scope = new ServiceLocatorTestScope("sge-launcher-api-override-"))
            {
                scope.WriteConfig(
                    "[application]\r\n" +
                    "launcher_update_api_url=  https://example.test/releases/latest  \r\n");

                Assert.True(LauncherReleaseConstants.TryGetReleasesApiUrl(out string apiUrl));
                Assert.Equal("https://example.test/releases/latest", apiUrl);
            }
        }

        [Theory]
        [InlineData("file:///C:/mock/releases/latest")]
        [InlineData("javascript:alert(1)")]
        [InlineData("")]
        [InlineData("   ")]
        public void TryGetReleasesApiUrl_ignores_unsafe_or_empty_override_and_falls_back(string overrideUrl)
        {
            using (var scope = new ServiceLocatorTestScope("sge-launcher-api-unsafe-"))
            {
                scope.WriteConfig(
                    "[application]\r\n" +
                    "launcher_update_api_url=" + overrideUrl + "\r\n");

                Assert.True(LauncherReleaseConstants.TryGetReleasesApiUrl(out string apiUrl));
                Assert.Equal(
                    string.Format(
                        LauncherReleaseConstants.ReleasesApiUrlFormat,
                        LauncherReleaseConstants.GitHubOwner,
                        LauncherReleaseConstants.GitHubRepo),
                    apiUrl);
            }
        }
    }
}
