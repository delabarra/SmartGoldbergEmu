using System;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    public class GameEditLoader
    {
        private readonly GameDataService _gameDataService;
        private readonly EmulatorConfigService _emulatorConfigService;
        private readonly GoldbergFilesService _goldbergFilesService;
        private readonly IRegistryService _registryService;

        public GameEditLoader()
            : this(
                ServiceLocator.GameDataService,
                ServiceLocator.EmulatorConfigService,
                ServiceLocator.GoldbergFilesService,
                ServiceLocator.RegistryService)
        {
        }

        public GameEditLoader(
            GameDataService gameDataService,
            EmulatorConfigService emulatorConfigService,
            GoldbergFilesService goldbergFilesService,
            IRegistryService registryService)
        {
            _gameDataService = gameDataService ?? throw new ArgumentNullException(nameof(gameDataService));
            _emulatorConfigService = emulatorConfigService ?? throw new ArgumentNullException(nameof(emulatorConfigService));
            _goldbergFilesService = goldbergFilesService ?? throw new ArgumentNullException(nameof(goldbergFilesService));
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
        }

        public GameEditBundle Load(GameConfig game)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));

            GameConfig libraryRow = _gameDataService.GetGame(game.GameGuid) ?? game;
            var bundle = new GameEditBundle
            {
                Game = CloneGameConfig(libraryRow)
            };

            ulong appId = bundle.Game.AppId;
            if (appId == 0)
                return bundle;

            bundle.SettingsSnapshot = _emulatorConfigService.LoadGameSettingsSnapshot(appId, mergePerGameSteamSettings: true);

            string iniTicket = bundle.SettingsSnapshot.User?.Ticket ?? string.Empty;
            string iniAlt = bundle.SettingsSnapshot.User?.AltSteamId ?? string.Empty;

            GameCredentialPersistenceService.ApplyRegistryFallbackForDisplay(appId, bundle.SettingsSnapshot, _registryService);

            if (string.IsNullOrEmpty(iniTicket) && !string.IsNullOrEmpty(bundle.SettingsSnapshot.User?.Ticket))
                bundle.RegistryTicket = bundle.SettingsSnapshot.User.Ticket;
            if (string.IsNullOrEmpty(iniAlt) && !string.IsNullOrEmpty(bundle.SettingsSnapshot.User?.AltSteamId))
                bundle.RegistryAltSteamId = bundle.SettingsSnapshot.User.AltSteamId;

            GoldbergFilesService.AppConfigData appConfig = _goldbergFilesService.LoadAppConfigDlcAndPaths(appId);
            if (appConfig?.DlcData != null && appConfig.DlcData.Count > 0)
                bundle.DlcData = appConfig.DlcData;

            LoadSidecars(appId, bundle.Sidecars);
            return bundle;
        }

        private void LoadSidecars(ulong appId, GameEditSidecarContent sidecars)
        {
            if (sidecars == null || appId == 0)
                return;

            sidecars.Leaderboards = _goldbergFilesService.LoadLeaderboards(appId) ?? string.Empty;
            sidecars.CustomBroadcasts = _goldbergFilesService.LoadCustomBroadcasts(appId) ?? string.Empty;
            sidecars.SubscribedGroups = _goldbergFilesService.LoadSubscribedGroups(appId) ?? string.Empty;
            sidecars.SubscribedGroupsClans = _goldbergFilesService.LoadSubscribedGroupsClans(appId) ?? string.Empty;
            sidecars.AutoAcceptInvite = _goldbergFilesService.LoadAutoAcceptInvite(appId) ?? string.Empty;
            sidecars.BranchesJson = _goldbergFilesService.LoadBranches(appId) ?? string.Empty;
            sidecars.SteamInterfaces = _goldbergFilesService.LoadSteamSettingsTextFile(appId, PathConstants.GoldbergSteamInterfacesFileName);
            sidecars.AchievementsJson = _goldbergFilesService.LoadAchievements(appId) ?? string.Empty;
            sidecars.ItemsJson = _goldbergFilesService.LoadItems(appId) ?? string.Empty;
            sidecars.CustomStatsJson = _goldbergFilesService.LoadStats(appId) ?? string.Empty;
            sidecars.SupportedLanguages = _goldbergFilesService.LoadSupportedLanguages(appId) ?? string.Empty;
        }

        private static GameConfig CloneGameConfig(GameConfig source)
        {
            if (source == null)
                return new GameConfig();

            return new GameConfig
            {
                AppName = source.AppName,
                AppId = source.AppId,
                StartFolder = source.StartFolder,
                Path = source.Path,
                Parameters = source.Parameters,
                WorkingDirectory = source.WorkingDirectory,
                CustomIcon = source.CustomIcon,
                GameGuid = source.GameGuid,
                LaunchMode = source.LaunchMode,
                PreFetchedDlcData = source.PreFetchedDlcData,
                DlcCheckPerformed = source.DlcCheckPerformed,
                SupportedLanguages = source.SupportedLanguages,
                AppPicsKeyValue = source.AppPicsKeyValue
            };
        }
    }
}
