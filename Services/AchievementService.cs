using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SmartGoldbergEmu.JsonKit;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu;

namespace SmartGoldbergEmu.Services
{
    public class AchievementService
    {
        private const int FeedbackDisplayDelayMs = 1500;

        private enum SteamAchievementFetchStatus
        {
            InvalidApiKey,
            Success,
            EmptyList,
            ApiError
        }

        private struct SteamAchievementFetchResult
        {
            public SteamAchievementFetchStatus Status;
            public List<CAchievement> Achievements;
            public string ApiErrorCode;
            public Exception Failure;
        }

        // Progress UI differs by caller: main menu (per-image), add-game status bar only, or add-game with step text.
        private enum AchievementProgressMode
        {
            None,
            Menu,
            AddSaveBarOnly,
            AddSaveVerbose
        }

        private static bool ProgressActive(AchievementProgressMode mode) => mode != AchievementProgressMode.None;

        private static bool ProgressIsMenu(AchievementProgressMode mode) => mode == AchievementProgressMode.Menu;

        private static bool ProgressIsAddSave(AchievementProgressMode mode) =>
            mode == AchievementProgressMode.AddSaveBarOnly || mode == AchievementProgressMode.AddSaveVerbose;

        private static bool ProgressBarOnly(AchievementProgressMode mode) =>
            mode == AchievementProgressMode.AddSaveBarOnly;

        private static string GetAddSaveGameDisplayName(GameConfig app)
        {
            if (app == null || string.IsNullOrWhiteSpace(app.AppName))
                return "game";
            return app.AppName.Trim();
        }

        private void ReportAddSaveDownloadingIconsProgress(
            GameConfig app,
            AchievementProgressMode progressMode,
            int current,
            int total)
        {
            if (_taskReportService == null || !ProgressBarOnly(progressMode))
                return;
            _taskReportService.SetMessage(
                AddGameStatusMessages.DownloadingAchievementIcons(GetAddSaveGameDisplayName(app), current, total));
        }

        private readonly ITaskReportService _taskReportService;
        private readonly SteamApiKeyService _steamApiKeyService;
        private readonly string _language;

        private static string GetSteamSettingsFolder(GameConfig app) =>
            PathConstants.GetGameSteamSettingsPath(app.AppId);

        private static string GetAchievementsFilePath(GameConfig app) =>
            Path.Combine(GetSteamSettingsFolder(app), AchievementConstants.AchievementsFileName);

        private static string GetAchievementImagesFolder(GameConfig app) =>
            Path.Combine(GetSteamSettingsFolder(app), AchievementConstants.AchievementImagesFolder);

        private static string _cachedPlaceholderImagePath;
        private static readonly object _placeholderPathLock = new object();

        private static string GetAchievementPlaceholderImagePath()
        {
            if (_cachedPlaceholderImagePath != null)
                return _cachedPlaceholderImagePath;
            lock (_placeholderPathLock)
            {
                if (_cachedPlaceholderImagePath != null)
                    return _cachedPlaceholderImagePath;
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var assemblyDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var paths = new[]
                {
                    Path.Combine(baseDir, PathConstants.LauncherResourcesFolderName, PathConstants.LauncherImagesSubfolderName, PathConstants.LauncherAchievementPlaceholderImageFileName),
                    Path.Combine(assemblyDir ?? "", PathConstants.LauncherResourcesFolderName, PathConstants.LauncherImagesSubfolderName, PathConstants.LauncherAchievementPlaceholderImageFileName),
                    Path.Combine(baseDir, "..", "..", PathConstants.LauncherResourcesFolderName, PathConstants.LauncherImagesSubfolderName, PathConstants.LauncherAchievementPlaceholderImageFileName)
                };
                _cachedPlaceholderImagePath = paths.FirstOrDefault(File.Exists);
                return _cachedPlaceholderImagePath;
            }
        }

        private static string GetAchievementImageRelativePath(string achievementName, bool grayscale) =>
            AchievementConstants.AchievementImagesFolder + "/" + achievementName + (grayscale ? "_gray" : "") + ".jpg";

        private static string JString(JsonObject o, string key)
        {
            var t = o?[key];
            return t != null ? t.ToString() : string.Empty;
        }

        private static string JStringFirst(JsonObject o, params string[] keys)
        {
            if (o == null || keys == null)
                return string.Empty;
            foreach (var key in keys)
            {
                var t = o[key];
                if (t != null)
                    return t.ToString();
            }
            return string.Empty;
        }

        private static bool BoolFromFirstPresentKey(JsonObject o, string[] keys)
        {
            if (o == null)
                return false;
            foreach (var key in keys)
            {
                if (o[key] != null)
                    return ToBool(o[key]);
            }
            return false;
        }

        private static bool AnyPositiveLongKey(JsonObject o, string[] keys)
        {
            if (o == null)
                return false;
            foreach (var key in keys)
            {
                if (o[key] != null && ToLong(o[key]) > 0)
                    return true;
            }
            return false;
        }

