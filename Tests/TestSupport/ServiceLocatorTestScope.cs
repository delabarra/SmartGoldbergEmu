using System;
using System.IO;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Tests.TestSupport
{
    internal sealed class ServiceLocatorTestScope : IDisposable
    {
        public AppDataService AppDataService { get; }
        public string DataDirectory { get; }
        public string ConfigFilePath { get; }

        public ServiceLocatorTestScope(string iniPrefix = "sge-slocator-")
        {
            DataDirectory = TestFileHelper.CreateTempDirectory(iniPrefix);
            ConfigFilePath = Path.Combine(DataDirectory, "settings.ini");
            AppDataService = new AppDataService(
                ConfigFilePath,
                Path.Combine(DataDirectory, "settings"),
                Path.Combine(DataDirectory, "ui_settings.ini"),
                new IniFileService());
            ServiceLocator.SetAppDataServiceForTests(AppDataService);
        }

        public void WriteConfig(string content)
        {
            File.WriteAllText(ConfigFilePath, content);
        }

        public void WriteEmulatorConfig(GoldbergForkSource fork, string goldbergVersion = null)
        {
            string ini =
                "[emulator]\r\n" +
                "goldberg_fork=" + GoldbergForkSourceIni.ToStorageValue(fork) + "\r\n";
            if (goldbergVersion != null)
                ini += "goldberg_version=" + goldbergVersion + "\r\n";
            WriteConfig(ini);
        }

        public void Dispose()
        {
            ServiceLocator.ClearAppDataServiceForTests();
            try { Directory.Delete(DataDirectory, recursive: true); } catch { }
        }
    }
}
