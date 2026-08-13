using System.IO;
using SmartGoldbergEmu.Constants;
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

        [Fact]
        public void TryStageIntoLoadDllsFolder_ignores_subfolder_dlls()
        {
            string source = TestFileHelper.CreateTempDirectory("sge-extra-flat-");
            string dest = TestFileHelper.CreateTempDirectory("sge-extra-flat-dst-");
            try
            {
                File.WriteAllBytes(Path.Combine(source, "root.dll"), new byte[] { 1 });
                string nested = Path.Combine(source, "nested");
                Directory.CreateDirectory(nested);
                File.WriteAllBytes(Path.Combine(nested, "nested.dll"), new byte[] { 2 });

                Assert.True(SteamClientExtraDllsStaging.TryStageIntoLoadDllsFolder(source, dest, useX64: true, out int copied));
                Assert.Equal(1, copied);
                Assert.True(File.Exists(Path.Combine(dest, "root.dll")));
                Assert.False(Directory.Exists(Path.Combine(dest, "nested")));
                Assert.False(File.Exists(Path.Combine(dest, "nested.dll")));
            }
            finally
            {
                try { Directory.Delete(source, recursive: true); } catch { }
                try { Directory.Delete(dest, recursive: true); } catch { }
            }
        }

        [Fact]
        public void TryStageIntoLoadDllsFolder_skips_shipped_steamclient_extra_dlls()
        {
            string source = TestFileHelper.CreateTempDirectory("sge-extra-skip-");
            string dest = TestFileHelper.CreateTempDirectory("sge-extra-skip-dst-");
            try
            {
                File.WriteAllBytes(Path.Combine(source, GoldbergInstallLayout.ShippedSteamClientExtraDll64FileName), new byte[] { 1 });
                File.WriteAllBytes(Path.Combine(source, GoldbergInstallLayout.ShippedSteamClientExtraDll32FileName), new byte[] { 2 });
                File.WriteAllBytes(Path.Combine(source, "plugin_x64.dll"), new byte[] { 3 });

                Assert.True(SteamClientExtraDllsStaging.TryStageIntoLoadDllsFolder(source, dest, useX64: true, out int copied));
                Assert.Equal(1, copied);
                Assert.True(File.Exists(Path.Combine(dest, "plugin_x64.dll")));
                Assert.False(File.Exists(Path.Combine(dest, GoldbergInstallLayout.ShippedSteamClientExtraDll64FileName)));
                Assert.False(File.Exists(Path.Combine(dest, GoldbergInstallLayout.ShippedSteamClientExtraDll32FileName)));
            }
            finally
            {
                try { Directory.Delete(source, recursive: true); } catch { }
                try { Directory.Delete(dest, recursive: true); } catch { }
            }
        }

        [Fact]
        public void TryStageIntoLoadDllsFolder_does_not_create_dest_when_only_shipped_extras_exist()
        {
            string source = TestFileHelper.CreateTempDirectory("sge-extra-empty-");
            string dest = Path.Combine(Path.GetTempPath(), "sge-extra-empty-dst-" + Path.GetRandomFileName());
            try
            {
                File.WriteAllBytes(Path.Combine(source, GoldbergInstallLayout.ShippedSteamClientExtraDll64FileName), new byte[] { 1 });

                Assert.False(SteamClientExtraDllsStaging.TryStageIntoLoadDllsFolder(source, dest, useX64: true, out int copied));
                Assert.Equal(0, copied);
                Assert.False(Directory.Exists(dest));
            }
            finally
            {
                try { Directory.Delete(source, recursive: true); } catch { }
                try { if (Directory.Exists(dest)) Directory.Delete(dest, recursive: true); } catch { }
            }
        }
    }
}
