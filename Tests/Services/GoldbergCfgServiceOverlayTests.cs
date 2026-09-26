using AppDataKit;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using SmartGoldbergEmu.Tests.TestSupport;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class GoldbergCfgServiceOverlayTests
    {
        [Fact]
        public void ApplyOverlayToIni_writes_enable_screenshot_when_disabled()
        {
            using (var scope = new ServiceLocatorTestScope("sge-goldberg-overlay-"))
            {
                string globalRoot = TestFileHelper.CreateTempDirectory("sge-goldberg-global-");
                try
                {
                    var cfgService = new GoldbergCfgService(globalRoot);
                    var iniFile = new IniFile();
                    cfgService.ApplyOverlayToIni(iniFile, new OverlaySettings { EnableScreenshot = false });

                    string value = ServiceLocator.IniFileService.GetValue(iniFile, "overlay::general", "enable_screenshot");
                    Assert.Equal("0", value);
                }
                finally
                {
                    try { System.IO.Directory.Delete(globalRoot, recursive: true); } catch { }
                }
            }
        }
    }
}
