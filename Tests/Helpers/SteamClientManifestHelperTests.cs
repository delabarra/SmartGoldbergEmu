using SmartGoldbergEmu.Helpers;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class SteamClientManifestHelperTests
    {
        [Fact]
        public void TryGetBinsWin32ZipVzFileName_ParsesManifestSnippet()
        {
            const string manifest = "\"win32\"\n{\n\t\"bins_win32\"\n\t{\n\t\t\"file\"\t\t\"bins_win32.zip.abc\"\n\t\t\"zipvz\"\t\t\"bins_win32.zip.vz.9e2e9a682812cea461b510de33ccbe43ebe31067_31353717\"\n\t}\n}\n";

            Assert.True(SteamClientManifestHelper.TryGetBinsWin32ZipVzFileName(manifest, out string zipVz));
            Assert.Equal("bins_win32.zip.vz.9e2e9a682812cea461b510de33ccbe43ebe31067_31353717", zipVz);
            Assert.Equal(
                "client/bins_win32.zip.vz.9e2e9a682812cea461b510de33ccbe43ebe31067_31353717",
                SteamClientManifestHelper.BuildClientPackageRelativePath(zipVz));
        }

        [Fact]
        public void TryGetResourcesAllZipVzFileName_ParsesManifestSnippet()
        {
            const string manifest = "\"resources_all\"\n{\n\t\"file\"\t\t\"resources_all.zip.abc\"\n\t\"zipvz\"\t\t\"resources_all.zip.vz.3c8b3203e5c69d75ea0684c2409b86fe4d0d6f83_2856188\"\n}\n";

            Assert.True(SteamClientManifestHelper.TryGetResourcesAllZipVzFileName(manifest, out string zipVz));
            Assert.Equal("resources_all.zip.vz.3c8b3203e5c69d75ea0684c2409b86fe4d0d6f83_2856188", zipVz);
            Assert.Equal(
                "client/resources_all.zip.vz.3c8b3203e5c69d75ea0684c2409b86fe4d0d6f83_2856188",
                SteamClientManifestHelper.BuildClientPackageRelativePath(zipVz));
        }

        [Fact]
        public void TryGetBinsWin32ZipVzFileName_RejectsMissingBlock()
        {
            Assert.False(SteamClientManifestHelper.TryGetBinsWin32ZipVzFileName("\"win32\" { }", out _));
            Assert.False(SteamClientManifestHelper.TryGetBinsWin32ZipVzFileName(null, out _));
        }
    }
}
