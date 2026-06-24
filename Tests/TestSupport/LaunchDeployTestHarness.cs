using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using SmartGoldbergEmu.Tests.Fakes;

namespace SmartGoldbergEmu.Tests.TestSupport
{
    // Temp game trees, Goldberg staging, and launch service wiring for deploy/cleanup tests.
    internal sealed class LaunchDeployTestHarness : IDisposable
    {
        public const ulong DefaultTestAppId = 999001;
        public const byte OriginalFileMarker = 0xA1;
        public const byte GoldbergFileMarker = 0xB2;
        public const byte StagedExtraDllMarker = 0xC3;

        private readonly System.Collections.Generic.List<string> _stagedGoldbergPaths =
            new System.Collections.Generic.List<string>();

        public string GamesRoot { get; }
        public LaunchSessionCleanupService CleanupService { get; }
        public GameLaunchService LaunchService { get; }

        public LaunchDeployTestHarness()
            : this(DefaultTestAppId)
        {
        }

        public LaunchDeployTestHarness(ulong appId)
        {
            GamesRoot = TestFileHelper.CreateTempDirectory("sge-launch-games-");
            var configService = new EmulatorConfigService(GamesRoot, Path.Combine(GamesRoot, "global.cfg"));
            CleanupService = new LaunchSessionCleanupService(new NullLogService(), configService);
            LaunchService = new GameLaunchService(new NullLogService(), configService, CleanupService);
            EnsureGameLibrarySteamSettings(appId);
        }

        public string CreateGameInstall(string gameFolder, out string executablePath)
        {
            Directory.CreateDirectory(gameFolder);
            string systemCmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            executablePath = Path.Combine(gameFolder, "testgame.exe");
            File.Copy(systemCmd, executablePath, overwrite: true);
            return gameFolder;
        }

        public void EnsureGameLibrarySteamSettings(ulong appId)
        {
            string settingsPath = PathConstants.GetGameSteamSettingsPath(appId);
            Directory.CreateDirectory(settingsPath);
            string appIdFile = Path.Combine(settingsPath, PathConstants.SteamAppIdFileName);
            if (!File.Exists(appIdFile))
                File.WriteAllText(appIdFile, appId.ToString());
        }

        public void WriteMarkerFile(string path, byte marker)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, new[] { marker });
        }

        public byte ReadMarkerFile(string path)
        {
            return File.ReadAllBytes(path)[0];
        }

        public void StageGoldbergSteamDll(byte marker = GoldbergFileMarker)
        {
            string path = PathConstants.CombineGoldbergSteamDllPath();
            WriteMarkerFile(path, marker);
            TrackGoldbergPath(path);
        }

        public void StageGoldbergExperimental(bool useX64, byte apiMarker = GoldbergFileMarker, byte clientMarker = GoldbergFileMarker)
        {
            string apiPath = PathConstants.CombineGoldbergExperimentalSteamApiPath(useX64);
            string clientPath = PathConstants.CombineGoldbergExperimentalSteamClientPath(useX64);
            WriteMarkerFile(apiPath, apiMarker);
            WriteMarkerFile(clientPath, clientMarker);
            TrackGoldbergPath(apiPath);
            TrackGoldbergPath(clientPath);
        }

        public void StageGoldbergExtraDll(bool useX64)
        {
            string folder = PathConstants.GoldbergSteamClientExtraDllsDirectory;
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, useX64 ? "plugin_x64.dll" : "plugin_x32.dll");
            WriteMarkerFile(path, StagedExtraDllMarker);
            TrackGoldbergPath(path);
        }

        public GameConfig CreateGameConfig(
            ulong appId,
            string gameFolder,
            string executablePath,
            GoldbergLaunchMode launchMode,
            string parameters = "/c ping 127.0.0.1 -n 6 > nul")
        {
            return new GameConfig
            {
                AppId = appId,
                AppName = "Launch Deploy Test",
                StartFolder = gameFolder,
                Path = Path.GetFileName(executablePath),
                Parameters = parameters,
                LaunchMode = launchMode,
                GameGuid = Guid.Empty,
            };
        }

        public static bool WaitForProcessExit(Process process, int timeoutMs)
        {
            if (process == null)
                return true;

            try
            {
                if (process.HasExited)
                    return true;
                return process.WaitForExit(timeoutMs);
            }
            catch
            {
                return true;
            }
        }

        public static bool WaitUntil(Func<bool> condition, int timeoutMs, int pollMs = 50)
        {
            var deadline = Environment.TickCount + timeoutMs;
            while (Environment.TickCount < deadline)
            {
                if (condition())
                    return true;
                Thread.Sleep(pollMs);
            }

            return condition();
        }

        public void TrackGoldbergPath(string path)
        {
            if (!string.IsNullOrEmpty(path))
                _stagedGoldbergPaths.Add(path);
        }

        public void Dispose()
        {
            GoldbergTestLayoutHelper.CleanupStagedFiles(_stagedGoldbergPaths);
            _stagedGoldbergPaths.Clear();

            try
            {
                if (!string.IsNullOrEmpty(GamesRoot) && Directory.Exists(GamesRoot))
                    Directory.Delete(GamesRoot, recursive: true);
            }
            catch
            {
            }

            string manifestPath = PathConstants.CombineLaunchSessionManifestPath(DefaultTestAppId);
            try
            {
                if (File.Exists(manifestPath))
                    File.Delete(manifestPath);
            }
            catch
            {
            }
        }
    }
}
