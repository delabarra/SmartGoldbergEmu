using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    public class GameDataService
    {
        private readonly string _gamesDirectory;
        private readonly string _gamesIniPath;

        public GameDataService() : this(null)
        {
        }

        public GameDataService(string gamesDirectory = null)
        {
            _gamesDirectory = gamesDirectory ?? PathConstants.GamesDirectory;
            _gamesIniPath = PathConstants.CombineGamesIniPath(_gamesDirectory);
        }

        public List<GameConfig> LoadGameLibrary()
        {
            try
            {
                if (!File.Exists(_gamesIniPath))
                    return new List<GameConfig>();

                var games = new List<GameConfig>();
                GameConfig current = null;

                foreach (var line in File.ReadAllLines(_gamesIniPath))
                {
                    var t = line.Trim();
                    if (t.Length == 0 || t[0] == ';' || t[0] == '#')
                        continue;

                    if (t.StartsWith("[") && t.EndsWith("]"))
                    {
                        if (current != null)
                            games.Add(current);
                        current = new GameConfig();
                        continue;
                    }

                    if (current == null || !t.Contains("="))
                        continue;

                    var parts = t.Split(new[] { '=' }, 2);
                    if (parts.Length != 2)
                        continue;

                    var key = parts[0].Trim().ToLowerInvariant();
                    var value = parts[1].Trim();

                    switch (key)
                    {
                        case GamesIniKeyNames.AppName:
                            current.AppName = value;
                            break;
                        case GamesIniKeyNames.AppId:
                            if (ulong.TryParse(value, out ulong appId))
                                current.AppId = appId;
                            break;
                        case GamesIniKeyNames.StartFolder:
                            current.StartFolder = value;
                            break;
                        case GamesIniKeyNames.Path:
                            current.Path = value;
                            break;
                        case GamesIniKeyNames.Parameters:
                            current.Parameters = value;
                            break;
                        case GamesIniKeyNames.WorkingDirectory:
                            current.WorkingDirectory = value;
                            break;
                        case GamesIniKeyNames.CustomIcon:
                            current.CustomIcon = value;
                            break;
                        case GamesIniKeyNames.GameGuid:
                            if (Guid.TryParse(value, out Guid guid))
                                current.GameGuid = guid;
                            break;
                        case GamesIniKeyNames.GoldbergLaunchMode:
                            current.LaunchMode = ParseGoldbergLaunchModeFromIni(value);
                            break;
                    }
                }

                if (current != null)
                    games.Add(current);

                return games;
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to load game library: {ex.Message}");
                return new List<GameConfig>();
            }
        }

        public ValidationResult SaveGameLibrary(List<GameConfig> games)
        {
            try
            {
                if (games == null)
                    return ValidationResult.Failure("Games list cannot be null");

                foreach (var game in games)
                {
                    var validation = ValidateGameConfig(game);
                    if (!validation.IsValid)
                    {
                        ServiceLocator.LogService?.LogWarning($"Game library save skipped: {validation.ErrorMessage}");
                        return ValidationResult.Failure($"Invalid game configuration: {validation.ErrorMessage}");
                    }
                }

                Directory.CreateDirectory(_gamesDirectory);

                var lines = new List<string>
                {
                    "; SmartGoldbergEmu Game Library",
                    "; This file contains game identity information only",
                    "; Emulation settings are stored in individual game folders",
                    ""
                };

                for (int i = 0; i < games.Count; i++)
                {
                    var game = games[i];
                    lines.Add($"[{GamesIniKeyNames.GameSectionPrefix}_{i}]");
                    lines.Add($"{GamesIniKeyNames.AppNameWrite}={game.AppName}");
                    lines.Add($"{GamesIniKeyNames.AppIdWrite}={game.AppId}");
                    lines.Add($"{GamesIniKeyNames.StartFolderWrite}={game.StartFolder}");
                    lines.Add($"{GamesIniKeyNames.PathWrite}={game.Path}");
                    lines.Add($"{GamesIniKeyNames.ParametersWrite}={game.Parameters}");
                    lines.Add($"{GamesIniKeyNames.WorkingDirectoryWrite}={game.WorkingDirectory}");
                    if (!string.IsNullOrEmpty(game.CustomIcon))
                        lines.Add($"{GamesIniKeyNames.CustomIconWrite}={game.CustomIcon}");
                    lines.Add($"{GamesIniKeyNames.GameGuidWrite}={game.GameGuid}");
                    if (game.LaunchMode != GoldbergLaunchMode.SteamClient)
                        lines.Add($"{GamesIniKeyNames.GoldbergLaunchModeWrite}={game.LaunchMode}");
                    lines.Add("");
                }

                File.WriteAllLines(_gamesIniPath, lines);
                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogError("Failed to save game library", ex);
                return ValidationResult.Failure($"Failed to save game library: {ex.Message}");
            }
        }

        public ValidationResult AddGame(GameConfig game)
        {
            try
            {
                if (game == null)
                {
                    ServiceLocator.LogService?.LogWarning("Add game rejected: null configuration");
                    return ValidationResult.Failure("Game cannot be null");
                }

                var validation = ValidateGameConfig(game);
                if (!validation.IsValid)
                {
                    ServiceLocator.LogService?.LogWarning($"Add game validation failed: {validation.ErrorMessage}");
                    return validation;
                }

                var games = LoadGameLibrary();

                if (games.Any(g => g.AppId == game.AppId && !string.IsNullOrWhiteSpace(g.Path) &&
                    GameFolderPathHelper.ExecutablesReferToSameGame(g, game)))
                {
                    ServiceLocator.LogService?.LogWarning("Add game rejected: same AppId, path, and executable as an existing entry");
                    return ValidationResult.Failure($"Game with same AppId, path and file already exists");
                }

                if (games.Any(g => g.GameGuid == game.GameGuid))
                {
                    ServiceLocator.LogService?.LogWarning($"Add game rejected: GUID {game.GameGuid} already exists");
                    return ValidationResult.Failure($"Game with GUID {game.GameGuid} already exists");
                }

                games.Add(game);
                return SaveGameLibrary(games);
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogError("Failed to add game", ex);
                return ValidationResult.Failure($"Failed to add game: {ex.Message}");
            }
        }

        public ValidationResult RemoveGame(Guid gameGuid, bool deleteFiles = true)
        {
            try
            {
                var games = LoadGameLibrary();
                var gameToRemove = games.FirstOrDefault(g => g.GameGuid == gameGuid);

                if (gameToRemove == null)
                    return ValidationResult.Failure($"Game with GUID {gameGuid} not found");

                if (deleteFiles)
                {
                    var gameDirectory = PathConstants.CombineGameFolder(_gamesDirectory, gameToRemove.AppId.ToString());
                    if (Directory.Exists(gameDirectory))
                    {
                        try
                        {
                            Directory.Delete(gameDirectory, true);
                        }
                        catch (Exception dirEx)
                        {
                            return ValidationResult.Failure($"Failed to delete game directory: {dirEx.Message}");
                        }
                    }
                }

                games.Remove(gameToRemove);
                return SaveGameLibrary(games);
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Failed to remove game: {ex.Message}");
            }
        }

        public ValidationResult UpdateGame(GameConfig game)
        {
            try
            {
                if (game == null)
                    return ValidationResult.Failure("Game cannot be null");

                var validation = ValidateGameConfig(game);
                if (!validation.IsValid)
                    return validation;

                var games = LoadGameLibrary();
                var index = games.FindIndex(g => g.GameGuid == game.GameGuid);

                if (index == -1)
                    return ValidationResult.Failure($"Game with GUID {game.GameGuid} not found");

                games[index] = game;
                return SaveGameLibrary(games);
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Failed to update game: {ex.Message}");
            }
        }

        public GameConfig GetGame(Guid gameGuid)
        {
            return TryQuery(() => LoadGameLibrary().FirstOrDefault(g => g.GameGuid == gameGuid), "Failed to get game");
        }

        public GameConfig GetGameByAppIdAndPath(ulong appId, string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
                return GetGameByAppId(appId);

            return TryQuery(() =>
            {
                var candidate = new GameConfig { Path = executablePath.Trim(), StartFolder = string.Empty };
                return LoadGameLibrary().FirstOrDefault(g => g.AppId == appId &&
                    !string.IsNullOrWhiteSpace(g.Path) &&
                    GameFolderPathHelper.ExecutablesReferToSameGame(g, candidate));
            }, "Failed to get game by AppId and path");
        }

        public GameConfig GetGameByAppId(ulong appId)
        {
            return TryQuery(() => LoadGameLibrary().FirstOrDefault(g => g.AppId == appId), "Failed to get game by AppId");
        }

        public List<GameConfig> GetAllGames()
        {
            return LoadGameLibrary();
        }

        public ValidationResult ValidateGameConfig(GameConfig game)
        {
            if (game == null)
                return ValidationResult.Failure("Game cannot be null");

            if (game.AppId == 0)
                return ValidationResult.Failure("App ID must be a valid non-zero Steam App ID");

            if (string.IsNullOrWhiteSpace(game.AppName))
                return ValidationResult.Failure("App name cannot be null or empty");

            if (string.IsNullOrWhiteSpace(game.Path))
                return ValidationResult.Failure("Game path cannot be null or empty");

            if (game.GameGuid == Guid.Empty)
                return ValidationResult.Failure("Game GUID cannot be empty");

            return ValidationResult.Success();
        }

        public Guid GenerateGameGuid()
        {
            var games = LoadGameLibrary();
            Guid newGuid;
            do
            {
                newGuid = Guid.NewGuid();
            } while (games.Any(g => g.GameGuid == newGuid));

            return newGuid;
        }

        public bool GameExists(ulong appId)
        {
            return GetGameByAppId(appId) != null;
        }

        public int GetGameCount()
        {
            return LoadGameLibrary().Count;
        }

        public GameConfig FindDuplicateByExecutable(GameConfig candidate)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.Path))
                return null;

            return TryQuery(() =>
            {
                foreach (var g in LoadGameLibrary())
                {
                    if (candidate.GameGuid != Guid.Empty && g.GameGuid == candidate.GameGuid)
                        continue;
                    if (!string.IsNullOrWhiteSpace(g.Path) && GameFolderPathHelper.ExecutablesReferToSameGame(g, candidate))
                        return g;
                }
                return null;
            }, "Failed to find duplicate executable");
        }

        public GameConfig CheckDuplicatePath(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
                return null;

            return FindDuplicateByExecutable(new GameConfig
            {
                Path = executablePath.Trim(),
                StartFolder = string.Empty
            });
        }

        public bool SearchAppIdInFolders(ulong appId)
        {
            try
            {
                if (!Directory.Exists(_gamesDirectory))
                    return false;

                foreach (var folder in Directory.GetDirectories(_gamesDirectory))
                {
                    if (GameFolderMatchesAppId(folder, appId))
                        return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to search appid in folders: {ex.Message}");
                return false;
            }
        }

        private static bool ParseIniBool(string value)
        {
            return value == "1"
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        private static bool SteamAppIdFileMatches(string path, ulong appId)
        {
            if (!File.Exists(path))
                return false;
            var content = File.ReadAllText(path).Trim();
            return ulong.TryParse(content, out ulong found) && found == appId;
        }

        private static bool GameFolderMatchesAppId(string gameFolder, ulong appId)
        {
            var name = Path.GetFileName(gameFolder);
            if (ulong.TryParse(name, out ulong folderAppId) && folderAppId == appId)
                return true;
            return SteamAppIdFileMatches(Path.Combine(gameFolder, PathConstants.SteamAppIdFileName), appId)
                || SteamAppIdFileMatches(Path.Combine(gameFolder, PathConstants.SteamSettingsFolderName, PathConstants.SteamAppIdFileName), appId);
        }

        private GameConfig TryQuery(Func<GameConfig> query, string debugPrefix)
        {
            try
            {
                return query();
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"{debugPrefix}: {ex.Message}");
                return null;
            }
        }

        private static GoldbergLaunchMode ParseGoldbergLaunchModeFromIni(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return GoldbergLaunchMode.SteamClient;
            if (value.Equals("StandardSteamApi", StringComparison.OrdinalIgnoreCase)
                || value.Equals("steamapi", StringComparison.OrdinalIgnoreCase)
                || value.Equals("regular", StringComparison.OrdinalIgnoreCase)
                || value == "1")
            {
                return GoldbergLaunchMode.StandardSteamApi;
            }

            if (value.Equals("SteamDllBesideExe", StringComparison.OrdinalIgnoreCase)
                || value.Equals("steamdll", StringComparison.OrdinalIgnoreCase)
                || value == "2")
            {
                return GoldbergLaunchMode.SteamDllBesideExe;
            }

            if (value.Equals("NoEmulation", StringComparison.OrdinalIgnoreCase)
                || value.Equals("noemulation", StringComparison.OrdinalIgnoreCase)
                || value.Equals("no emulation", StringComparison.OrdinalIgnoreCase)
                || value == "3")
            {
                return GoldbergLaunchMode.NoEmulation;
            }

            if (Enum.TryParse(value, true, out GoldbergLaunchMode parsed))
                return parsed;
            return GoldbergLaunchMode.SteamClient;
        }
    }
}