        private static bool TrySaveJpegFromBitmapSource(string sourcePath, string destPath)
        {
            try
            {
                using (var bmp = new Bitmap(sourcePath))
                    bmp.Save(destPath, ImageFormat.Jpeg);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TrySavePlaceholderAsJpeg(string destPath)
        {
            var source = GetAchievementPlaceholderImagePath();
            return !string.IsNullOrEmpty(source) && TrySaveJpegFromBitmapSource(source, destPath);
        }

        public AchievementService(
            ITaskReportService feedbackReporter = null,
            SteamApiKeyService steamApiKeyService = null,
            string language = null)
        {
            _taskReportService = feedbackReporter;
            _steamApiKeyService = steamApiKeyService ?? ServiceLocator.SteamApiKeyService;
            _language = language ?? "english";
        }

        private async Task ShowFeedbackAndClearAsync(string message, bool showProgress, TaskReportKind? type = null)
        {
            if (!showProgress || _taskReportService == null)
                return;

            if (type.HasValue)
                _taskReportService.SetMessage(message, type.Value);
            else
                _taskReportService.SetMessage(message);
            _taskReportService.SetProgress(0, 0);
            await Task.Delay(FeedbackDisplayDelayMs);
            ClearAddSaveAchievementProgress();
        }

        private void ClearAddSaveAchievementProgress()
        {
            _taskReportService?.SetMessage(string.Empty);
            _taskReportService?.SetProgress(0, 0);
        }

        private async Task<SteamAchievementFetchResult> FetchAchievementSchemaFromSteamAsync(
            ulong appId,
            bool showRetrieveStatusMessage)
        {
            if (!_steamApiKeyService.TryGetValidFormatKey(out string apiKey))
            {
                return new SteamAchievementFetchResult { Status = SteamAchievementFetchStatus.InvalidApiKey };
            }

            string url = string.Format(AchievementConstants.SteamUserStatsApiUrl, _language, apiKey, appId);
            Program.LogService?.LogDebug($"Fetching achievements from Steam API with language: {_language}");
            if (showRetrieveStatusMessage)
                _taskReportService?.SetMessage("Retrieving achievement data... Please wait.");

            try
            {
                string result = await HttpHelpers.GetStringWithRetryAsync(
                    url,
                    AchievementConstants.HttpRetryCount,
                    AchievementConstants.HttpRetryDelayMs,
                    AchievementConstants.HttpRequestShortTimeout).ConfigureAwait(false);

                if (result.StartsWith("ERROR:", StringComparison.Ordinal))
                {
                    return new SteamAchievementFetchResult
                    {
                        Status = SteamAchievementFetchStatus.ApiError,
                        ApiErrorCode = result.Substring("ERROR:".Length).Trim()
                    };
                }

                CSteamGameSchema schema = JsonConvert.DeserializeObject<CSteamGameSchema>(result);
                List<CAchievement> achievements = schema?.game?.availableGameStats?.achievements;
                if (achievements == null || achievements.Count == 0)
                {
                    return new SteamAchievementFetchResult { Status = SteamAchievementFetchStatus.EmptyList };
                }

                return new SteamAchievementFetchResult
                {
                    Status = SteamAchievementFetchStatus.Success,
                    Achievements = achievements
                };
            }
            catch (HttpRequestException httpEx)
            {
                var errorMsg = $"Web request error: {httpEx.Message}\n\nIs your internet connection working? The AppID might also be incorrect.";
                Program.LogService?.LogError(errorMsg, httpEx);
                return new SteamAchievementFetchResult { Failure = new ConnectionException(errorMsg, url, httpEx) };
            }
            catch (TaskCanceledException)
            {
                return new SteamAchievementFetchResult { Failure = new RequestTimeoutException(url) };
            }
            catch (JsonKitException jsonEx)
            {
                var errorMsg = $"Failed to parse achievement data: {jsonEx.Message}\n\nThe API response format may have changed.";
                Program.LogService?.LogError(errorMsg, jsonEx);
                return new SteamAchievementFetchResult
                {
                    Failure = new AchievementException(errorMsg, appId.ToString(), jsonEx)
                };
            }
            catch (Exception ex)
            {
                var errorMsg = $"Unexpected error: {ex.Message}";
                Program.LogService?.LogError(errorMsg, ex);
                return new SteamAchievementFetchResult
                {
                    Failure = new AchievementException(errorMsg, appId.ToString(), ex)
                };
            }
        }

        // Add-mode preview is in-memory only; strip Steam CDN URLs so UI does not try to load remote icons.
        private static void ClearAchievementIconUrlsForPreview(List<CAchievement> achievements)
        {
            if (achievements == null)
                return;
            foreach (CAchievement achievement in achievements)
            {
                if (achievement == null)
                    continue;
                achievement.icon = string.Empty;
                achievement.icongray = string.Empty;
                achievement.icon_gray = string.Empty;
            }
        }

        private async Task<bool> CompleteDummyGenerationAsync(
            GameConfig app,
            AchievementProgressMode progressMode,
            DummyAchievementReason reason,
            string successMessage,
            string failureMessage)
        {
            bool active = ProgressActive(progressMode);
            bool created = EnsureDummyAchievementAndFiles(app, reason, progressMode);
            if (active && ProgressIsAddSave(progressMode) && created)
            {
                if (ProgressBarOnly(progressMode))
                {
                    ReportAddSaveDownloadingIconsProgress(app, progressMode, 1, 1);
                    _taskReportService?.SetProgress(1, 1);
                    return created;
                }

                _taskReportService?.SetMessage("Generating achievements 1/1");
                _taskReportService?.SetProgress(1, 1);
                ClearAddSaveAchievementProgress();
                return created;
            }

            await ShowFeedbackAndClearAsync(
                created ? successMessage : failureMessage,
                active,
                created ? (TaskReportKind?)null : TaskReportKind.Error);
            return created;
        }

        #region Achievement Checking

        // gbe_fork runtime progress file (%AppData%\GSE Saves\{appId}\achievements.json), not steam_settings schema.
        public HashSet<string> LoadUserUnlockedAchievementKeysFromSaves(ulong appId)
        {
            var unlocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var emulatorConfigService = ServiceLocator.EmulatorConfigService;
                if (emulatorConfigService == null)
                    return unlocked;

                string savesPath = emulatorConfigService.GetGameSavesPath(appId);
                if (string.IsNullOrEmpty(savesPath))
                    return unlocked;

                string path = Path.Combine(savesPath, AchievementConstants.AchievementsFileName);
                if (!File.Exists(path))
                    return unlocked;

                string json;
                // Goldberg may keep this file open while the game is running.
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fs))
                    json = reader.ReadToEnd();

                if (string.IsNullOrWhiteSpace(json))
                    return unlocked;

                var token = JsonValue.Parse(json);
                var root = token as JsonObject;
                if (root == null)
                    return unlocked;

                // Flat { "ACH_NAME": {...} } or wrapped { "achievements": { ... } } depending on emulator build.
                JsonObject data = root;
                JsonValue nested = root["achievements"];
                if (nested != null && nested.Type == JsonValueKind.Object)
                    data = (JsonObject)nested;

                foreach (var prop in data.Properties())
                {
                    if (IsUserSavesAchievementUnlocked(prop.Value))
                        unlocked.Add(prop.Name);
                }
            }
            catch
            {
                return unlocked;
            }

