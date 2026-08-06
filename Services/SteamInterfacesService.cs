using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Validation;

namespace SmartGoldbergEmu.Services
{
    // Builds Goldberg steam_interfaces.txt (see gbe_fork tools/generate_interfaces and steam_interfaces.EXAMPLE.txt).
    // One original steam_api beside the game exe — never a union of every DLL under StartFolder.
    public class SteamInterfacesService
    {
        // Pattern set matches gbe_fork tools/generate_interfaces/generate_interfaces.cpp (regex over full DLL bytes).
        private static readonly Regex[] GoldbergInterfacePatterns =
        {
            new Regex(@"STEAMAPPLIST_INTERFACE_VERSION\d+", RegexOptions.CultureInvariant),
            new Regex(@"STEAMAPPS_INTERFACE_VERSION\d+", RegexOptions.CultureInvariant),
            new Regex(@"STEAMAPPTICKET_INTERFACE_VERSION\d+", RegexOptions.CultureInvariant),
            new Regex(@"STEAMCONTROLLER_INTERFACE_VERSION", RegexOptions.CultureInvariant),
            new Regex(@"STEAMHTMLSURFACE_INTERFACE_VERSION_\d+", RegexOptions.CultureInvariant),
            new Regex(@"STEAMHTTP_INTERFACE_VERSION\d+", RegexOptions.CultureInvariant),
            new Regex(@"STEAMINVENTORY_INTERFACE_V\d+", RegexOptions.CultureInvariant),
            new Regex(@"STEAMMUSIC_INTERFACE_VERSION\d+", RegexOptions.CultureInvariant),
            new Regex(@"STEAMMUSICREMOTE_INTERFACE_VERSION\d+", RegexOptions.CultureInvariant),
            new Regex(@"STEAMPARENTALSETTINGS_INTERFACE_VERSION\d+", RegexOptions.CultureInvariant),
            new Regex(@"STEAMREMOTEPLAY_INTERFACE_VERSION\d+", RegexOptions.CultureInvariant),
            new Regex(@"STEAMREMOTESTORAGE_INTERFACE_VERSION\d+", RegexOptions.CultureInvariant),
            new Regex(@"STEAMSCREENSHOTS_INTERFACE_VERSION\d+", RegexOptions.CultureInvariant),
            new Regex(@"STEAMTIMELINE_INTERFACE_V\d+", RegexOptions.CultureInvariant),
            new Regex(@"STEAMUGC_INTERFACE_VERSION\d+", RegexOptions.CultureInvariant),
            new Regex(@"STEAMUNIFIEDMESSAGES_INTERFACE_VERSION\d+", RegexOptions.CultureInvariant),
            new Regex(@"STEAMUSERSTATS_INTERFACE_VERSION\d+", RegexOptions.CultureInvariant),
            new Regex(@"STEAMVIDEO_INTERFACE_V\d+", RegexOptions.CultureInvariant),
            new Regex(@"SteamApps\d+", RegexOptions.CultureInvariant),
            new Regex(@"SteamClient\d+", RegexOptions.CultureInvariant),
            new Regex(@"SteamController\d+", RegexOptions.CultureInvariant),
            new Regex(@"SteamFriends\d+", RegexOptions.CultureInvariant),
            new Regex(@"SteamGameCoordinator\d+", RegexOptions.CultureInvariant),
            new Regex(@"SteamGameServer\d+", RegexOptions.CultureInvariant),
            new Regex(@"SteamGameServerStats\d+", RegexOptions.CultureInvariant),
            new Regex(@"SteamInput\d+", RegexOptions.CultureInvariant),
            new Regex(@"SteamMasterServerUpdater\d+", RegexOptions.CultureInvariant),
            new Regex(@"SteamMatchGameSearch\d+", RegexOptions.CultureInvariant),
            new Regex(@"SteamMatchMaking\d+", RegexOptions.CultureInvariant),
            new Regex(@"SteamMatchMakingServers\d+", RegexOptions.CultureInvariant),
            new Regex(@"SteamNetworking\d+", RegexOptions.CultureInvariant),
            new Regex(@"SteamNetworkingMessages\d+", RegexOptions.CultureInvariant),
            new Regex(@"SteamNetworkingSockets\d+", RegexOptions.CultureInvariant),
            new Regex(@"SteamNetworkingUtils\d+", RegexOptions.CultureInvariant),
            new Regex(@"SteamParties\d+", RegexOptions.CultureInvariant),
            new Regex(@"SteamUser\d+", RegexOptions.CultureInvariant),
            new Regex(@"SteamUtils\d+", RegexOptions.CultureInvariant)
        };

