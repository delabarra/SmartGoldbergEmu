using System.IO;
using SmartGoldbergEmu.Helpers;
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
                Assert.Contains("[SteamClient]", text);
                Assert.Contains("AppId=570", text);
                Assert.Contains(@"Exe=D:\Games\game.exe", text);
                Assert.Contains("ForceInjectSteamClient=1", text);
                Assert.Contains("ForceInjectGameOverlayRenderer=1", text);
                Assert.Contains("DllsToInjectFolder=", text);
                Assert.Contains("[Persistence]", text);
                Assert.Contains("Mode=0", text);
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }
    }
}