            return unlocked;
        }

        public void ApplyUserUnlockStateFromSaves(List<AchievementPreviewData> achievements, ulong appId)
        {
            if (achievements == null || achievements.Count == 0 || appId == 0)
                return;

            var userUnlockedKeys = LoadUserUnlockedAchievementKeysFromSaves(appId);
            if (userUnlockedKeys == null || userUnlockedKeys.Count == 0)
                return;

            for (int i = 0; i < achievements.Count; i++)
            {
                var achievement = achievements[i];
                if (achievement == null || achievement.IsUnlocked)
                    continue;

                if (!string.IsNullOrEmpty(achievement.Name) && userUnlockedKeys.Contains(achievement.Name))
                {
                    achievement.IsUnlocked = true;
                    continue;
                }

                // Dummy achievements use Goldberg placeholder keys instead of a Steam API name.
                string key3 = string.Format("ACHIEVEMENT_{0:D3}", i);
                if (userUnlockedKeys.Contains(key3))
                {
                    achievement.IsUnlocked = true;
                    continue;
                }

                string keyPlain = "ACHIEVEMENT_" + i.ToString();
                if (userUnlockedKeys.Contains(keyPlain))
                    achievement.IsUnlocked = true;
            }
        }

        public List<AchievementPreviewData> ParseAchievementPreviewData(string achievementsJson)
        {
            var result = new List<AchievementPreviewData>();
            if (string.IsNullOrWhiteSpace(achievementsJson))
                return result;

            try
            {
                var token = JsonValue.Parse(achievementsJson);
                var achievementsArray = token as JsonArray;
                if (achievementsArray == null)
                    return result;

                foreach (var achievementToken in achievementsArray.OfType<JsonObject>())
                {
                    bool isUnlocked = ParseAchievementUnlockedState(achievementToken);
                    bool isHidden = ParseAchievementHiddenState(achievementToken);
                    result.Add(new AchievementPreviewData
                    {
                        Name = JString(achievementToken, "name"),
                        DisplayName = JString(achievementToken, "displayName"),
                        Description = JString(achievementToken, "description"),
                        IconPath = JString(achievementToken, "icon"),
                        GrayIconPath = JStringFirst(achievementToken, "icon_gray", "icongray"),
                        IsUnlocked = isUnlocked,
                        IsHidden = isHidden
                    });
                }
            }
            catch
            {
                return new List<AchievementPreviewData>();
            }

            return result;
        }

        public static AchievementPreviewData ToPreviewData(
            string name,
            string displayName,
            string description,
            string iconPath,
            string grayIconPath,
            bool isUnlocked,
            bool isHidden)
        {
            return new AchievementPreviewData
            {
                Name = name,
                DisplayName = displayName,
                Description = description,
                IconPath = iconPath,
                GrayIconPath = grayIconPath,
                IsUnlocked = isUnlocked,
                IsHidden = isHidden
            };
        }

        public static string GetPreviewTitle(AchievementPreviewData achievement)
        {
            if (achievement == null)
                return "(unnamed)";
            return !string.IsNullOrWhiteSpace(achievement.DisplayName)
                ? achievement.DisplayName
                : (!string.IsNullOrWhiteSpace(achievement.Name) ? achievement.Name : "(unnamed)");
        }