        private static readonly Encoding BinaryInterfaceEncoding = Encoding.GetEncoding(28591);

        private readonly ILogService _logger;

        public SteamInterfacesService() : this(null)
        {
        }

        public SteamInterfacesService(ILogService logger)
        {
            _logger = logger ?? ServiceLocator.LogService;
        }

        public string GetSteamSettingsFilePath(string steamSettingsPath)
        {
            return Path.Combine(steamSettingsPath ?? string.Empty, PathConstants.GoldbergSteamInterfacesFileName);
        }

        public string FindExistingSourceFile(GameConfig gameConfig)
        {
            if (gameConfig == null)
                return null;

            var candidates = new List<string>();
            string startFolder = gameConfig.StartFolder ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(startFolder) && Directory.Exists(startFolder))
            {
                candidates.Add(Path.Combine(startFolder, PathConstants.GoldbergSteamInterfacesFileName));
                candidates.Add(Path.Combine(startFolder, PathConstants.GoldbergSteamInterfacesExampleFileName));
                candidates.Add(Path.Combine(startFolder, PathConstants.SteamSettingsFolderName, PathConstants.GoldbergSteamInterfacesFileName));
            }

            foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        /// <summary>
        /// Returns the single preferred Valve steam_api path for interface generation (Path-adjacent), or empty.
        /// </summary>
        public IReadOnlyList<string> CollectSteamApiDllPathsForGame(GameConfig gameConfig)
        {
            var scanPaths = new List<string>();
            if (!TryResolvePreferredSteamApiSource(gameConfig, preferredExePath: null, out string sourcePath))
                return scanPaths;

            scanPaths.Add(sourcePath);
            return scanPaths;
        }

        public IReadOnlyList<string> ExtractInterfaceNamesFromGame(GameConfig gameConfig)
        {
            return ExtractInterfaceNamesFromSteamApiDlls(CollectSteamApiDllPathsForGame(gameConfig));
        }

        public IReadOnlyList<string> ExtractInterfaceNamesFromSteamApiDlls(IEnumerable<string> dllPaths)
        {
            var results = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (dllPaths == null)
                return results;

            foreach (string path in dllPaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    continue;

                foreach (string entry in ExtractInterfaceNamesFromBinary(path))
                {
                    if (seen.Add(entry))
                        results.Add(entry);
                }
            }

            return results;
        }

        public IReadOnlyList<string> ExtractInterfaceNamesFromBinary(string binaryPath)
        {
            if (string.IsNullOrWhiteSpace(binaryPath) || !File.Exists(binaryPath))
                return new List<string>();

            byte[] data = File.ReadAllBytes(binaryPath);
            return ExtractInterfaceNamesFromBinaryData(data);
        }

        private static IReadOnlyList<string> ExtractInterfaceNamesFromBinaryData(byte[] data)
        {
            var results = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (data == null || data.Length == 0)
                return results;

            string content = BinaryInterfaceEncoding.GetString(data);
            foreach (Regex pattern in GoldbergInterfacePatterns)
                AddPatternMatches(content, pattern, results, seen);

            return results;
        }

        private static void AddPatternMatches(string content, Regex pattern, IList<string> results, HashSet<string> seen)
        {
            var matches = new List<string>();
            foreach (Match match in pattern.Matches(content))
            {
                string value = match.Value;
                if (!matches.Contains(value))
                    matches.Add(value);
            }

            foreach (string value in matches)
            {
                if (seen.Add(value))
                    results.Add(value);
            }
        }

