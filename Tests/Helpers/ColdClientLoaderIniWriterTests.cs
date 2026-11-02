using System.IO;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Tests.TestSupport;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class ColdClientLoaderIniWriterTests
    {
        [Fact]
        public void WriteLoaderIni_writes_stem_ini_with_required_sections()
        {
            string dir = Path.Combine(Path.GetTempPath(), "sge-coldclient-ini-" + Path.GetRandomFileName());
            try
            {
                Directory.CreateDirectory(dir);
                string loader = Path.Combine(dir, "steamclient_loader_x64.exe");
                File.WriteAllBytes(loader, new byte[] { 0x4D, 0x5A });

                string iniPath = ColdClientLoaderIniWriter.WriteLoaderIni(
                    loader,
                    570,
                    @"D:\Games\game.exe",
                    @"D:\Games",
                    "-windowed",
                    @"C:\games\570\steamclient.dll",
                    @"C:\games\570\steamclient64.dll",
                    forceInjectGameOverlayRenderer: true);

                Assert.Equal(Path.Combine(dir, "steamclient_loader_x64.ini"), iniPath);
                string text = File.ReadAllText(iniPath);
                Assert.Contains("Auto-managed by SmartGoldbergEmu", text);
                Assert.Contains("[SteamClient]", text);
                Assert.Contains("AppId=570", text);
                Assert.Contains(@"Exe=D:\Games\game.exe", text);
                Assert.Contains("ForceInjectSteamClient=1", text);
                Assert.Contains("ForceInjectGameOverlayRenderer=1", text);
                Assert.Contains("DllsToInjectFolder=", text);
                Assert.DoesNotContain("DllsToInjectFolder=C:", text);
                Assert.Contains("[Persistence]", text);
                Assert.Contains("Mode=0", text);
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }

        [Fact]
        public void WriteLoaderIni_writes_dlls_to_inject_folder_when_provided()
        {
            string dir = Path.Combine(Path.GetTempPath(), "sge-coldclient-ini-inject-" + Path.GetRandomFileName());
            try
            {
                Directory.CreateDirectory(dir);
                string loader = Path.Combine(dir, "steamclient_loader_x86.exe");
                File.WriteAllBytes(loader, new byte[] { 0x4D, 0x5A });
                string injectFolder = Path.Combine(dir, "extra_dlls");

                string iniPath = ColdClientLoaderIniWriter.WriteLoaderIni(
                    loader,
                    570,
                    @"D:\Games\game.exe",
                    @"D:\Games",
                    string.Empty,
                    @"C:\games\570\steamclient.dll",
                    @"C:\games\570\steamclient64.dll",
                    forceInjectGameOverlayRenderer: false,
                    dllsToInjectFolder: injectFolder);

                string text = File.ReadAllText(iniPath);
                Assert.Contains("DllsToInjectFolder=" + injectFolder, text);
                Assert.Contains("ForceInjectGameOverlayRenderer=0", text);
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }

        [Fact]
        public void ResolveStubDrmInjectFolder_empty_without_steamstub()
        {
            Assert.Equal(string.Empty, ColdClientLoaderIniWriter.ResolveStubDrmInjectFolder(hasSteamStub: false));
        }

        [Fact]
        public void ResolveStubDrmInjectFolder_returns_coldclient_extra_dlls_when_steamstub_and_samples_present()
        {
            using (var harness = new LaunchDeployTestHarness())
            {
                harness.StageColdClientLoaderStubs();

                string folder = ColdClientLoaderIniWriter.ResolveStubDrmInjectFolder(hasSteamStub: true);
                Assert.False(string.IsNullOrEmpty(folder));
                Assert.Equal(
                    Path.GetFullPath(PathConstants.GoldbergColdClientExtraDllsDirectory),
                    folder);
            }
        }

        [Fact]
        public void ResolveStubDrmInjectFolder_empty_when_steamstub_but_samples_missing()
        {
            using (var harness = new LaunchDeployTestHarness())
            {
                Directory.CreateDirectory(PathConstants.GoldbergColdClientExtraDllsDirectory);
                Assert.Equal(string.Empty, ColdClientLoaderIniWriter.ResolveStubDrmInjectFolder(hasSteamStub: true));
            }
        }
    }
}