        public Image LoadAchievementPreviewIcon(string steamSettingsPath, string iconRelativePath, Size imageSize)
        {
            if (string.IsNullOrWhiteSpace(iconRelativePath))
                return LoadPlaceholderAchievementIcon(imageSize);

            string fullPath = ResolveAchievementIconPath(steamSettingsPath, iconRelativePath);
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
                return LoadPlaceholderAchievementIcon(imageSize);

            try
            {
                using (var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sourceImage = Image.FromStream(fileStream))
                {
                    return new Bitmap(sourceImage, imageSize);
                }
            }
            catch
            {
                return LoadPlaceholderAchievementIcon(imageSize);
            }
        }

        public Image LoadPlaceholderAchievementIcon(Size imageSize)
        {
            string path = GetAchievementPlaceholderImagePath();
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var sourceImage = Image.FromStream(fileStream))
                    {
                        return new Bitmap(sourceImage, imageSize);
                    }
                }
                catch
                {
                }
            }

            return CreateDimGrayAchievementIcon(imageSize);
        }

        public AchievementPreviewTextResult BuildAchievementPreviewText(AchievementPreviewData achievement, int index, ISet<string> revealedKeys)
        {
            string revealKey = GetAchievementPreviewRevealKey(achievement, index);
            bool isRevealed = revealedKeys != null && revealedKeys.Contains(revealKey);
            string title = GetPreviewTitle(achievement);
            string description = achievement != null ? (achievement.Description ?? string.Empty) : string.Empty;

            if (achievement != null && achievement.IsHidden && !achievement.IsUnlocked)
            {
                title = "Hidden achievement";
                description = "This achievement stays secret until you unlock it.";
            }
            else if (achievement != null && achievement.IsHidden && achievement.IsUnlocked && !isRevealed)
            {
                description = "â–ˆâ–ˆâ–ˆâ–ˆâ–ˆâ–ˆâ–ˆâ–ˆâ–ˆâ–ˆâ–ˆâ–ˆâ–ˆâ–ˆâ–ˆâ–ˆâ–ˆâ–ˆâ–ˆâ–ˆ";
            }

            return new AchievementPreviewTextResult
            {
                RevealKey = revealKey,
                Title = title,
                Description = description,
                Tooltip = BuildAchievementPreviewTooltip(achievement, isRevealed)
            };
        }

        // gbe_fork progress entries use inconsistent field names across versions.
        private static bool IsUserSavesAchievementUnlocked(JsonValue token)
        {
            if (token == null)
                return false;

            if (token.Type == JsonValueKind.Boolean)
                return token.ToBoolean();

            if (token.Type == JsonValueKind.Integer || token.Type == JsonValueKind.Float)
                return token.ToDouble() > 0;

            var obj = token as JsonObject;
            if (obj == null)
                return false;

            if (obj["earned"] != null)
                return ToBool(obj["earned"]);
            if (obj["achieved"] != null)
                return ToBool(obj["achieved"]);
            if (obj["unlocked"] != null)
                return ToBool(obj["unlocked"]);
            if (obj["is_unlocked"] != null)
                return ToBool(obj["is_unlocked"]);

            if (obj["earned_time"] != null && ToLong(obj["earned_time"]) > 0)
                return true;
            if (obj["unlock_time"] != null && ToLong(obj["unlock_time"]) > 0)
                return true;
            if (obj["unlockTime"] != null && ToLong(obj["unlockTime"]) > 0)
                return true;
            if (obj["time_unlocked"] != null && ToLong(obj["time_unlocked"]) > 0)
                return true;

            return false;
        }

        private static bool ParseAchievementUnlockedState(JsonObject achievementToken)
        {
            if (achievementToken == null)
                return false;

            foreach (var key in new[] { "achieved", "unlocked", "earned", "is_unlocked" })
            {
                if (achievementToken[key] != null)
                    return ToBool(achievementToken[key]);
            }

            return AnyPositiveLongKey(achievementToken, new[] { "unlockTime", "unlock_time", "time_unlocked", "state" });
        }

        private static bool ParseAchievementHiddenState(JsonObject achievementToken)
        {
            return BoolFromFirstPresentKey(achievementToken, new[] { "hidden", "is_hidden", "secret", "client_secret" });
        }

        private static string ResolveAchievementIconPath(string steamSettingsPath, string iconRelativePath)
        {
            if (string.IsNullOrWhiteSpace(iconRelativePath))
                return string.Empty;

            if (Path.IsPathRooted(iconRelativePath))
                return iconRelativePath;

            string normalizedPath = iconRelativePath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            return Path.Combine(steamSettingsPath, normalizedPath);
        }

        private static string GetAchievementPreviewRevealKey(AchievementPreviewData achievement, int index)
        {
            if (achievement != null && !string.IsNullOrEmpty(achievement.Name))
                return achievement.Name;
            return "idx:" + index.ToString();
        }

        private static string BuildAchievementPreviewTooltip(AchievementPreviewData achievement, bool isRevealed)
        {
            if (achievement == null || !achievement.IsHidden)
                return string.Empty;

            var tip = new List<string> { "Secret achievement" };
            if (achievement.IsUnlocked)
            {
                if (isRevealed)
                {
                    if (!string.IsNullOrWhiteSpace(achievement.Name))
                        tip.Add("API name: " + achievement.Name);
                }
                else
                {
                    tip.Add("Click or select the row to show the description.");
                }
            }
            else
            {
                tip.Add("Locked — title and description are hidden like in Steam.");
            }

            return string.Join(Environment.NewLine, tip);
        }

        private static Image CreateDimGrayAchievementIcon(Size imageSize)
        {
            var bitmap = new Bitmap(imageSize.Width, imageSize.Height);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.DimGray);
                graphics.DrawRectangle(Pens.Gray, 0, 0, bitmap.Width - 1, bitmap.Height - 1);
            }
            return bitmap;
        }

        private static bool ToBool(JsonValue token)
        {
            if (token == null)
                return false;

            if (token.Type == JsonValueKind.Boolean)
                return token.ToBoolean();

            if (token.Type == JsonValueKind.Integer || token.Type == JsonValueKind.Float)
                return token.ToDouble() > 0;

            var value = token.ToString().Trim();
            return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        private static long ToLong(JsonValue token)
        {
            if (token == null)
                return 0;

            if (token.Type == JsonValueKind.Integer || token.Type == JsonValueKind.Float)
                return token.ToInt64();

            long parsed;
            return long.TryParse(token.ToString().Trim(), out parsed) ? parsed : 0;
        }

        #endregion

        #region Achievement Generation

        public Task<bool> GenerateAchievementsAsync(GameConfig app, bool showProgress = true)
        {
            AchievementProgressMode mode = showProgress ? AchievementProgressMode.Menu : AchievementProgressMode.None;
            return GenerateAchievementsCoreAsync(app, mode);
        }

        private async Task<bool> GenerateAchievementsCoreAsync(GameConfig app, AchievementProgressMode progressMode)
        {
            try
            {
                if (ProgressIsMenu(progressMode) && _taskReportService != null)
                {
                    _taskReportService.SetMessage("Generating achievements... Please wait.");
                    _taskReportService.SetProgress(0, 100);
                }

                string achievementsFile = GetAchievementsFilePath(app);
                string imagesFolder = GetAchievementImagesFolder(app);

                try
                {
                    if (File.Exists(achievementsFile))
                        File.Delete(achievementsFile);
                }
                catch (Exception)
                {
                    if (ProgressIsMenu(progressMode))
                        _taskReportService?.SetMessage("Warning: Could not clean up old files");
                }

                EnsureSteamSettingsFolder(app);

                SteamAchievementFetchResult fetch = await FetchAchievementSchemaFromSteamAsync(
                    app.AppId,
                    showRetrieveStatusMessage: ProgressIsMenu(progressMode)).ConfigureAwait(false);

                if (fetch.Failure != null)
                {
                    if (ProgressIsMenu(progressMode))
                    {
                        if (fetch.Failure is RequestTimeoutException)
                            _taskReportService?.SetMessage("Error: Request timed out");
                        else
                            _taskReportService?.SetMessage(fetch.Failure.Message);
                    }

                    throw fetch.Failure;
                }

                if (fetch.Status == SteamAchievementFetchStatus.InvalidApiKey)
                {
                    if (ProgressIsMenu(progressMode))
                        _taskReportService?.SetMessage("Web API key required - creating placeholder achievement");
                    return await CompleteDummyGenerationAsync(
                        app,
                        progressMode,
                        DummyAchievementReason.NoApiKey,
                        "Placeholder achievement created successfully",
                        "Failed to create placeholder achievement");
                }

                if (fetch.Status == SteamAchievementFetchStatus.ApiError)
                {
                    return await HandleApiError("ERROR:" + fetch.ApiErrorCode, app, progressMode);
                }

                if (fetch.Status == SteamAchievementFetchStatus.EmptyList)
                {
                    return await CompleteDummyGenerationAsync(
                        app,
                        progressMode,
                        DummyAchievementReason.NoAchievements,
                        "No achievements available - placeholder created",
                        "No achievements available - failed to create placeholder");
                }

                bool success = await GenerateAchievementFilesAsync(
                    app,
                    fetch.Achievements,
                    achievementsFile,
                    imagesFolder,
                    progressMode).ConfigureAwait(false);

                if (success && ProgressIsMenu(progressMode) && _taskReportService != null)
                {
                    _taskReportService.SetMessage("Achievement generation successful");
                    _taskReportService.SetProgress(100, 100);
                    await Task.Delay(FeedbackDisplayDelayMs);
                    _taskReportService.SetMessage("");
                    _taskReportService.SetProgress(0, 0);
                }

                return success;
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError("Achievement generation failed", ex);
                if (ProgressActive(progressMode) && _taskReportService != null)
                {
                    if (ProgressBarOnly(progressMode))
                        _taskReportService.SetProgress(0, 0);
                    else if (ProgressIsAddSave(progressMode))
                        ClearAddSaveAchievementProgress();
                    else
                    {
                        _taskReportService.SetMessage("Achievement generation failed.", TaskReportKind.Error);
                        _taskReportService.SetProgress(0, 0);
                    }
                }

                throw;
            }
        }

        public async Task<bool> GenerateAchievementsForAddSaveAsync(
            GameConfig app,
            AchievementPreviewKind previewKind,
            bool showProgress = true,
            bool progressOnlyNoMessages = false)
        {
            if (app == null || app.AppId == 0)
                return false;

            AchievementProgressMode progressMode = !showProgress
                ? AchievementProgressMode.None
                : progressOnlyNoMessages
                    ? AchievementProgressMode.AddSaveBarOnly
                    : AchievementProgressMode.AddSaveVerbose;

            switch (previewKind)
            {
                case AchievementPreviewKind.NoApiKey:
                    return await CompleteDummyGenerationAsync(
                        app,
                        progressMode,
                        DummyAchievementReason.NoApiKey,
                        "Placeholder achievement created successfully",
                        "Failed to create placeholder achievement").ConfigureAwait(false);
                case AchievementPreviewKind.NoAchievementsOnSteam:
                    return await CompleteDummyGenerationAsync(
                        app,
                        progressMode,
                        DummyAchievementReason.NoAchievements,
                        "No achievements on Steam — placeholder created",
                        "Failed to create placeholder achievement").ConfigureAwait(false);
                default:
                    return await GenerateAchievementsCoreAsync(app, progressMode).ConfigureAwait(false);
            }
        }

        public async Task<(AchievementPreviewKind kind, string previewJson)> BuildAddModePreviewAsync(GameConfig app)
        {
            if (app == null || app.AppId == 0)
            {
                return (
                    AchievementPreviewKind.NoApiKey,
                    AchievementPreviewHelper.BuildDummyPreviewJson(DummyAchievementReason.NoApiKey));
            }

            try
            {
                SteamAchievementFetchResult fetch = await FetchAchievementSchemaFromSteamAsync(
                    app.AppId,
                    showRetrieveStatusMessage: false).ConfigureAwait(false);

                if (fetch.Failure != null)
                {
                    Program.LogService?.LogWarning(
                        $"Add-mode achievement preview failed for app {app.AppId}: {fetch.Failure.Message}");
                    return (
                        AchievementPreviewKind.NoApiKey,
                        AchievementPreviewHelper.BuildDummyPreviewJson(DummyAchievementReason.NoApiKey));
                }

                if (fetch.Status == SteamAchievementFetchStatus.InvalidApiKey)
                {
                    return (
                        AchievementPreviewKind.NoApiKey,
                        AchievementPreviewHelper.BuildDummyPreviewJson(DummyAchievementReason.NoApiKey));
                }

                if (fetch.Status == SteamAchievementFetchStatus.ApiError)
                {
                    if (fetch.ApiErrorCode == "404")
                    {
                        return (
                            AchievementPreviewKind.NoAchievementsOnSteam,
                            AchievementPreviewHelper.BuildDummyPreviewJson(DummyAchievementReason.NoAchievements));
                    }

                    return (
                        AchievementPreviewKind.NoApiKey,
                        AchievementPreviewHelper.BuildDummyPreviewJson(DummyAchievementReason.NoApiKey));
                }

                if (fetch.Status == SteamAchievementFetchStatus.EmptyList)
                {
                    return (
                        AchievementPreviewKind.NoAchievementsOnSteam,
                        AchievementPreviewHelper.BuildDummyPreviewJson(DummyAchievementReason.NoAchievements));
                }

                ClearAchievementIconUrlsForPreview(fetch.Achievements);
                string json = JsonConvert.SerializeObject(fetch.Achievements, JsonFormatting.Indented);
                return (AchievementPreviewKind.RealList, json);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning($"Add-mode achievement preview failed for app {app.AppId}: {ex.Message}");
                return (
                    AchievementPreviewKind.NoApiKey,
                    AchievementPreviewHelper.BuildDummyPreviewJson(DummyAchievementReason.NoApiKey));
            }
        }

        private async Task<bool> HandleApiError(string result, GameConfig app, AchievementProgressMode progressMode)
        {
            var statusCode = result.Substring("ERROR:".Length).Trim();
            string msg = $"Failed to get achievements definition. Error code: {statusCode}";

            if (statusCode == "401" || statusCode == "403")
            {
                msg += "\n\nYour WebAPI key may be invalid or has expired. Go to setting menu to update your key.";
                Program.LogService?.LogError(msg);
                if (ProgressIsMenu(progressMode))
                    _taskReportService?.SetMessage(msg);
                throw new InvalidApiKeyException(msg);
            }
            if (statusCode == "404")
            {
                return await CompleteDummyGenerationAsync(
                    app,
                    progressMode,
                    DummyAchievementReason.NoAchievements,
                    "No achievements available - placeholder created",
                    "No achievements available - failed to create placeholder");
            }
            if (statusCode == "502")
            {
                msg = "Bad Gateway error while trying to access Steam API.\n\nPlease try again later.";
                if (ProgressIsMenu(progressMode))
                    _taskReportService?.SetMessage(msg);
                throw new NetworkException(msg);
            }

            Program.LogService?.LogError(msg);
            if (ProgressIsMenu(progressMode))
                _taskReportService?.SetMessage(msg);
            if (!int.TryParse(statusCode, out var statusCodeInt))
                statusCodeInt = 0;
            throw new AchievementApiException(msg, statusCodeInt, app.AppId.ToString());
        }

        private async Task<bool> GenerateAchievementFilesAsync(
            GameConfig app,
            List<CAchievement> achievements,
            string achievementsFile,
            string imagesFolder,
            AchievementProgressMode progressMode)
        {
            int achievementCount = achievements.Count;
            int totalImages = achievementCount * 2;
            int imagesDownloaded = 0;
            int achievementsCompleted = 0;

            if (ProgressActive(progressMode))
            {
                if (progressMode == AchievementProgressMode.AddSaveVerbose)
                {
                    _taskReportService?.SetMessage($"Generating achievements 0/{achievementCount}");
                    _taskReportService?.SetProgress(0, achievementCount);
                }
                else if (progressMode == AchievementProgressMode.AddSaveBarOnly)
                {
                    ReportAddSaveDownloadingIconsProgress(app, progressMode, 0, achievementCount);
                    _taskReportService?.SetProgress(0, achievementCount);
                }
                else
                {
                    _taskReportService?.SetMessage($"Generating {achievementCount} achievements... Please wait.");
                    _taskReportService?.SetProgress(0, totalImages);
                }
            }

            if (!Directory.Exists(imagesFolder))
                Directory.CreateDirectory(imagesFolder);

            // Icons download in parallel; Interlocked counters feed either per-image (menu) or per-achievement (add-save) progress.
            var tasks = achievements.Select(async achievement =>
            {
                int imagesForThisAchievement = await DownloadAchievementImagesAsync(achievement, imagesFolder);
                int newImagesDownloaded = Interlocked.Add(ref imagesDownloaded, imagesForThisAchievement);
                int newAchievementsCompleted = Interlocked.Increment(ref achievementsCompleted);
                if (!ProgressActive(progressMode))
                    return;

                if (ProgressIsAddSave(progressMode))
                {
                    _taskReportService?.SetProgress(newAchievementsCompleted, achievementCount);
                    if (progressMode == AchievementProgressMode.AddSaveVerbose)
                        _taskReportService?.SetMessage($"Generating achievements {newAchievementsCompleted}/{achievementCount}");
                    else if (ProgressBarOnly(progressMode))
                        ReportAddSaveDownloadingIconsProgress(app, progressMode, newAchievementsCompleted, achievementCount);
                }
                else
                {
                    _taskReportService?.SetProgress(newImagesDownloaded, totalImages);
                    _taskReportService?.SetMessage($"{newAchievementsCompleted}/{achievementCount} achievements generated... Please wait.");
                }
            });
            await Task.WhenAll(tasks);

            File.WriteAllText(achievementsFile, JsonConvert.SerializeObject(achievements, JsonFormatting.Indented), Encoding.UTF8);
            TryEnsureUserSavesAchievementsProgressFile(app.AppId, achievements);

            if (progressMode == AchievementProgressMode.AddSaveVerbose)
                ClearAddSaveAchievementProgress();
            else if (ProgressIsMenu(progressMode))
            {
                _taskReportService?.SetMessage($"Successfully generated {achievementCount} achievements.");
                _taskReportService?.SetProgress(totalImages, totalImages);
            }

            return true;
        }

        private async Task<bool> EnsureAchievementImageAsync(string url, string localPath)
        {
            if (File.Exists(localPath))
                return true;
            if (!string.IsNullOrEmpty(url) && PathValidationHelper.IsSafeUrl(url))
            {
                try
                {
                    byte[] data = await HttpHelpers.GetByteArrayAsync(url, AchievementConstants.HttpRequestLongTimeout);
                    await Task.Run(() => File.WriteAllBytes(localPath, data));
                    return true;
                }
                catch (Exception)
                {
                    // Download failed, fall through to placeholder
                }
            }
            return TrySavePlaceholderAsJpeg(localPath);
        }

        private async Task<int> DownloadAchievementImagesAsync(CAchievement achievement, string imagesFolder)
        {
            string iconPath = Path.Combine(imagesFolder, achievement.name + ".jpg");
            string iconGrayPath = Path.Combine(imagesFolder, achievement.name + "_gray.jpg");
            int imagesProcessed = 0;

            if (await EnsureAchievementImageAsync(achievement.icon, iconPath))
                imagesProcessed++;
            if (await EnsureAchievementImageAsync(achievement.icongray, iconGrayPath))
                imagesProcessed++;

            // Replace Steam CDN URLs with steam_settings-relative paths written into achievements.json.
            achievement.icon = GetAchievementImageRelativePath(achievement.name, false);
            achievement.icongray = GetAchievementImageRelativePath(achievement.name, true);
            achievement.icon_gray = achievement.icongray;

            return imagesProcessed;
        }

        private void EnsureSteamSettingsFolder(GameConfig app)
        {
            string steamSettings = GetSteamSettingsFolder(app);
            if (!Directory.Exists(steamSettings))
                Directory.CreateDirectory(steamSettings);
        }

        // User progress JSON under %AppData%\GSE Saves\{appId}\ (gbe_fork runtime contract); separate from steam_settings schema.
        private static void TryEnsureUserSavesAchievementsProgressFile(ulong appId, IList<CAchievement> achievements)
        {
            if (appId == 0 || achievements == null || achievements.Count == 0)
                return;

            string savesDirectory = PathConstants.GetUserSavesPath(PathConstants.GseSavesFolderName, appId);
            if (string.IsNullOrEmpty(savesDirectory))
                return;

            string path = Path.Combine(savesDirectory, AchievementConstants.AchievementsFileName);
            if (File.Exists(path))
                return;

            string progressJson = BuildUserSavesAchievementsProgressJson(achievements);
            if (string.IsNullOrEmpty(progressJson))
                return;

            try
            {
                Directory.CreateDirectory(savesDirectory);
                File.WriteAllText(path, progressJson, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning(
                    $"Failed to create user saves {AchievementConstants.AchievementsFileName} for app {appId}: {ex.Message}");
            }
        }

        private static string BuildUserSavesAchievementsProgressJson(IEnumerable<CAchievement> achievements)
        {
            var root = new JsonObject();
            foreach (CAchievement achievement in achievements)
            {
                if (achievement == null || string.IsNullOrWhiteSpace(achievement.name))
                    continue;

                root[achievement.name.Trim()] = new JsonObject
                {
                    ["earned"] = new JsonBool(false),
                    ["earned_time"] = new JsonNumber(0L)
                };
            }

            return root.Count == 0 ? null : JsonConvert.SerializeObject(root, JsonFormatting.Indented);
        }

        private bool EnsureDummyAchievementAndFiles(GameConfig app, DummyAchievementReason reason, AchievementProgressMode progressMode)
        {
            try
            {
                if (app == null)
                {
                    Program.LogService?.LogError("Cannot create dummy achievement: GameConfig is null");
                    return false;
                }

                string steamSettingsFolder = GetSteamSettingsFolder(app);
                if (string.IsNullOrEmpty(steamSettingsFolder))
                {
                    Program.LogService?.LogError("Cannot create dummy achievement: Game folder path is null or empty");
                    return false;
                }

                string achievementsFile = GetAchievementsFilePath(app);
                string imagesFolder = GetAchievementImagesFolder(app);

                try
                {
                    if (!Directory.Exists(steamSettingsFolder))
                        Directory.CreateDirectory(steamSettingsFolder);
                    if (!Directory.Exists(imagesFolder))
                        Directory.CreateDirectory(imagesFolder);
                }
                catch (Exception dirEx)
                {
                    Program.LogService?.LogError($"Failed to create achievement directories: {dirEx.Message}", dirEx);
                    return false;
                }

                var dummyText = AchievementPreviewHelper.GetDummyFields(reason);
                string achievementName = dummyText.name;
                string displayName = dummyText.displayName;
                string description = dummyText.description;

                var dummyAchievement = new CAchievement
                {
                    name = achievementName,
                    displayName = displayName,
                    description = description,
                    hidden = 0,
                    icon = "",
                    icongray = "",
                    icon_gray = ""
                };

                string iconPath = Path.Combine(imagesFolder, achievementName + ".jpg");
                bool imagesCreated = false;
                string achievementImagePath = GetAchievementPlaceholderImagePath();

                if (!string.IsNullOrEmpty(achievementImagePath))
                {
                    if (ProgressActive(progressMode) && !ProgressBarOnly(progressMode))
                        _taskReportService?.SetMessage("Creating achievement image...");
                    if (TrySaveJpegFromBitmapSource(achievementImagePath, iconPath))
                    {
                        imagesCreated = true;
                        Program.LogService?.LogDebug("Successfully created dummy achievement image");
                    }
                    else
                    {
                        Program.LogService?.LogWarning($"Failed to create achievement image from {achievementImagePath}");
                        if (ProgressActive(progressMode) && !ProgressBarOnly(progressMode))
                            _taskReportService?.SetProgress(0, 100);
                    }
                }
                else
                {
                    Program.LogService?.LogWarning("Could not find achievement.png in Resources/Images folder. Creating achievement without images.");
                }

                // One placeholder JPEG serves as both locked and unlocked icon paths.
                if (imagesCreated)
                {
                    string imagePath = GetAchievementImageRelativePath(achievementName, false);
                    dummyAchievement.icon = imagePath;
                    dummyAchievement.icongray = imagePath;
                    dummyAchievement.icon_gray = imagePath;
                }
                else
                {
                    dummyAchievement.icon = "";
                    dummyAchievement.icongray = "";
                    dummyAchievement.icon_gray = "";
                }

                try
                {
                    var achievements = new List<CAchievement> { dummyAchievement };
                    string json = JsonConvert.SerializeObject(achievements, JsonFormatting.Indented);
                    File.WriteAllText(achievementsFile, json, Encoding.UTF8);
                    TryEnsureUserSavesAchievementsProgressFile(app.AppId, achievements);

                    Program.LogService?.LogDebug("Successfully created dummy achievement file");
                    return true;
                }
                catch (Exception fileEx)
                {
                    Program.LogService?.LogError($"Failed to write achievement file: {fileEx.Message}", fileEx);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Failed to create dummy achievement files: {ex.Message}", ex);
                return false;
            }
        }

        #endregion
    }
}