        public bool TryWriteSteamInterfacesFile(string steamSettingsPath, IReadOnlyList<string> entries, bool overwriteExisting)
        {
            if (string.IsNullOrWhiteSpace(steamSettingsPath) || entries == null || entries.Count == 0)
                return false;

            string targetPath = GetSteamSettingsFilePath(steamSettingsPath);
            if (!overwriteExisting && File.Exists(targetPath))
                return false;

            try
            {
                Directory.CreateDirectory(steamSettingsPath);
                File.WriteAllLines(targetPath, entries);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to write {PathConstants.GoldbergSteamInterfacesFileName} to {targetPath}", ex);
                return false;
            }
        }

        public bool TryWriteSteamInterfacesFromSteamApi(string steamSettingsPath, string steamApiPath, bool overwrite)
        {
            if (string.IsNullOrWhiteSpace(steamSettingsPath) || string.IsNullOrWhiteSpace(steamApiPath) || !File.Exists(steamApiPath))
                return false;

            string targetPath = GetSteamSettingsFilePath(steamSettingsPath);
            if (!overwrite && File.Exists(targetPath))
                return false;

            try
            {
                IReadOnlyList<string> interfaces = ExtractInterfaceNamesFromBinary(steamApiPath);
                if (interfaces.Count == 0)
                {
                    _logger?.LogWarning(
                        $"No Steam interface strings found in {steamApiPath}.");
                    return false;
                }

                Directory.CreateDirectory(steamSettingsPath);
                File.WriteAllLines(targetPath, interfaces);
                _logger?.LogMessage(
                    $"Wrote {PathConstants.GoldbergSteamInterfacesFileName} ({interfaces.Count} entries) from {steamApiPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed writing {PathConstants.GoldbergSteamInterfacesFileName} from {steamApiPath}", ex);
                return false;
            }
        }

        public bool TryCopySourceFileToSteamSettings(string steamSettingsPath, string sourceFilePath)
        {
            if (string.IsNullOrWhiteSpace(steamSettingsPath) || string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
                return false;

            try
            {
                string content = File.ReadAllText(sourceFilePath);
                if (string.IsNullOrWhiteSpace(content))
                    return false;

                Directory.CreateDirectory(steamSettingsPath);
                File.WriteAllText(GetSteamSettingsFilePath(steamSettingsPath), content.Trim());
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to copy {PathConstants.GoldbergSteamInterfacesFileName} from {sourceFilePath}", ex);
                return false;
            }
        }

        // Creates steam_interfaces.txt when missing: copy from game folder source, else scan one Path-adjacent Valve steam_api.
        public bool TryEnsureSteamInterfacesFile(string steamSettingsPath, GameConfig gameConfig, ref bool anyFileGenerated)
        {
            if (gameConfig == null || gameConfig.AppId == 0 || string.IsNullOrWhiteSpace(steamSettingsPath))
                return false;

            string targetPath = GetSteamSettingsFilePath(steamSettingsPath);
            if (File.Exists(targetPath))
                return true;

            string sourcePath = FindExistingSourceFile(gameConfig);
            if (!string.IsNullOrEmpty(sourcePath) && TryCopySourceFileToSteamSettings(steamSettingsPath, sourcePath))
            {
                anyFileGenerated = true;
                _logger?.LogMessage($"Generated {PathConstants.GoldbergSteamInterfacesFileName} from source file for app {gameConfig.AppId}");
                return true;
            }

            if (File.Exists(targetPath))
                return true;

            return TryWriteFromSteamApiScan(steamSettingsPath, gameConfig, preferredExePath: null, overwrite: false, ref anyFileGenerated);
        }

        public bool TryRegenerateSteamInterfacesFile(GameConfig gameConfig)
        {
            if (gameConfig == null || gameConfig.AppId == 0)
                return false;
            return TryRegenerateSteamInterfacesFile(PathConstants.GetGameSteamSettingsPath(gameConfig.AppId), gameConfig);
        }

        public bool TryRegenerateSteamInterfacesFile(string steamSettingsPath, GameConfig gameConfig)
        {
            if (gameConfig == null || gameConfig.AppId == 0 || string.IsNullOrWhiteSpace(steamSettingsPath))
                return false;

            Directory.CreateDirectory(steamSettingsPath);
            string targetPath = GetSteamSettingsFilePath(steamSettingsPath);
            try
            {
                if (File.Exists(targetPath))
                    File.Delete(targetPath);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"Could not remove existing {PathConstants.GoldbergSteamInterfacesFileName}: {ex.Message}");
                return false;
            }

            bool anyFileGenerated = false;
            TryWriteFromSteamApiScan(steamSettingsPath, gameConfig, preferredExePath: null, overwrite: true, ref anyFileGenerated);
            return anyFileGenerated && File.Exists(targetPath);
        }

