using System.IO;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Tests.TestSupport;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class SteamClientExtraDllsStagingTests
    {
        [Fact]
        public void TryStageIntoLoadDllsFolder_copies_only_matching_arch_dlls()
        {
            string source = TestFileHelper.CreateTempDirectory("sge-extra-src-");
            string dest = TestFileHelper.CreateTempDirectory("sge-extra-dst-");
            try
            {
                File.WriteAllBytes(Path.Combine(source, "mod_x64.dll"), new byte[] { 1 });
                File.WriteAllBytes(Path.Combine(source, "mod_x32.dll"), new byte[] { 2 });
                File.WriteAllBytes(Path.Combine(source, "mod_any.dll"), new byte[] { 3 });

                Assert.True(SteamClientExtraDllsStaging.TryStageIntoLoadDllsFolder(source, dest, useX64: true, out int copied));
                Assert.Equal(2, copied);

                Assert.True(File.Exists(Path.Combine(dest, "mod_x64.dll")));
                Assert.True(File.Exists(Path.Combine(dest, "mod_any.dll")));
                Assert.False(File.Exists(Path.Combine(dest, "mod_x32.dll")));
            }
            finally
            {
                try { Directory.Delete(source, recursive: true); } catch { }
                try { Directory.Delete(dest, recursive: true); } catch { }
            }
        }
    }
}
