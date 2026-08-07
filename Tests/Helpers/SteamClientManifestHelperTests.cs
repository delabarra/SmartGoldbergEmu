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
        public void TryGetBinsWin32ZipVzFileName_RejectsMissingBlock()
        {
            Assert.False(SteamClientManifestHelper.TryGetBinsWin32ZipVzFileName("\"win32\" { }", out _));
            Assert.False(SteamClientManifestHelper.TryGetBinsWin32ZipVzFileName(null, out _));
        }
    }
}