        /// <summary>
        /// Overwrites steam_interfaces.txt from the Valve steam_api resolved for <paramref name="launchExePath"/> (or stored Path).
        /// </summary>
        public bool TryRefreshSteamInterfacesForLaunch(
            string steamSettingsPath,
            GameConfig gameConfig,
            string launchExePath)
        {
            if (gameConfig == null || gameConfig.AppId == 0 || string.IsNullOrWhiteSpace(steamSettingsPath))
                return false;

            bool anyFileGenerated = false;
            return TryWriteFromSteamApiScan(
                steamSettingsPath,
                gameConfig,
                preferredExePath: launchExePath,
                overwrite: true,
                ref anyFileGenerated);
        }

        private bool TryWriteFromSteamApiScan(
            string steamSettingsPath,
            GameConfig gameConfig,
            string preferredExePath,
            bool overwrite,
            ref bool anyFileGenerated)
        {
            string targetPath = GetSteamSettingsFilePath(steamSettingsPath);
            if (!overwrite && File.Exists(targetPath))
                return true;

            try
            {
                if (!TryResolvePreferredSteamApiSource(gameConfig, preferredExePath, out string sourceDllPath))
                {
                    _logger?.LogMessage(
                        $"Skipped {PathConstants.GoldbergSteamInterfacesFileName} for app {gameConfig.AppId}: no valid steam_api DLL beside the game executable.");
                    return false;
                }

                if (!TryWriteSteamInterfacesFromSteamApi(steamSettingsPath, sourceDllPath, overwrite: true))
                    return false;

                anyFileGenerated = true;
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed generating {PathConstants.GoldbergSteamInterfacesFileName} from binaries for app {gameConfig.AppId}", ex);
                return false;
            }
        }

        private bool TryResolvePreferredSteamApiSource(
            GameConfig gameConfig,
            string preferredExePath,
            out string sourcePath)
        {
            sourcePath = null;
            if (gameConfig == null)
                return false;

            string startFolder = gameConfig.StartFolder ?? string.Empty;
            string exePath = preferredExePath;
            if (string.IsNullOrWhiteSpace(exePath))
            {
                if (GameFolderPathHelper.TryResolveStoredExecutable(gameConfig, out string storedExe))
                    exePath = storedExe;
                else
                    exePath = gameConfig.Path;
            }

            bool useX64 = true;
            if (!string.IsNullOrWhiteSpace(exePath) &&
                SteamApiValidator.TryDetectExecutableIsX64(exePath, out bool detectedX64))
            {
                useX64 = detectedX64;
            }
            else if (!string.IsNullOrWhiteSpace(exePath))
            {
                // Default x64 when PE cannot be read; still attempt resolve for both if needed below.
                useX64 = true;
            }

            if (SteamApiValidator.TryResolveSteamApiSourceForInterfaces(startFolder, exePath, useX64, out sourcePath))
                return true;

            // If arch guess was wrong (rare), try the opposite bitness once.
            if (SteamApiValidator.TryResolveSteamApiSourceForInterfaces(startFolder, exePath, !useX64, out sourcePath))
                return true;

            return false;
        }
    }
}
