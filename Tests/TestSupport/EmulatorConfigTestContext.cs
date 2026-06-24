using System;
using System.IO;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using SmartGoldbergEmu.Tests.TestSupport;

namespace SmartGoldbergEmu.Tests.Services
{
    internal sealed class EmulatorConfigTestContext : IDisposable
    {
        public string GamesRoot { get; }
        public string GlobalRoot { get; }
        public GoldbergCfgService CfgService { get; }
        public EmulatorConfigService Service { get; }

        public EmulatorConfigTestContext()
        {
            GamesRoot = TestFileHelper.CreateTempDirectory("sge-emulator-games-");
            GlobalRoot = TestFileHelper.CreateTempDirectory("sge-emulator-global-");
            CfgService = new GoldbergCfgService(GlobalRoot);
            Service = new EmulatorConfigService(GamesRoot, GlobalRoot, CfgService);
        }

        public string GetGameSteamSettingsPath(ulong appId) => Service.GetGameSteamSettingsPath(appId);

        public void WriteGameMainIni(ulong appId, string iniBody)
        {
            string folder = GetGameSteamSettingsPath(appId);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, PathConstants.GoldbergMainIniFileName), iniBody);
        }

        public void WriteGameUserIni(ulong appId, string iniBody)
        {
            string folder = GetGameSteamSettingsPath(appId);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, PathConstants.GoldbergUserIniFileName), iniBody);
        }

        public void WriteGameOverlayIni(ulong appId, string iniBody)
        {
            string folder = GetGameSteamSettingsPath(appId);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, PathConstants.GoldbergOverlayIniFileName), iniBody);
        }

        public void WriteGameAppIni(ulong appId, string iniBody)
        {
            string folder = GetGameSteamSettingsPath(appId);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, PathConstants.GoldbergAppIniFileName), iniBody);
        }

        public void Dispose()
        {
            TryDeleteDirectory(GamesRoot);
            TryDeleteDirectory(GlobalRoot);
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
