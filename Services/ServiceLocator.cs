using System;
using System.ComponentModel;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Generators;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    public static class ServiceLocator
    {
        private sealed class DesignTimeLogService : ILogService
        {
            internal static readonly DesignTimeLogService Instance = new DesignTimeLogService();

            private DesignTimeLogService()
            {
            }

            public void LogDebug(string message)
            {
            }

            public void LogMessage(string message)
            {
            }

            public void LogWarning(string message)
            {
            }

            public void LogError(string message)
            {
            }

            public void LogError(string message, Exception exception)
            {
            }
        }

        private static ILogService CreateLogService()
        {
            return BootstrapService.LogService
                ?? (LicenseManager.UsageMode == LicenseUsageMode.Designtime
                    ? (ILogService)DesignTimeLogService.Instance
                    : new LogService(LoggingConfiguration.CreateDefault()));
        }

        private static readonly Lazy<ILogService> _logService = new Lazy<ILogService>(CreateLogService);
        private static readonly Lazy<AppDataService> _appDataService = new Lazy<AppDataService>();
        private static AppDataService _appDataServiceOverride;
        private static readonly Lazy<GameDataService> _gameDataService = new Lazy<GameDataService>();
        private static readonly Lazy<GameDisplayService> _gameDisplayService = new Lazy<GameDisplayService>();
        private static readonly Lazy<LaunchSessionCleanupService> _launchSessionCleanupService = new Lazy<LaunchSessionCleanupService>();
        private static readonly Lazy<GameLaunchService> _gameLaunchService = new Lazy<GameLaunchService>();
        private static readonly Lazy<SteamProductInfoService> _steamProductInfoService = new Lazy<SteamProductInfoService>();
        private static readonly Lazy<GameSetupService> _gameSetupService = new Lazy<GameSetupService>();
        private static readonly Lazy<ThemeService> _themeService = new Lazy<ThemeService>();
        private static readonly Lazy<EmulatorConfigService> _emulatorConfigService = new Lazy<EmulatorConfigService>();
        private static readonly Lazy<GoldbergCfgService> _goldbergCfgService = new Lazy<GoldbergCfgService>();
        private static readonly Lazy<IniFileService> _iniFileService = new Lazy<IniFileService>();
        private static readonly Lazy<GameImageService> _gameImageService = new Lazy<GameImageService>();
        private static readonly Lazy<ImageNormalizationService> _imageNormalizationService = new Lazy<ImageNormalizationService>();
        private static readonly Lazy<IconService> _iconService = new Lazy<IconService>();
        private static readonly Lazy<DlcService> _dlcService = new Lazy<DlcService>(() => new DlcService());
        private static volatile TaskReportService _taskReportService;
        private static readonly Lazy<SteamApiKeyService> _steamApiKeyService = new Lazy<SteamApiKeyService>();
        private static readonly Lazy<LaunchOptionService> _launchOptionService = new Lazy<LaunchOptionService>();
        private static readonly Lazy<GoldbergFilesService> _goldbergFilesService = new Lazy<GoldbergFilesService>();
        private static readonly Lazy<AchievementService> _achievementService =
            new Lazy<AchievementService>(() => new AchievementService(steamApiKeyService: _steamApiKeyService.Value));
        private static readonly Lazy<GoldbergArtifactService> _goldbergArtifactService = new Lazy<GoldbergArtifactService>();
        private static readonly Lazy<StatsGenerator> _statsGenerator = new Lazy<StatsGenerator>();
        private static readonly Lazy<RegistryService> _registryService = new Lazy<RegistryService>();
        private static readonly Lazy<AssetDownloadService> _assetDownloadService = new Lazy<AssetDownloadService>();
        private static readonly Lazy<GameSettingsSaveService> _gameSettingsSaveService = new Lazy<GameSettingsSaveService>();
        private static readonly Lazy<PendingAddGameListService> _pendingAddGameListService = new Lazy<PendingAddGameListService>();
        private static readonly Lazy<GameAddCollector> _gameAddCollector = new Lazy<GameAddCollector>();
        private static readonly Lazy<GameEditLoader> _gameEditLoader = new Lazy<GameEditLoader>();
        private static readonly Lazy<GameSaveWriter> _gameSaveWriter = new Lazy<GameSaveWriter>();
        private static readonly Lazy<SteamlessService> _steamlessService = new Lazy<SteamlessService>();
        private static readonly Lazy<SteamInterfacesService> _steamInterfacesService = new Lazy<SteamInterfacesService>();

        public static ILogService LogService => _logService.Value;

        public static AppDataService AppDataService => _appDataServiceOverride ?? _appDataService.Value;

        internal static void SetAppDataServiceForTests(AppDataService service)
        {
            _appDataServiceOverride = service;
        }

        internal static void ClearAppDataServiceForTests()
        {
            _appDataServiceOverride = null;
        }

        public static GameDataService GameDataService => _gameDataService.Value;

        public static GameDisplayService GameDisplayService => _gameDisplayService.Value;

        public static LaunchSessionCleanupService LaunchSessionCleanupService => _launchSessionCleanupService.Value;

        public static GameLaunchService GameLaunchService => _gameLaunchService.Value;

        public static GameSetupService GameSetupService => _gameSetupService.Value;

        public static ThemeService ThemeService => _themeService.Value;

        public static SteamProductInfoService SteamProductInfoService => _steamProductInfoService.Value;

        public static EmulatorConfigService EmulatorConfigService => _emulatorConfigService.Value;

        public static GoldbergCfgService GoldbergCfgService => _goldbergCfgService.Value;

        public static IniFileService IniFileService => _iniFileService.Value;

        public static GameImageService GameImageService => _gameImageService.Value;

        public static ImageNormalizationService ImageNormalizationService => _imageNormalizationService.Value;

        public static IconService IconService => _iconService.Value;

        public static DlcService DlcService => _dlcService.Value;

        public static TaskReportService TaskReportService => _taskReportService;

        internal static void SetTaskReportService(TaskReportService service)
        {
            _taskReportService = service;
        }

        public static SteamApiKeyService SteamApiKeyService => _steamApiKeyService.Value;

        public static LaunchOptionService LaunchOptionService => _launchOptionService.Value;

        public static GoldbergFilesService GoldbergFilesService => _goldbergFilesService.Value;

        public static AchievementService AchievementService => _achievementService.Value;

        public static GoldbergArtifactService GoldbergArtifactService => _goldbergArtifactService.Value;

        public static StatsGenerator StatsGenerator => _statsGenerator.Value;

        public static IRegistryService RegistryService => _registryService.Value;

        public static AssetDownloadService AssetDownloadService => _assetDownloadService.Value;

        public static GameSettingsSaveService GameSettingsSaveService => _gameSettingsSaveService.Value;

        public static PendingAddGameListService PendingAddGameListService => _pendingAddGameListService.Value;

        public static GameAddCollector GameAddCollector => _gameAddCollector.Value;

        public static GameEditLoader GameEditLoader => _gameEditLoader.Value;

        public static GameSaveWriter GameSaveWriter => _gameSaveWriter.Value;

        public static SteamlessService SteamlessService => _steamlessService.Value;

        public static SteamInterfacesService SteamInterfacesService => _steamInterfacesService.Value;

        // Call once after the UI message loop exits; tears down session-scoped singletons (Steam PICS, images, theme).
        internal static void DisposeApplicationResources()
        {
            try
            {
                if (_steamProductInfoService.IsValueCreated)
                    _steamProductInfoService.Value.Dispose();
            }
            catch
            {
            }

            try
            {
                if (_gameImageService.IsValueCreated)
                    _gameImageService.Value.Dispose();
            }
            catch
            {
                // Avoid throwing from shutdown paths.
            }

            try
            {
                if (_themeService.IsValueCreated)
                    _themeService.Value.Dispose();
            }
            catch
            {
            }
        }
    }
}
