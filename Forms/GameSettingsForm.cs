using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SmartGoldbergEmu.JsonKit;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Extensions;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using SmartGoldbergEmu.Validation;
using SteamKit;

namespace SmartGoldbergEmu.Forms
{
    public partial class GameSettingsForm : Form
    {
        private readonly GameConfig _gameConfig;
        private readonly bool _isEditMode;
        private readonly GameAddBundle _addBundle;
        private readonly GameEditBundle _editBundle;
        private bool _editSidecarsApplied;
        public PendingAddGameSave PendingAddSave { get; private set; }
        public Guid EditExistingGameGuid { get; private set; }
        private string _initialCredentialTicket = string.Empty;
        private string _initialCredentialAlt = string.Empty;
        private OnlineAppData _metadata;
        private readonly GameDataService _gameDataService;
        private readonly EmulatorConfigService _emulatorConfigService;
        private readonly ThemeService _themeService;
        private readonly DlcService _dlcService;
        private readonly GameSettingsSaveService _gameSettingsSaveService;
        private readonly GameSaveWriter _gameSaveWriter;
        private readonly Action _onSaveCompleted;
        private readonly ITaskReportService _taskReportService;
        private readonly AchievementService _achievementService;
        private readonly GameLaunchService _gameLaunchService;
        private readonly SteamApiKeyService _steamApiKeyService;
        private GameConfig _initialGameConfig;
        private GameSettingsSnapshot _initialSettingsSnapshot;
        private string _initialDlcList = string.Empty;
        private string _achievementsRawJson = string.Empty;

        private bool _isLoading = false;
        private PlaceholderTextBoxHelper _placeholderHelper;

        public GameSettingsForm()
            : this(null, false, null, null, null, null, null, null, null, null, null, null, null, null)
        {
        }

        public GameSettingsForm(
            GameConfig game,
            bool isEditMode = false,
            OnlineAppData metadata = null,
            GameDataService gameDataService = null,
            EmulatorConfigService emulatorConfigService = null,
            ThemeService themeService = null,
            DlcService dlcService = null,
            ITaskReportService feedbackService = null,
            AchievementService achievementService = null,
            GameSettingsSaveService gameSettingsSaveService = null,
            Action onSaveCompleted = null,
            GameAddBundle addBundle = null,
            GameSaveWriter gameSaveWriter = null,
            GameEditBundle editBundle = null,
            SteamApiKeyService steamApiKeyService = null)
        {
            InitializeComponent();

            _gameConfig = game;
            _isEditMode = isEditMode;
            _addBundle = addBundle;
            _editBundle = editBundle;
            _metadata = metadata;
            _gameDataService = gameDataService ?? ServiceLocator.GameDataService ?? throw new ArgumentNullException(nameof(gameDataService));
            _emulatorConfigService = emulatorConfigService ?? ServiceLocator.EmulatorConfigService ?? throw new ArgumentNullException(nameof(emulatorConfigService));
            _themeService = themeService ?? ServiceLocator.ThemeService ?? throw new ArgumentNullException(nameof(themeService));
            _dlcService = dlcService ?? ServiceLocator.DlcService ?? throw new ArgumentNullException(nameof(dlcService));
            _taskReportService = feedbackService;
            _gameSettingsSaveService = gameSettingsSaveService ?? ServiceLocator.GameSettingsSaveService ?? throw new ArgumentNullException(nameof(gameSettingsSaveService));
            _gameSaveWriter = gameSaveWriter ?? ServiceLocator.GameSaveWriter ?? throw new ArgumentNullException(nameof(gameSaveWriter));
            _onSaveCompleted = onSaveCompleted;
            _achievementService = achievementService ?? ServiceLocator.AchievementService;
            _gameLaunchService = ServiceLocator.GameLaunchService;
            _steamApiKeyService = steamApiKeyService ?? ServiceLocator.SteamApiKeyService;

            if (DesignTimeHelper.IsDesignTime)
                return;

            _placeholderHelper = new PlaceholderTextBoxHelper(GetThemeForegroundColor);
            WireAchievementsPreviewListEvents();
            WireModsSummaryListEvents();
            WireInventoryListEvents();

            Text = isEditMode ? $"Edit Game - {game?.AppName ?? "Unknown"}" : $"Add Game - {game?.AppName ?? "Unknown"}";

            ApplyTheme();
            _themeService.ThemeChanged += ThemeService_ThemeChanged;

            if (txtGameFolder != null)
                txtGameFolder.TextChanged += TxtGameFolder_TextChanged;
            if (txtGameExecutable != null)
                txtGameExecutable.TextChanged += TxtGameExecutable_TextChanged;

            SetupInventoryListView();
            LayoutInventoryEditor();
            if (pnlStatsDisplayScroll != null)
                pnlStatsDisplayScroll.Resize += PnlStatsDisplayScroll_Resize;
            if (grpInventoryEditor != null)
                grpInventoryEditor.Resize += GrpInventoryEditor_Resize;
            if (grpBasicInfo != null)
                grpBasicInfo.SizeChanged += GrpBasicInfo_SizeChanged;
        }

        private void WireAchievementsPreviewListEvents()
        {
            if (lstAchievementsPreview == null)
                return;
            lstAchievementsPreview.SizeChanged += LstAchievementsPreview_SizeChanged;
            lstAchievementsPreview.ItemSelectionChanged += LstAchievementsPreview_ItemSelectionChanged;
            lstAchievementsPreview.DrawColumnHeader += LstAchievementsPreview_DrawColumnHeader;
            lstAchievementsPreview.DrawItem += LstAchievementsPreview_DrawItem;
            lstAchievementsPreview.DrawSubItem += LstAchievementsPreview_DrawSubItem;
            lstAchievementsPreview.ColumnWidthChanging += LstAchievementsPreview_ColumnWidthChanging;
            lstAchievementsPreview.ColumnWidthChanged += LstAchievementsPreview_ColumnWidthChanged;
        }

        private void ConfigureFormKeyboardNavigation()
        {
            ApplyLabelTabStops(this);
        }

        private static void ApplyLabelTabStops(Control root)
        {
            if (root == null)
                return;

            foreach (Control child in root.Controls)
            {
                if (child is Label)
                    child.TabStop = false;
                if (child.HasChildren)
                    ApplyLabelTabStops(child);
            }
        }

        private void GameSettingsForm_Load(object sender, EventArgs e)
        {
            _isLoading = true;
            ConfigureFormKeyboardNavigation();
            InitializePlaceholders();

            if (_gameConfig != null)
            {
                LoadGameConfig();
                ValidateSteamApiDlls();
            }

            WireUpChangeEvents();

            if (txtAppID != null)
                txtAppID.Leave += TxtAppID_Leave_RefreshSteamLaunch;

            if (txtForceSteamId != null)
            {
                txtForceSteamId.TextChanged += TxtForceSteamId_TextChanged;
                txtForceSteamId.Leave += TxtForceSteamId_Leave;
            }

            InitializeLanguageComboBoxPlaceholder();

            if (cmbForceLanguage != null && cmbForceLanguage.SelectedIndex < 0 && cmbForceLanguage.Items.Count > 0)
            {
                int idx = cmbForceLanguage.Items.IndexOf(SteamLanguageDisplayHelper.UseGlobalSettingOption);
                if (idx >= 0)
                    cmbForceLanguage.SelectedIndex = idx;
            }

            StoreInitialState();
            _isLoading = false;
            btnSave.Enabled = !_isEditMode;
            UpdateGameFolderInstallDirHintVisibility();
        }

        private void LoadGameConfig()
        {
            try
            {
                if (txtAppID != null)
                {
                    SetTextBoxText(txtAppID, _gameConfig.AppId.ToString());
                    if (_isEditMode)
                    {
                        txtAppID.ReadOnly = true;
                        txtAppID.TabStop = false;
                    }
                }

                if (txtGameFolder != null)
                    SetTextBoxText(txtGameFolder, _gameConfig.StartFolder);

                if (txtGameExecutable != null)
                    SetTextBoxText(txtGameExecutable, PathValidationHelper.ToDisplayPathRelativeToGameFolder(_gameConfig.Path, _gameConfig.StartFolder));

                if (txtLaunchParameters != null)
                    SetTextBoxText(txtLaunchParameters, _gameConfig.Parameters);

                if (txtWorkingDirectory != null)
                    SetTextBoxText(txtWorkingDirectory, PathValidationHelper.ToDisplayPathRelativeToGameFolder(_gameConfig.WorkingDirectory, _gameConfig.StartFolder));

                if (txtCustomIcon != null)
                    SetTextBoxText(txtCustomIcon, _gameConfig.CustomIcon);

                SelectLaunchModeUi(_gameConfig.LaunchMode);

                ApplyLaunchModeAvailability();

                if (txtGameName != null)
                {
                    string name = (_metadata != null && !string.IsNullOrEmpty(_metadata.Name)) ? _metadata.Name : _gameConfig.AppName;
                    if (!string.IsNullOrEmpty(name))
                        SetTextBoxText(txtGameName, name);
                }

                if (_metadata != null && !string.IsNullOrEmpty(_metadata.SupportedLanguages) && cmbForceLanguage != null)
                    PopulateLanguageDropdown(_metadata.SupportedLanguages);

                if (!_isEditMode && cmbForceLanguage != null
                    && (cmbForceLanguage.Items.Count <= 1)
                    && _gameConfig.SupportedLanguages != null
                    && _gameConfig.SupportedLanguages.Count > 0)
                {
                    PopulateLanguageDropdown(string.Join(",", _gameConfig.SupportedLanguages));
                }

                if (_gameConfig.PreFetchedDlcData != null && _gameConfig.PreFetchedDlcData.Count > 0)
                    SetTextBoxText(txtDLCList, DlcService.BuildDlcListText(_gameConfig.PreFetchedDlcData));
                else if (_gameConfig.DlcCheckPerformed && txtDLCList != null)
                    SetTextBoxText(txtDLCList, "No DLC found for this game.");

                if (_isEditMode)
                {
                    if (_editBundle != null)
                        ApplyEditBundlePreview();
                    else
                    {
                        LoadSupportedLanguagesFromFile();
                        LoadSettingsToForm(_emulatorConfigService.LoadGameSettingsSnapshot(_gameConfig.AppId));
                    }
                }
                else
                {
                    if (_addBundle != null)
                    {
                        ApplyAddBundlePreview();
                    }
                    else
                    {
                        if (_gameConfig.AppId > 0)
                        {
                            LoadSettingsToForm(
                                _emulatorConfigService.LoadGameSettingsSnapshot(_gameConfig.AppId, mergePerGameSteamSettings: false),
                                loadPerGamePersistedGoldbergFiles: false);
                        }

                        ulong appIdToCheck = _gameConfig?.AppId ?? 0;
                        if (appIdToCheck == 0 && txtAppID != null && ulong.TryParse(txtAppID.Text.Trim(), out ulong parsedAppId))
                            appIdToCheck = parsedAppId;

                        if (appIdToCheck > 0)
                        {
                            try
                            {
                                var registryService = ServiceLocator.RegistryService;
                                string storedTicket = registryService.GetBase64Ticket(appIdToCheck);
                                string storedAltSteamId = registryService.GetBase64AltSteamId(appIdToCheck);

                                if (!string.IsNullOrEmpty(storedTicket) && txtUserTicket != null)
                                    SetTextBoxText(txtUserTicket, storedTicket);

                                if (!string.IsNullOrEmpty(storedAltSteamId) && txtAltSteamId != null)
                                    _placeholderHelper.SetTextBoxValue(txtAltSteamId, storedAltSteamId);
                            }
                            catch (Exception ex)
                            {
                                LogWarningWithExceptionMessage("Failed to load base64 token from registry", ex);
                            }
                        }

                    }
                }

                RequestRefreshSteamLaunchOptionsCombo();
            }
            catch (Exception ex)
            {
                LogErrorWithExceptionMessage("Error loading game config", ex);
                FormMessageBoxHelper.ShowIfAlive(this, $"Error loading game configuration: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyEditBundlePreview()
        {
            if (_editBundle == null || _gameConfig == null)
                return;

            GameEditSidecarContent sidecars = _editBundle.Sidecars;
            if (_editBundle.DlcData != null && _editBundle.DlcData.Count > 0)
                SetTextBoxText(txtDLCList, DlcService.BuildDlcListText(_editBundle.DlcData));
            else if (txtDLCList != null && HasCurrentGameWithValidAppId())
                SetTextBoxText(txtDLCList, "No DLC found for this game.");

            if (!string.IsNullOrEmpty(sidecars?.SupportedLanguages))
                PopulateLanguageDropdown(sidecars.SupportedLanguages);
            else
                LoadSupportedLanguagesFromFile();

            if (_editBundle.SettingsSnapshot != null)
            {
                LoadSettingsToForm(_editBundle.SettingsSnapshot, loadPerGamePersistedGoldbergFiles: false);
            }

            if (sidecars != null)
            {
                SetSubscribedGroupLines(lstSubscribedGroups, sidecars.SubscribedGroups);
                SetSubscribedGroupLines(lstSubscribedGroupsClans, sidecars.SubscribedGroupsClans);

                ReloadAchievementsPreviewFromDisk();

                _customStatsRawJson = sidecars.CustomStatsJson ?? string.Empty;
                if (lblCustomStatsDisplay != null)
                {
                    lblCustomStatsDisplay.Text = ServiceLocator.GoldbergFilesService.FormatStatsForDisplay(_customStatsRawJson);
                    LayoutStatsDisplayLabel();
                }

                ApplyItemsJsonToInventoryUi(sidecars.ItemsJson ?? "{}");
            }

            RefreshModsSummaryList();
            _editSidecarsApplied = true;
        }

        private void ApplyAddBundlePreview()
        {
            if (_addBundle == null)
                return;

            if (!string.IsNullOrEmpty(_addBundle.AchievementsPreviewJson))
            {
                _achievementsRawJson = _addBundle.AchievementsPreviewJson;
                RefreshAchievementsPreview();
            }

            if (_addBundle.FormDefaults != null)
            {
                var snapshot = _addBundle.FormDefaults;
                if (_gameConfig != null && _gameConfig.AppId > 0)
                {
                    GameCredentialPersistenceService.ApplyRegistryFallbackForDisplay(
                        _gameConfig.AppId,
                        snapshot,
                        ServiceLocator.RegistryService);
                }

                LoadSettingsToForm(snapshot, loadPerGamePersistedGoldbergFiles: false);
            }
        }

        private GameSettingsSnapshot GetSettingsFromForm()
        {
            ulong appId = _gameConfig?.AppId ?? 0;
            if (txtAppID != null && ulong.TryParse(txtAppID.Text.Trim(), out ulong parsedAppId))
                appId = parsedAppId;

            var snapshot = _emulatorConfigService.LoadGameSettingsSnapshot(appId);

            if (_gameConfig != null)
            {
                BindTrim(txtGameName, v => _gameConfig.AppName = v);
                BindTrim(txtGameFolder, v => _gameConfig.StartFolder = v);
                BindTrim(txtGameExecutable, v => _gameConfig.Path = v);
                BindTrim(txtLaunchParameters, v => _gameConfig.Parameters = v);
                BindTrim(txtWorkingDirectory, v => _gameConfig.WorkingDirectory = v);
            }

            ApplyNetworkMainFromForm(snapshot.Main);
            ApplyStatsAchievementsMainFromForm(snapshot.Main);

            BindCheck(chkUnlockAllDLC, v => snapshot.App.UnlockAllDLC = v);
            BindCheck(chkBetaBranch, v => snapshot.App.IsBetaBranch = v);
            if (txtBetaBranchName != null)
                snapshot.App.BranchName = (chkBetaBranch != null && !chkBetaBranch.Checked) ? SteamPicsKeyNames.SteamDefaultBranchName : txtBetaBranchName.Text.Trim();

            BindPlaceholderTrim(txtForceAccountName, v => snapshot.User.AccountName = v);
            BindPlaceholderTrim(txtForceSteamId, v => snapshot.User.AccountSteamId = v);
            BindTrim(txtUserTicket, v => snapshot.User.Ticket = v);
            BindPlaceholderTrim(txtAltSteamId, v => snapshot.User.AltSteamId = v);
            BindNum(numAltSteamIdCount, v => snapshot.User.AltSteamIdCount = v);

            if (!string.IsNullOrEmpty(snapshot.User.Ticket) && snapshot.User.AltSteamIdCount <= 0)
                snapshot.User.AltSteamIdCount = 1;

            snapshot.User.Language = string.Empty;
            if (cmbForceLanguage != null && cmbForceLanguage.SelectedItem != null)
            {
                string selectedLang = cmbForceLanguage.SelectedItem.ToString();
                if (selectedLang != SteamLanguageDisplayHelper.UseGlobalSettingOption)
                    snapshot.User.Language = SteamLanguageDisplayHelper.ToLanguageCodeFromSimpleDisplay(selectedLang);
            }

            // LocalSavePath / SavesFolderName are global-only (see EmulatorConfigService.SaveUserSettings).
            snapshot.User.LocalSavePath = string.Empty;
            snapshot.User.SavesFolderName = string.Empty;
            BindTrim(txtForceIpCountry, v => snapshot.User.IpCountry = v);
            BindTrim(txtClanTag, v => snapshot.User.ClanTag = v);

            snapshot.AppId = appId;
            return snapshot;
        }

        private void ApplyNetworkMainFromForm(MainSettings main)
        {
            if (main == null)
                return;

            BindCheck(chkBlockUnknownClients, v => main.BlockUnknownClients = v);
            BindCheck(chkImmediateGameserverStats, v => main.ImmediateGameserverStats = v);
            BindCheck(chkMatchmakingServerListActualType, v => main.MatchmakingServerListActualType = v);
            BindCheck(chkMatchmakingServerDetailsViaSourceQuery, v => main.MatchmakingServerDetailsViaSourceQuery = v);
            BindCheck(chkDisableLanOnly, v => main.DisableLanOnly = v);
            BindCheck(chkDisableNetworking, v => main.DisableNetworking = v);
            BindNum(numForcePort, v => main.ListenPort = v);
            BindCheck(chkOffline, v => main.Offline = v);
            BindCheck(chkDisableSharingStatsWithGameserver, v => main.DisableSharingStatsWithGameserver = v);
            BindCheck(chkDisableSourceQuery, v => main.DisableSourceQuery = v);
            BindCheck(chkShareLeaderboardsOverNetwork, v => main.ShareLeaderboardsOverNetwork = v);
            BindCheck(chkDisableLobbyCreation, v => main.DisableLobbyCreation = v);
            BindCheck(chkDownloadSteamhttpRequests, v => main.DownloadSteamhttpRequests = v);
            BindNum(numOldP2PPacketSharingMode, v => main.OldP2PPacketSharingMode = v);
        }

        private void LoadNetworkMainToForm(MainSettings main)
        {
            if (main == null)
                return;

            LoadCheck(chkBlockUnknownClients, main.BlockUnknownClients);
            LoadCheck(chkImmediateGameserverStats, main.ImmediateGameserverStats);
            LoadCheck(chkMatchmakingServerListActualType, main.MatchmakingServerListActualType);
            LoadCheck(chkMatchmakingServerDetailsViaSourceQuery, main.MatchmakingServerDetailsViaSourceQuery);
            LoadCheck(chkDisableLanOnly, main.DisableLanOnly);
            LoadCheck(chkDisableNetworking, main.DisableNetworking);
            LoadNum(numForcePort, main.ListenPort);
            LoadCheck(chkOffline, main.Offline);
            LoadCheck(chkDisableSharingStatsWithGameserver, main.DisableSharingStatsWithGameserver);
            LoadCheck(chkDisableSourceQuery, main.DisableSourceQuery);
            LoadCheck(chkShareLeaderboardsOverNetwork, main.ShareLeaderboardsOverNetwork);
            LoadCheck(chkDisableLobbyCreation, main.DisableLobbyCreation);
            LoadCheck(chkDownloadSteamhttpRequests, main.DownloadSteamhttpRequests);
            LoadNum(numOldP2PPacketSharingMode, main.OldP2PPacketSharingMode);
        }

        private void ApplyStatsAchievementsMainFromForm(MainSettings main)
        {
            if (main == null)
                return;

            BindCheck(chkDisableLeaderboardsCreateUnknown, v => main.DisableLeaderboardsCreateUnknown = v);
            BindCheck(chkAllowUnknownStats, v => main.AllowUnknownStats = v);
            BindCheck(chkStatAchievementProgressFunctionality, v => main.StatAchievementProgressFunctionality = v);
            BindCheck(chkSaveOnlyHigherStatAchievementProgress, v => main.SaveOnlyHigherStatAchievementProgress = v);
            BindNum(numIconsPerIteration, v => main.PaginatedAchievementsIcons = v);
            BindCheck(chkRecordPlaytime, v => main.RecordPlaytime = v);
            BindCheck(chkAchievementsBypass, v => main.AchievementsBypass = v);
            if (txtSteamGameStatsReportsDir != null)
                main.SteamGameStatsReportsDir = txtSteamGameStatsReportsDir.Text.Trim();
        }

        private void LoadStatsAchievementsMainToForm(MainSettings main)
        {
            if (main == null)
                return;

            LoadCheck(chkDisableLeaderboardsCreateUnknown, main.DisableLeaderboardsCreateUnknown);
            LoadCheck(chkAllowUnknownStats, main.AllowUnknownStats);
            LoadCheck(chkStatAchievementProgressFunctionality, main.StatAchievementProgressFunctionality);
            LoadCheck(chkSaveOnlyHigherStatAchievementProgress, main.SaveOnlyHigherStatAchievementProgress);
            if (numIconsPerIteration != null)
            {
                int icons = main.PaginatedAchievementsIcons;
                if (icons < (int)numIconsPerIteration.Minimum)
                    icons = (int)numIconsPerIteration.Minimum;
                if (icons > (int)numIconsPerIteration.Maximum)
                    icons = (int)numIconsPerIteration.Maximum;
                LoadNum(numIconsPerIteration, icons);
            }
            LoadCheck(chkRecordPlaytime, main.RecordPlaytime);
            LoadCheck(chkAchievementsBypass, main.AchievementsBypass);
            LoadText(txtSteamGameStatsReportsDir, main.SteamGameStatsReportsDir);
        }

        private void LoadSettingsToForm(GameSettingsSnapshot snapshot, bool loadPerGamePersistedGoldbergFiles = true)
        {
            if (snapshot == null)
                return;

            LoadNetworkMainToForm(snapshot.Main);
            LoadStatsAchievementsMainToForm(snapshot.Main);

            LoadCheck(chkUnlockAllDLC, snapshot.App.UnlockAllDLC);
            LoadCheck(chkBetaBranch, snapshot.App.IsBetaBranch);
            LoadText(txtBetaBranchName, snapshot.App.BranchName ?? SteamPicsKeyNames.SteamDefaultBranchName);
            UpdateBetaBranchNameEnabled();

            if (loadPerGamePersistedGoldbergFiles)
                LoadDlcListAndAppPaths();

            if (chkUnlockAllDLC != null && chkUnlockAllDLC.Checked && txtDLCList != null)
            {
                txtDLCList.Enabled = false;
                ApplyDlcTextBoxTheme();
            }

            if (txtForceAccountName != null)
                _placeholderHelper.SetTextBoxValue(txtForceAccountName, snapshot.User.AccountName);
            if (txtForceSteamId != null)
                _placeholderHelper.SetTextBoxValue(txtForceSteamId, snapshot.User.AccountSteamId);
            LoadText(txtUserTicket, snapshot.User.Ticket);
            if (txtAltSteamId != null)
                _placeholderHelper.SetTextBoxValue(txtAltSteamId, snapshot.User.AltSteamId);
            LoadNum(numAltSteamIdCount, snapshot.User.AltSteamIdCount);

            if (cmbForceLanguage != null)
            {
                int idx = -1;
                if (!string.IsNullOrEmpty(snapshot.User.Language))
                    idx = cmbForceLanguage.Items.IndexOf(SteamLanguageDisplayHelper.ToSimpleDisplayName(snapshot.User.Language));
                if (idx < 0)
                    idx = cmbForceLanguage.Items.IndexOf(SteamLanguageDisplayHelper.UseGlobalSettingOption);
                if (idx >= 0)
                    cmbForceLanguage.SelectedIndex = idx;
                UpdateLanguageComboBoxPlaceholder();
            }

            LoadText(txtForceIpCountry, snapshot.User.IpCountry);
            LoadText(txtClanTag, snapshot.User.ClanTag);

            if (loadPerGamePersistedGoldbergFiles)
            {
                LoadAndDisplayStats();
                LoadAdditionalGoldbergFiles();
            }
            UpdateAltSteamIdPlaceholder();
        }

        private void InitializePlaceholders()
        {
            var g = LoadGlobalUserSettings();
            InitializeCredentialPlaceholders(g);
            InitializeLanguagePlaceholderFromGlobal(g);
        }

        private System.Drawing.Color GetThemeForegroundColor()
        {
            return _themeService != null
                ? _themeService.GetThemeColors(_themeService.EffectiveTheme).Foreground
                : System.Drawing.SystemColors.ControlText;
        }

        private void LoadDlcListAndAppPaths()
        {
            if (!HasCurrentGameWithValidAppId())
                return;

            try
            {
                var service = ServiceLocator.GoldbergFilesService;
                var appConfigData = service.LoadAppConfigDlcAndPaths(_gameConfig.AppId);
                var dlcDataFromFile = appConfigData.DlcData;

                // Populate DLC list textbox - use PreFetchedDlcData for proper names if available
                // This prevents depot-file names from overwriting proper Steam API names
                if (txtDLCList != null && dlcDataFromFile.Count > 0)
                {
                    SetTextBoxText(txtDLCList, DlcService.BuildDlcListTextWithPreferredNames(dlcDataFromFile, _gameConfig.PreFetchedDlcData));
                }
                else if (txtDLCList != null && dlcDataFromFile.Count == 0)
                {
                    // In edit mode, DlcCheckPerformed is runtime-only and not persisted.
                    // Show explicit no-DLC state when config has no DLC entries.
                    SetTextBoxText(txtDLCList, "No DLC found for this game.");
                }

            }
            catch (Exception ex)
            {
                LogFailedLoad("DLC list", ex);
            }
        }

        private void SaveDlcListAndAppPaths()
        {
            if (!HasCurrentGameWithValidAppId())
                return;

            try
            {
                // Extract DLC data from txtDLCList - user edits are preserved
                Dictionary<long, string> dlcData = txtDLCList != null
                    ? DlcService.ParseDlcListText(txtDLCList.Text, _gameConfig.PreFetchedDlcData)
                    : null;
                if (dlcData == null && _gameConfig.PreFetchedDlcData != null && _gameConfig.PreFetchedDlcData.Count > 0)
                    dlcData = _gameConfig.PreFetchedDlcData;

                var saveResult = ServiceLocator.GoldbergFilesService.SaveAppConfigDlcAndPaths(_gameConfig.AppId, dlcData, null);
                if (!saveResult.IsSuccess)
                {
                    LogSaveResultWarning(saveResult);
                }
            }
            catch (Exception ex)
            {
                LogFailedSave("DLC list", ex);
            }
        }

        private void OnLookupAppID_Click(object sender, EventArgs e)
        {
            using (var searchForm = new GameSearchForm())
            {
                if (searchForm.ShowDialog() == DialogResult.OK && searchForm.SelectedAppId.HasValue)
                {
                    if (txtAppID != null)
                        SetTextBoxText(txtAppID, searchForm.SelectedAppId.Value.ToString());
                    RequestRefreshSteamLaunchOptionsCombo();
                    // Trigger metadata fetch
                    _ = Task.Run(async () =>
                    {
                        await FetchMetadataForAppIdAsync(searchForm.SelectedAppId.Value.ToString());
                    }).ForgetFaults(Program.LogService, nameof(FetchMetadataForAppIdAsync));
                }
            }
        }

        private void OnLookupGameName_Click(object sender, EventArgs e)
        {
            if (txtAppID == null || string.IsNullOrEmpty(txtAppID.Text))
            {
                FormMessageBoxHelper.ShowIfAlive(this, "Please enter an App ID first.", "App ID Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!ulong.TryParse(txtAppID.Text.Trim(), out ulong appId) || appId == 0)
            {
                FormMessageBoxHelper.ShowIfAlive(this, "Please enter a valid App ID.", "Invalid App ID", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _ = Task.Run(async () =>
            {
                await FetchMetadataForAppIdAsync(appId.ToString());
            }).ForgetFaults(Program.LogService, nameof(FetchMetadataForAppIdAsync));
        }

        private async Task FetchMetadataForAppIdAsync(string appId)
        {
            try
            {
                var fetchResult = await ServiceLocator.GameSetupService
                    .FetchPicsMetadataWithRootAsync(appId, _gameConfig?.AppPicsKeyValue)
                    .ConfigureAwait(false);

                if (IsDisposed || Disposing)
                    return;

                if (InvokeRequired)
                    Invoke(new Action(() => ApplyFetchedAppMetadata(fetchResult.Metadata, fetchResult.PicsRoot)));
                else
                    ApplyFetchedAppMetadata(fetchResult.Metadata, fetchResult.PicsRoot);
            }
            catch (Exception ex)
            {
                LogErrorWithExceptionMessage("Error fetching metadata", ex);
            }
        }

        private void ApplyFetchedAppMetadata(OnlineAppData metadata, KeyValue picsRoot = null)
        {
            if (IsDisposed || Disposing)
                return;
            if (metadata == null)
                return;

            if (picsRoot != null && _gameConfig != null)
                _gameConfig.AppPicsKeyValue = picsRoot;

            if (_metadata == null)
                _metadata = metadata;
            else
            {
                if (!string.IsNullOrEmpty(metadata.Name))
                    _metadata.Name = metadata.Name;
                if (!string.IsNullOrEmpty(metadata.DataSources))
                    _metadata.DataSources = metadata.DataSources;
                if (!string.IsNullOrEmpty(metadata.InstallDir))
                    _metadata.InstallDir = metadata.InstallDir;
                if (!string.IsNullOrEmpty(metadata.SupportedLanguages))
                    _metadata.SupportedLanguages = metadata.SupportedLanguages;
                if (metadata.DlcIds != null && metadata.DlcIds.Count > 0)
                    _metadata.DlcIds = metadata.DlcIds;
                if (metadata.Success)
                    _metadata.Success = true;
            }

            if (txtGameName != null && !string.IsNullOrEmpty(metadata.Name))
                SetTextBoxText(txtGameName, metadata.Name);

            UpdateGameFolderInstallDirHintVisibility();
        }

        private string GetSteamInstallDirFolderNameForForm()
        {
            if (_metadata != null && !string.IsNullOrWhiteSpace(_metadata.InstallDir))
                return _metadata.InstallDir.Trim();
            if (_gameConfig?.AppPicsKeyValue != null && SteamPicsKeyValueHelper.TryGetSteamInstallDirFolderName(_gameConfig.AppPicsKeyValue, out string fromPics))
                return fromPics;
            return null;
        }

        private void UpdateGameFolderInstallDirHintVisibility()
        {
            if (lblHintGameFolder == null)
                return;

            string installDirFolder = GetSteamInstallDirFolderNameForForm();
            if (string.IsNullOrEmpty(installDirFolder))
            {
                lblHintGameFolder.Visible = true;
                return;
            }

            string exeText = txtGameExecutable?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(exeText))
            {
                lblHintGameFolder.Visible = true;
                return;
            }

            string folderText = txtGameFolder?.Text?.Trim() ?? string.Empty;
            if (!GameFolderPathHelper.TryResolveExecutableFromGameFolderFields(folderText, exeText, out string fullExe))
            {
                lblHintGameFolder.Visible = true;
                return;
            }

            bool installdirFound = GameFolderPathHelper.TrySplitExecutableAtSteamInstallDir(fullExe, installDirFolder, out _, out _);
            lblHintGameFolder.Visible = !installdirFound;
            if (lblHintGameFolder.Visible)
                lblHintGameFolder.BringToFront();
        }

        private void OnBrowseGameFolder_Click(object sender, EventArgs e)
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select Game Folder";
                folderDialog.ShowNewFolderButton = false;
                
                if (!string.IsNullOrEmpty(txtGameFolder?.Text))
                {
                    folderDialog.SelectedPath = txtGameFolder.Text;
                }

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    if (txtGameFolder != null)
                        SetTextBoxText(txtGameFolder, folderDialog.SelectedPath);
                    // Validate Steam API DLLs after folder selection
                    ValidateSteamApiDlls();
                    UpdateGameFolderInstallDirHintVisibility();
                }
            }
        }

        private void OnLauncherOptionsSteamDb_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ulong appId = 0;
            if (txtAppID != null && ulong.TryParse(txtAppID.Text.Trim(), out ulong parsed))
                appId = parsed;
            else if (_gameConfig != null)
                appId = _gameConfig.AppId;

            if (appId == 0)
            {
                FormMessageBoxHelper.ShowIfAlive(this, "Please select a game with a valid App ID.", "No Game Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string url = string.Format(ApplicationConstants.SteamDbConfigUrlFormat, appId);
            OpenSafeExternalUrl(url, "Failed to open SteamDB config page.", "Failed to open SteamDB config page");
        }

        private void OnSteamCmdLineOptionsValveWiki_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            const string url = ApplicationConstants.ValveSteamCommandLineOptionsUrl;
            OpenSafeExternalUrl(url, "Failed to open Valve wiki page.", "Failed to open Valve Steam command-line options page");
        }

        private void OpenSafeExternalUrl(string url, string userErrorMessage, string logErrorMessage)
        {
            if (!PathValidationHelper.IsSafeUrl(url))
            {
                FormMessageBoxHelper.ShowIfAlive(this, "Invalid URL format detected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                Process.Start(url);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError(logErrorMessage + ": " + ex.Message, ex);
                FormMessageBoxHelper.ShowIfAlive(this, userErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnBrowseGameExecutable_Click(object sender, EventArgs e)
        {
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Executable Files (*.exe;*.bat)|*.exe;*.bat|All Files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;
                openFileDialog.Title = "Select Game Executable";

                if (!string.IsNullOrEmpty(txtGameExecutable?.Text))
                {
                    if (GameFolderPathHelper.TryResolveExecutableDialogSeed(
                        txtGameFolder?.Text,
                        txtGameExecutable.Text,
                        out string initialDirectory,
                        out string fileName))
                    {
                        openFileDialog.InitialDirectory = initialDirectory;
                        openFileDialog.FileName = fileName;
                    }
                }

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string selected = Path.GetFullPath(openFileDialog.FileName);

                    string installDirFolder = GetSteamInstallDirFolderNameForForm();
                    if (!string.IsNullOrEmpty(installDirFolder) &&
                        GameFolderPathHelper.TrySplitExecutableAtSteamInstallDir(selected, installDirFolder, out string gameRootFromInstall, out string relativeExe))
                    {
                        if (txtGameFolder != null)
                            SetTextBoxText(txtGameFolder, gameRootFromInstall);
                        if (txtGameExecutable != null)
                            SetTextBoxText(txtGameExecutable, relativeExe);
                        ValidateSteamApiDlls();
                    }
                    else
                    {
                        // Auto-update game folder if empty
                        if (txtGameFolder != null && string.IsNullOrEmpty(txtGameFolder.Text))
                        {
                            SetTextBoxText(txtGameFolder, Path.GetDirectoryName(selected));
                            ValidateSteamApiDlls();
                        }

                        if (txtGameExecutable != null)
                        {
                            string gameBase = txtGameFolder?.Text?.Trim();
                            if (!string.IsNullOrEmpty(gameBase) && Directory.Exists(gameBase))
                            {
                                gameBase = Path.GetFullPath(gameBase);
                                SetTextBoxText(txtGameExecutable, PathValidationHelper.ToRelativePathOrFileNameOrOriginal(gameBase, selected));
                            }
                            else
                                SetTextBoxText(txtGameExecutable, selected);
                        }
                    }

                    UpdateGameFolderInstallDirHintVisibility();
                }
            }
        }

        private void OnBrowseWorkingDirectory_Click(object sender, EventArgs e)
        {
            string gameBase = GameFolderPathHelper.ResolveBaseFolderFromInputs(txtGameFolder?.Text, txtGameExecutable?.Text);
            if (string.IsNullOrEmpty(gameBase))
            {
                FormMessageBoxHelper.ShowIfAlive(this,
                    "Set the game folder or executable first before choosing a working directory.",
                    "Working directory",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select the folder to use as the working directory when launching (must be inside the game folder).";
                folderDialog.ShowNewFolderButton = false;

                string initial = PathValidationHelper.TryResolveWorkingDirectoryTextToFullPath(txtWorkingDirectory?.Text, gameBase);
                if (!string.IsNullOrEmpty(initial) && Directory.Exists(initial))
                    folderDialog.SelectedPath = initial;
                else
                    folderDialog.SelectedPath = gameBase;

                if (folderDialog.ShowDialog(this) != DialogResult.OK || txtWorkingDirectory == null)
                    return;

                string selected = folderDialog.SelectedPath;
                if (string.IsNullOrEmpty(selected))
                    return;

                if (!PathValidationHelper.TryMakePathRelativeToDirectory(gameBase, selected, out string relativeWorkingDir))
                {
                    FormMessageBoxHelper.ShowIfAlive(this,
                        "The selected folder must be inside the game folder (or the executable directory if no game folder is set).",
                        "Working directory",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                SetTextBoxText(txtWorkingDirectory, PathValidationHelper.ToRelativePathOrOriginal(gameBase, selected));
            }
        }


        private void NormalizeExecutableAndWorkingDirRelativeToGameFolder()
        {
            string gameBase = txtGameFolder?.Text?.Trim();
            if (string.IsNullOrEmpty(gameBase) || !Directory.Exists(gameBase))
                return;
            gameBase = Path.GetFullPath(gameBase);

            if (txtGameExecutable != null)
            {
                string p = txtGameExecutable.Text.Trim();
                if (!string.IsNullOrEmpty(p) && Path.IsPathRooted(p) && File.Exists(p))
                {
                    SetTextBoxText(txtGameExecutable, PathValidationHelper.ToRelativePathOrFileNameOrOriginal(gameBase, p));
                }
            }
            if (txtWorkingDirectory != null)
            {
                string w = txtWorkingDirectory.Text.Trim();
                if (!string.IsNullOrEmpty(w) && Path.IsPathRooted(w) && Directory.Exists(w))
                {
                    SetTextBoxText(txtWorkingDirectory, PathValidationHelper.ToRelativePathOrOriginal(gameBase, w));
                }
            }
        }

        private void OnBrowseCustomIcon_Click(object sender, EventArgs e)
        {
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Icon Files (*.exe;*.bat;*.ico)|*.exe;*.bat;*.ico|Executable Files (*.exe;*.bat)|*.exe;*.bat|Icon Files (*.ico)|*.ico|All Files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;
                openFileDialog.Title = "Select Custom Icon";

                if (!string.IsNullOrEmpty(txtCustomIcon?.Text))
                {
                    openFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(txtCustomIcon.Text);
                    openFileDialog.FileName = System.IO.Path.GetFileName(txtCustomIcon.Text);
                }

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    if (txtCustomIcon != null)
                        SetTextBoxText(txtCustomIcon, openFileDialog.FileName);
                }
            }
        }

        private void OnClearCustomIcon_Click(object sender, EventArgs e)
        {
            ClearTextBox(txtCustomIcon);
        }

        private async void OnFindDLCs_Click(object sender, EventArgs e)
        {
            if (!HasCurrentGameWithValidAppId())
            {
                FormMessageBoxHelper.ShowIfAlive(this, "App ID is required to find DLCs.", "App ID Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                btnFindDLCs.Enabled = false;
                btnFindDLCs.Text = "Searching...";

                // Clear the DLC list at the start
                if (txtDLCList != null)
                {
                    ClearTextBox(txtDLCList);
                }

                var dlcData = await _dlcService.GetDlcDataAsync(
                    _gameConfig.AppId.ToString(),
                    existingDlcData: null,
                    picsAppRoot: _gameConfig.AppPicsKeyValue);

                if (IsDisposed || Disposing)
                    return;

                // Mark that DLC check has been performed
                _gameConfig.DlcCheckPerformed = true;

                if (dlcData != null && dlcData.Count > 0)
                {
                    // Update GameConfig with DLC data
                    if (_gameConfig.PreFetchedDlcData == null)
                    {
                        _gameConfig.PreFetchedDlcData = new Dictionary<long, string>();
                    }
                    foreach (var kvp in dlcData)
                    {
                        _gameConfig.PreFetchedDlcData[kvp.Key] = kvp.Value;
                    }

                    // Populate DLC list textbox
                    if (txtDLCList != null)
                        SetTextBoxText(txtDLCList, DlcService.BuildDlcListText(dlcData));

                    // Note: Metadata files (including installed_app_ids.txt and supported_languages.txt) 
                    // are only generated when adding a new game, not when editing

                }
                else
                {
                    // Show message in the list when no DLC is found
                    if (txtDLCList != null)
                    {
                        SetTextBoxText(txtDLCList, "No DLC found for this game.");
                    }
                }
            }
            catch (Exception ex)
            {
                LogAndShowErrorWithExceptionMessage("Error finding DLCs", ex);
                
                // Show error message in the list as well
                if (txtDLCList != null)
                {
                    SetTextBoxText(txtDLCList, "Error: " + ex.Message);
                }
            }
            finally
            {
                if (!IsDisposed && !Disposing)
                {
                    btnFindDLCs.Enabled = true;
                    btnFindDLCs.Text = "Find DLCs";
                }
            }
        }

        private void OnBrowseSteamGameStatsReportsDir_Click(object sender, EventArgs e)
        {
            BrowseFolderIntoTextBox(txtSteamGameStatsReportsDir, "Select Steam Game Stats Reports Directory", true);
        }

        private void BrowseFolderIntoTextBox(TextBox targetTextBox, string description, bool showNewFolderButton)
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = description;
                folderDialog.ShowNewFolderButton = showNewFolderButton;
                if (!string.IsNullOrEmpty(targetTextBox?.Text))
                {
                    folderDialog.SelectedPath = targetTextBox.Text;
                }
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    if (targetTextBox != null)
                        targetTextBox.Text = folderDialog.SelectedPath;
                }
            }
        }

        private void OnRefreshAchievements_Click(object sender, EventArgs e)
        {
            if (!HasCurrentGameWithValidAppId())
                return;

            try
            {
                ReloadAchievementsPreviewFromDisk();
                if (txtAchievementsFilter != null)
                    ClearTextBox(txtAchievementsFilter);
            }
            catch (Exception ex)
            {
                LogAndShowErrorWithExceptionMessage($"Failed to refresh {AchievementConstants.AchievementsFileName}", ex);
            }
        }

        private void ReloadAchievementsPreviewFromDisk()
        {
            if (!HasCurrentGameWithValidAppId())
                return;

            _achievementsRawJson = ServiceLocator.GoldbergFilesService.LoadAchievements(_gameConfig.AppId) ?? string.Empty;
            RefreshAchievementsPreview();
        }

        private void ClearTextBox(TextBox textBox)
        {
            if (textBox != null)
                textBox.Text = string.Empty;
        }

        private void LogAndShowErrorWithExceptionMessage(string messagePrefix, Exception ex)
        {
            Program.LogService?.LogError(messagePrefix + ": " + ex.Message, ex);
            FormMessageBoxHelper.ShowIfAlive(this, messagePrefix + ": " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void ApplyTheme()
        {
            if (_themeService != null)
            {
                _themeService.ApplyTheme(this);
                // Always apply DLC textbox theme (handles both enabled and disabled states)
                ApplyDlcTextBoxTheme();
                ApplyAchievementsPreviewTheme();
                ApplyModsTabTheme();
                ApplyInventoryTabTheme();
                UpdateGameFolderInstallDirHintVisibility();
            }
        }

        private void ThemeService_ThemeChanged(object sender, ThemeChangedEventArgs e)
        {
            if (IsDisposed || Disposing)
                return;
            if (InvokeRequired)
            {
                Invoke(new Action(() => 
                {
                    ApplyTheme();
                    UpdatePlaceholderTextColors();
                    UpdateLanguageComboBoxPlaceholder();
                }));
            }
            else
            {
                ApplyTheme();
                UpdatePlaceholderTextColors();
                UpdateLanguageComboBoxPlaceholder();
            }
        }

        private void UpdatePlaceholderTextColors()
        {
            _placeholderHelper?.UpdatePlaceholderColors();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            CancelModsListResolve();
            StopAndDisposeRestoreMessageHideTimer();
            if (_themeService != null)
            {
                _themeService.ThemeChanged -= ThemeService_ThemeChanged;
            }
            base.OnFormClosed(e);
        }

        private void SetTextBoxText(TextBox textBox, string value)
        {
            if (textBox != null)
                textBox.Text = value ?? string.Empty;
        }

        private static string GetTextOrNull(TextBox textBox)
        {
            return textBox != null ? textBox.Text : null;
        }

        private void BindCheck(CheckBox c, Action<bool> setter)
        {
            if (c != null)
                setter(c.Checked);
        }

        private void BindNum(NumericUpDown n, Action<int> setter)
        {
            if (n != null)
                setter((int)n.Value);
        }

        private void BindTrim(TextBox t, Action<string> setter)
        {
            if (t != null)
                setter(t.Text.Trim());
        }

        private void BindPlaceholderTrim(TextBox t, Action<string> setter)
        {
            if (t != null)
                setter(_placeholderHelper.GetActualText(t).Trim());
        }

        private void LoadCheck(CheckBox c, bool value)
        {
            if (c != null)
                c.Checked = value;
        }

        private void LoadNum(NumericUpDown n, int value)
        {
            if (n != null)
                n.Value = value;
        }

        private void LoadText(TextBox t, string value)
        {
            SetTextBoxText(t, value);
        }

        private void LogFailedLoad(string subject, Exception ex)
        {
            Program.LogService?.LogError("Failed to load " + subject + ": " + ex.Message, ex);
        }

        private void LogFailedSave(string subject, Exception ex)
        {
            Program.LogService?.LogError("Failed to save " + subject + ": " + ex.Message, ex);
        }

        private void LogWarningWithExceptionMessage(string messagePrefix, Exception ex)
        {
            Program.LogService?.LogWarning(messagePrefix + ": " + ex.Message);
        }

        private void LogWarningWithDetail(string messagePrefix, string detail)
        {
            Program.LogService?.LogWarning(messagePrefix + ": " + detail);
        }

        private void LogErrorWithExceptionMessage(string messagePrefix, Exception ex)
        {
            Program.LogService?.LogError(messagePrefix + ": " + ex.Message, ex);
        }

        private void LogSaveResultWarning(SaveResult saveResult, string prefix = null)
        {
            if (string.IsNullOrEmpty(prefix))
                Program.LogService?.LogWarning(saveResult.ErrorMessage);
            else
                Program.LogService?.LogWarning(prefix + ": " + saveResult.ErrorMessage);
        }

        private bool HasCurrentGameWithValidAppId()
        {
            return _gameConfig != null && _gameConfig.AppId != 0;
        }

        private void LogAndShowModsWarning(string logPrefix, string dialogTitle, Exception ex)
        {
            Program.LogService?.LogError(logPrefix + ": " + ex.Message, ex);
            FormMessageBoxHelper.ShowIfAlive(this, ex.Message, dialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void TxtGameFolder_TextChanged(object sender, EventArgs e)
        {
            // Debounce validation to avoid excessive calls while typing
            // Only validate if text is not empty and looks like a valid path
            string folder = txtGameFolder?.Text?.Trim();
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                ValidateSteamApiDlls();
            }

            if (!_isLoading)
                UpdateGameFolderInstallDirHintVisibility();
        }

        private void TxtGameExecutable_TextChanged(object sender, EventArgs e)
        {
            if (!_isLoading)
                UpdateGameFolderInstallDirHintVisibility();
        }

        private void WireTextChanged(TextBox tb)
        {
            if (tb != null)
                tb.TextChanged += Control_Changed;
        }

        private void WireChecked(CheckBox chk)
        {
            if (chk != null)
                chk.CheckedChanged += Control_Changed;
        }

        private void WireValueChanged(NumericUpDown nud)
        {
            if (nud != null)
                nud.ValueChanged += Control_Changed;
        }

        private void WireCheckedWithSecondary(CheckBox chk, EventHandler secondary)
        {
            if (chk == null)
                return;
            chk.CheckedChanged += Control_Changed;
            chk.CheckedChanged += secondary;
        }

        private void WireUpChangeEvents()
        {
            foreach (var tb in new[]
            {
                txtAppID, txtGameName, txtGameFolder, txtGameExecutable, txtLaunchParameters, txtWorkingDirectory, txtCustomIcon,
                txtForceAccountName, txtForceSteamId, txtUserTicket, txtAltSteamId, txtForceIpCountry,
                txtClanTag, txtBetaBranchName, txtDLCList, txtInventoryRaw
            })
                WireTextChanged(tb);

            foreach (var chk in new[]
            {
                chkBlockUnknownClients,
                chkImmediateGameserverStats, chkMatchmakingServerListActualType, chkMatchmakingServerDetailsViaSourceQuery,
                chkDisableLanOnly, chkDisableNetworking, chkOffline,
                chkDisableSharingStatsWithGameserver, chkDisableSourceQuery, chkShareLeaderboardsOverNetwork,
                chkDisableLobbyCreation, chkDownloadSteamhttpRequests
            })
                WireChecked(chk);

            foreach (var nud in new[] { numForcePort, numOldP2PPacketSharingMode, numAltSteamIdCount })
                WireValueChanged(nud);

            WireCheckedWithSecondary(chkUnlockAllDLC, ChkUnlockAllDLC_CheckedChanged);
            WireCheckedWithSecondary(chkBetaBranch, ChkBetaBranch_CheckedChanged);
            WireLaunchModeRadio(rdoLaunchSteamClient);
            WireLaunchModeRadio(rdoLaunchExperimentalMode);
            WireLaunchModeRadio(rdoLaunchSteamDll);
            WireLaunchModeRadio(rdoLaunchNoEmulation);

            if (tabMods != null)
                tabMods.Enter += TabMods_Enter;
            if (btnReloadInventoryFromDisk != null)
                btnReloadInventoryFromDisk.Click += OnReloadInventoryFromDisk_Click;
        }

        private void ChkUnlockAllDLC_CheckedChanged(object sender, EventArgs e)
        {
            if (chkUnlockAllDLC == null || txtDLCList == null)
                return;

            txtDLCList.Enabled = !chkUnlockAllDLC.Checked;
            ApplyDlcTextBoxTheme();
        }

        private void UpdateBetaBranchNameEnabled()
        {
            if (chkBetaBranch == null || txtBetaBranchName == null)
                return;
            txtBetaBranchName.Enabled = chkBetaBranch.Checked;
            if (!chkBetaBranch.Checked)
                SetTextBoxText(txtBetaBranchName, SteamPicsKeyNames.SteamDefaultBranchName);
        }

        private void ChkBetaBranch_CheckedChanged(object sender, EventArgs e)
        {
            UpdateBetaBranchNameEnabled();
        }

        private void ApplyDlcTextBoxTheme()
        {
            if (txtDLCList == null || _themeService == null)
                return;

            var colors = _themeService.GetThemeColors(_themeService.EffectiveTheme);

            if (!txtDLCList.Enabled)
            {
                txtDLCList.BackColor = colors.DisabledBackground;
                txtDLCList.ForeColor = colors.DisabledForeground;
            }
            else
            {
                txtDLCList.BackColor = colors.FieldBackground;
                txtDLCList.ForeColor = colors.Foreground;
            }
        }

        private List<AchievementPreviewModel> _achievementsPreviewListCache;
        private HashSet<string> _achievementsPreviewRevealedSecrets;
        private bool _achievementsPreviewSortApplied;
        private int _achievementsPreviewSortColumn;
        private bool _achievementsPreviewSortAscending = true;
        private const string AchievementsPreviewColumnAchievement = "Achievement";
        private const string AchievementsPreviewColumnDescription = "Description";
        private bool _balancingAchievementsColumns;

        private sealed class AchievementPreviewModel
        {
            public string Name { get; set; }
            public string DisplayName { get; set; }
            public string Description { get; set; }
            public string IconPath { get; set; }
            public string GrayIconPath { get; set; }
            public bool IsUnlocked { get; set; }
            public bool IsHidden { get; set; }
        }

        private void RefreshAchievementsPreview()
        {
            if (lstAchievementsPreview == null || imgAchievementsPreview == null)
                return;

            _achievementsPreviewListCache = null;
            if (_achievementsPreviewRevealedSecrets != null)
                _achievementsPreviewRevealedSecrets.Clear();
            else
                _achievementsPreviewRevealedSecrets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            _achievementsPreviewSortApplied = false;
            _achievementsPreviewSortColumn = 0;

            var achievements = _achievementService.ParseAchievementPreviewData(_achievementsRawJson)
                .Select(a => new AchievementPreviewModel
                {
                    Name = a.Name,
                    DisplayName = a.DisplayName,
                    Description = a.Description,
                    IconPath = a.IconPath,
                    GrayIconPath = a.GrayIconPath,
                    IsUnlocked = a.IsUnlocked,
                    IsHidden = a.IsHidden
                })
                .ToList();
            if (achievements.Count == 0)
            {
                RebuildAchievementsPreviewItems();
                return;
            }

            var serviceModels = achievements.Select(a => AchievementService.ToPreviewData(
                a.Name,
                a.DisplayName,
                a.Description,
                a.IconPath,
                a.GrayIconPath,
                a.IsUnlocked,
                a.IsHidden)).ToList();
            _achievementService.ApplyUserUnlockStateFromSaves(serviceModels, _gameConfig.AppId);
            for (int i = 0; i < achievements.Count && i < serviceModels.Count; i++)
                achievements[i].IsUnlocked = serviceModels[i].IsUnlocked;
            _achievementsPreviewListCache = achievements;
            RebuildAchievementsPreviewItems();
        }

        private void TxtAchievementsFilter_TextChanged(object sender, EventArgs e)
        {
            RebuildAchievementsPreviewItems();
        }

        private void LstAchievementsPreview_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            const int columnCount = 2;
            if (e.Column < 0 || e.Column >= columnCount)
                return;

            if (_achievementsPreviewSortApplied && _achievementsPreviewSortColumn == e.Column)
                _achievementsPreviewSortAscending = !_achievementsPreviewSortAscending;
            else
            {
                _achievementsPreviewSortColumn = e.Column;
                _achievementsPreviewSortAscending = true;
            }

            _achievementsPreviewSortApplied = true;
            RebuildAchievementsPreviewItems();
        }

        private void UpdateAchievementPreviewColumnHeaders()
        {
            if (lstAchievementsPreview == null || lstAchievementsPreview.Columns.Count < 2)
                return;

            string nameCol = AchievementsPreviewColumnAchievement;
            string descCol = AchievementsPreviewColumnDescription;
            if (_achievementsPreviewSortApplied)
            {
                if (_achievementsPreviewSortColumn == 0)
                    nameCol += _achievementsPreviewSortAscending ? " â–²" : " â–¼";
                else
                    descCol += _achievementsPreviewSortAscending ? " â–²" : " â–¼";
            }

            lstAchievementsPreview.Columns[0].Text = nameCol;
            lstAchievementsPreview.Columns[1].Text = descCol;
        }

        private bool AchievementPreviewMatchesFilter(AchievementPreviewModel m, int index, string filter)
        {
            if (string.IsNullOrEmpty(filter))
                return true;

            if (!string.IsNullOrEmpty(m.Name) && m.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (!string.IsNullOrEmpty(m.DisplayName) && m.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (!string.IsNullOrEmpty(m.Description) && m.Description.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            var preview = _achievementService.BuildAchievementPreviewText(
                AchievementService.ToPreviewData(m.Name, m.DisplayName, m.Description, m.IconPath, m.GrayIconPath, m.IsUnlocked, m.IsHidden),
                index,
                _achievementsPreviewRevealedSecrets);
            string title = preview.Title;
            string desc = preview.Description;
            if (!string.IsNullOrEmpty(title) && title.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (!string.IsNullOrEmpty(desc) && desc.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        private int CompareAchievementPreviewIndices(int a, int b)
        {
            if (_achievementsPreviewListCache == null)
                return 0;

            if (!_achievementsPreviewSortApplied)
                return a.CompareTo(b);

            var ca = _achievementsPreviewListCache[a];
            var cb = _achievementsPreviewListCache[b];
            var caData = ToAchievementPreviewData(ca);
            var cbData = ToAchievementPreviewData(cb);
            int cmp;
            if (_achievementsPreviewSortColumn == 0)
            {
                cmp = string.Compare(
                    AchievementService.GetPreviewTitle(caData),
                    AchievementService.GetPreviewTitle(cbData),
                    StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                string da = _achievementService.BuildAchievementPreviewText(
                    caData,
                    a,
                    _achievementsPreviewRevealedSecrets).Description;
                string db = _achievementService.BuildAchievementPreviewText(
                    cbData,
                    b,
                    _achievementsPreviewRevealedSecrets).Description;
                cmp = string.Compare(da ?? string.Empty, db ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            }

            if (cmp != 0)
                return _achievementsPreviewSortAscending ? cmp : -cmp;

            return a.CompareTo(b);
        }

        private void RebuildAchievementsPreviewItems()
        {
            if (lstAchievementsPreview == null || imgAchievementsPreview == null)
                return;

            UpdateAchievementPreviewColumnHeaders();

            if (_achievementsPreviewListCache == null || _achievementsPreviewListCache.Count == 0)
            {
                lstAchievementsPreview.BeginUpdate();
                try
                {
                    lstAchievementsPreview.Items.Clear();
                    imgAchievementsPreview.Images.Clear();
                }
                finally
                {
                    lstAchievementsPreview.EndUpdate();
                }
                return;
            }

            string filter = txtAchievementsFilter != null ? txtAchievementsFilter.Text.Trim() : string.Empty;
            var indices = new List<int>(_achievementsPreviewListCache.Count);
            for (int i = 0; i < _achievementsPreviewListCache.Count; i++)
            {
                if (AchievementPreviewMatchesFilter(_achievementsPreviewListCache[i], i, filter))
                    indices.Add(i);
            }

            indices.Sort(CompareAchievementPreviewIndices);

            string steamSettingsPath = _emulatorConfigService.GetGameSteamSettingsPath(_gameConfig.AppId);
            lstAchievementsPreview.BeginUpdate();
            try
            {
                lstAchievementsPreview.Items.Clear();
                imgAchievementsPreview.Images.Clear();

                int imageIndex = 0;
                foreach (int srcIdx in indices)
                {
                    var achievement = _achievementsPreviewListCache[srcIdx];
                    var iconPath = achievement.IsUnlocked ? achievement.IconPath : achievement.GrayIconPath;
                    if (string.IsNullOrWhiteSpace(iconPath))
                        iconPath = achievement.IconPath;

                    imgAchievementsPreview.Images.Add(_achievementService.LoadAchievementPreviewIcon(steamSettingsPath, iconPath, imgAchievementsPreview.ImageSize));

                    var preview = _achievementService.BuildAchievementPreviewText(
                        ToAchievementPreviewData(achievement),
                        srcIdx,
                        _achievementsPreviewRevealedSecrets);
                    string title = preview.Title;
                    string description = preview.Description;

                    var item = new ListViewItem(title, imageIndex);
                    item.SubItems.Add(description);
                    item.Tag = srcIdx;
                    item.ToolTipText = preview.Tooltip;
                    lstAchievementsPreview.Items.Add(item);
                    imageIndex++;
                }

                AutoResizeAchievementsColumns();
            }
            catch (Exception ex)
            {
                LogWarningWithExceptionMessage("Failed to render achievements preview", ex);
            }
            finally
            {
                lstAchievementsPreview.EndUpdate();
            }
        }

        private void LstAchievementsPreview_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (!e.IsSelected || lstAchievementsPreview == null)
                return;

            TryRevealSelectedAchievementPreviewSecret(e.Item);
        }

        private void TryRevealSelectedAchievementPreviewSecret(ListViewItem item)
        {
            if (item == null || _achievementsPreviewListCache == null || _achievementsPreviewRevealedSecrets == null)
                return;

            if (!(item.Tag is int idx) || idx < 0 || idx >= _achievementsPreviewListCache.Count)
                return;

            var m = _achievementsPreviewListCache[idx];
            if (!m.IsHidden || !m.IsUnlocked)
                return;

            var currentPreview = _achievementService.BuildAchievementPreviewText(
                ToAchievementPreviewData(m),
                idx,
                _achievementsPreviewRevealedSecrets);
            string key = currentPreview.RevealKey;
            if (_achievementsPreviewRevealedSecrets.Contains(key))
                return;

            _achievementsPreviewRevealedSecrets.Add(key);
            var revealedPreview = _achievementService.BuildAchievementPreviewText(
                ToAchievementPreviewData(m),
                idx,
                _achievementsPreviewRevealedSecrets);
            string title = revealedPreview.Title;
            string description = revealedPreview.Description;
            item.Text = title;
            if (item.SubItems.Count > 1)
                item.SubItems[1].Text = description;
            item.ToolTipText = revealedPreview.Tooltip;
            AutoResizeAchievementsColumns();
        }

        private static AchievementPreviewData ToAchievementPreviewData(AchievementPreviewModel model)
        {
            if (model == null)
                return null;
            return AchievementService.ToPreviewData(
                model.Name,
                model.DisplayName,
                model.Description,
                model.IconPath,
                model.GrayIconPath,
                model.IsUnlocked,
                model.IsHidden);
        }

        private int GetAchievementsListColumnSpanWidth()
        {
            if (lstAchievementsPreview == null)
                return 0;

            int w = lstAchievementsPreview.ClientSize.Width;
            if (w < 1)
                return 120;

            if (lstAchievementsPreview.Items.Count > 0 && lstAchievementsPreview.IsHandleCreated)
            {
                try
                {
                    int hItem = lstAchievementsPreview.GetItemRect(0).Height;
                    if (hItem > 0)
                    {
                        int itemsHeight = hItem * lstAchievementsPreview.Items.Count;
                        if (lstAchievementsPreview.ClientSize.Height < itemsHeight)
                            w -= SystemInformation.VerticalScrollBarWidth;
                    }
                }
                catch (ArgumentException)
                {
                    // Item rect not available; use full client width.
                }
            }

            return Math.Max(120, w + 17);
        }

        private void SetAchievementsColumnWidthsInternal(int w0, int w1)
        {
            if (lstAchievementsPreview == null || lstAchievementsPreview.Columns.Count < 2)
                return;

            _balancingAchievementsColumns = true;
            try
            {
                if (lstAchievementsPreview.Columns[0].Width != w0)
                    lstAchievementsPreview.Columns[0].Width = w0;
                if (lstAchievementsPreview.Columns[1].Width != w1)
                    lstAchievementsPreview.Columns[1].Width = w1;
            }
            finally
            {
                _balancingAchievementsColumns = false;
            }
        }

        private void AutoResizeAchievementsColumns()
        {
            if (lstAchievementsPreview == null || lstAchievementsPreview.Columns.Count < 2)
                return;

            const int minW0 = 60;
            const int minW1 = 60;
            int total = GetAchievementsListColumnSpanWidth();
            if (total < minW0 + minW1)
                total = minW0 + minW1;

            int w0 = lstAchievementsPreview.Columns[0].Width;
            int w1 = total - w0;
            if (w1 < minW1)
            {
                w1 = minW1;
                w0 = total - w1;
            }
            if (w0 < minW0)
            {
                w0 = minW0;
                w1 = total - w0;
            }

            SetAchievementsColumnWidthsInternal(w0, w1);
        }

        private void RebalanceAchievementsColumnsAfterUserDrag(int resizedColumnIndex)
        {
            if (lstAchievementsPreview == null || lstAchievementsPreview.Columns.Count < 2)
                return;

            const int minW0 = 60;
            const int minW1 = 60;
            int total = GetAchievementsListColumnSpanWidth();
            if (total < minW0 + minW1)
                total = minW0 + minW1;

            int w0;
            int w1;
            if (resizedColumnIndex == 0)
            {
                w0 = lstAchievementsPreview.Columns[0].Width;
                w0 = Math.Max(minW0, Math.Min(w0, total - minW1));
                w1 = total - w0;
            }
            else
            {
                w1 = lstAchievementsPreview.Columns[1].Width;
                w1 = Math.Max(minW1, Math.Min(w1, total - minW0));
                w0 = total - w1;
            }

            SetAchievementsColumnWidthsInternal(w0, w1);
        }

        private void LstAchievementsPreview_ColumnWidthChanging(object sender, ColumnWidthChangingEventArgs e)
        {
            if (_balancingAchievementsColumns)
                return;
            if (e.ColumnIndex != 0 && e.ColumnIndex != 1)
                return;

            const int minW0 = 60;
            const int minW1 = 60;
            int total = GetAchievementsListColumnSpanWidth();
            if (total < minW0 + minW1)
                total = minW0 + minW1;

            if (e.ColumnIndex == 0)
            {
                if (e.NewWidth < minW0)
                    e.NewWidth = minW0;
                else if (e.NewWidth > total - minW1)
                    e.NewWidth = total - minW1;
            }
            else
            {
                if (e.NewWidth < minW1)
                    e.NewWidth = minW1;
                else if (e.NewWidth > total - minW0)
                    e.NewWidth = total - minW0;
            }
        }

        private void LstAchievementsPreview_ColumnWidthChanged(object sender, ColumnWidthChangedEventArgs e)
        {
            if (_balancingAchievementsColumns)
                return;
            if (e.ColumnIndex != 0 && e.ColumnIndex != 1)
                return;

            RebalanceAchievementsColumnsAfterUserDrag(e.ColumnIndex);
        }

        private void ApplyAchievementsPreviewTheme()
        {
            if (_themeService == null)
                return;

            var colors = _themeService.GetThemeColors(_themeService.EffectiveTheme);

            if (lblAchievementsPreview != null)
            {
                lblAchievementsPreview.ForeColor = colors.Foreground;
                lblAchievementsPreview.BackColor = colors.ControlBackground;
            }

            if (lblAchievementsFilter != null)
            {
                lblAchievementsFilter.ForeColor = colors.Foreground;
                lblAchievementsFilter.BackColor = colors.ControlBackground;
            }

            if (txtAchievementsFilter != null)
            {
                txtAchievementsFilter.ForeColor = colors.Foreground;
                txtAchievementsFilter.BackColor = colors.FieldBackground;
            }

            if (lstAchievementsPreview != null)
            {
                lstAchievementsPreview.BackColor = colors.ListViewBackground;
                lstAchievementsPreview.ForeColor = colors.ListViewForeground;
                lstAchievementsPreview.Invalidate();
            }
        }

        private void ApplyModsTabTheme()
        {
            if (_themeService == null)
                return;

            var colors = _themeService.GetThemeColors(_themeService.EffectiveTheme);

            if (lblModsHint != null)
            {
                lblModsHint.ForeColor = colors.Foreground;
                lblModsHint.BackColor = colors.ControlBackground;
            }

            if (grpMods != null)
                grpMods.BackColor = colors.ControlBackground;

            if (lstModsSummary != null)
            {
                lstModsSummary.BackColor = colors.ListViewBackground;
                lstModsSummary.ForeColor = colors.ListViewForeground;
                lstModsSummary.BorderStyle = BorderStyle.FixedSingle;
                lstModsSummary.Invalidate();
            }

            if (pnlModsToolbar != null)
            {
                pnlModsToolbar.BackColor = colors.ControlBackground;
                pnlModsToolbar.ForeColor = colors.ControlForeground;
            }
        }

        private void ApplyInventoryTabTheme()
        {
            if (_themeService == null)
                return;

            var colors = _themeService.GetThemeColors(_themeService.EffectiveTheme);

            if (lblInventoryHint != null)
            {
                lblInventoryHint.ForeColor = colors.Foreground;
                lblInventoryHint.BackColor = colors.ControlBackground;
            }

            if (grpInventoryEditor != null)
                grpInventoryEditor.BackColor = colors.ControlBackground;

            if (pnlInventoryButtons != null)
            {
                pnlInventoryButtons.BackColor = colors.ControlBackground;
                pnlInventoryButtons.ForeColor = colors.ControlForeground;
            }

            if (txtInventoryRaw != null)
            {
                if (txtInventoryRaw.Enabled)
                {
                    txtInventoryRaw.BackColor = colors.FieldBackground;
                    txtInventoryRaw.ForeColor = colors.Foreground;
                }
                else
                {
                    txtInventoryRaw.BackColor = colors.DisabledBackground;
                    txtInventoryRaw.ForeColor = colors.DisabledForeground;
                }

                txtInventoryRaw.BorderStyle = BorderStyle.FixedSingle;
            }

            if (lstInventoryItems != null)
            {
                lstInventoryItems.BackColor = colors.ListViewBackground;
                lstInventoryItems.ForeColor = colors.ListViewForeground;
                if (_inventoryInlineEditor != null && _inventoryInlineEditor.Visible)
                {
                    _inventoryInlineEditor.BackColor = colors.FieldBackground;
                    _inventoryInlineEditor.ForeColor = colors.Foreground;
                }
            }
        }

        private void LstAchievementsPreview_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            ListViewColumnHelper.DrawThemedColumnHeader(e, _themeService, null);
        }

        private void LstAchievementsPreview_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void LstAchievementsPreview_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void LstAchievementsPreview_SizeChanged(object sender, EventArgs e)
        {
            AutoResizeAchievementsColumns();
        }

        private Models.SteamApiStatus _currentApiStatus;
        private DateTime _restoreMessageVisibleUntilUtc = DateTime.MinValue;
        private System.Windows.Forms.Timer _restoreMessageHideTimer;

        private const string SteamApiDisplayCheckMark = "\u2714\uFE0F";
        private const string SteamApiDisplayQuestionMark = "\u2753";
        private const string SteamApiDisplayWarningMark = "\u26A0\uFE0F";
        private const string SteamApiMessageSuccessPrefix = "\u2713";
        private const string SteamApiMessageErrorPrefix = "\u274C";
        private const int SteamApiStatusRowGap = 2;
        private const int LaunchModeToSteamApiGap = 6;
        private const int SteamApiBlockGap = 4;
        private const int SteamApiHealthMaxLinesWhenNoStatus = 2;
        private const string SteamApiHealthNoDllsFoundHeadline =
            SteamApiMessageErrorPrefix + " No Steamworks API file was found (steam_api.dll / steam_api64.dll).";
        private const string SteamApiHealthNoDllsFoundNote =
            "Some games may require the Steam.dll launch mode; others may not require the use of Steamworks at all.";
        private const string SteamApiHealthGoodHeadline = "Valid Steamworks DLLs found.";
        private const string SteamApiHealthModifiedHeadline = "No valid Steamworks DLLs found.";
        private const string SteamApiHealthValidBackupFoundFormat = "Valid {0} ({1}) found.";
        private const string SteamApiHealthValidBackupFoundNoVersionFormat = "Valid Steamworks DLL ({0}) found.";
        private const string SteamApiStatusValidUnknownVersion =
            "Valid Steamworks DLL, unknown version ({0})";
        private const string SteamApiStatusValidKnownHash =
            "Valid Steamworks DLL ({0})";
        private const string SteamApiStatusModified =
            "Modified Steamworks DLL ({0})";
        private const string SteamApiRestoreOpApplied =
            SteamApiMessageSuccessPrefix + " Valid Steamworks DLLs were restored.";
        private const string SteamApiRestoreOpNoBackups =
            SteamApiMessageErrorPrefix + " No matching valid backup DLLs were found.";
        private const string SteamApiRestoreOpNoMatchBackup =
            SteamApiMessageErrorPrefix + " Could not restore valid Steamworks DLLs from backup.";

        private void ValidateSteamApiDlls()
        {
            try
            {
                string gameFolder = txtGameFolder?.Text?.Trim();
                if (string.IsNullOrEmpty(gameFolder) || !Directory.Exists(gameFolder))
                {
                    UpdateSteamApiStatusLabels(
                        null,
                        null,
                        SteamApiDisplaySeverity.Neutral,
                        SteamApiDisplaySeverity.Neutral);
                    ClearSteamApiHealth();
                    ClearExpiredPatchOpMessage();
                    UpdateRestoreDllsButton(canRestore: false);
                    ReflowSteamApiPanels();
                    return;
                }

                var apiStatus = SteamApiValidator.DetectAndValidateSteamApi(gameFolder);
                _currentApiStatus = apiStatus;

                string x32Text = null;
                string x64Text = null;
                SteamApiDisplaySeverity x32Severity = SteamApiDisplaySeverity.Neutral;
                SteamApiDisplaySeverity x64Severity = SteamApiDisplaySeverity.Neutral;

                if (!apiStatus.X32Found && !apiStatus.X64Found)
                {
                    x32Text = SteamApiHealthNoDllsFoundHeadline;
                    x32Severity = SteamApiDisplaySeverity.Error;
                }
                else
                {
                    if (apiStatus.X32Found)
                        x32Text = BuildSteamApiDisplayLine("x32", SteamApiValidator.SteamApiDll32, apiStatus.X32Path, apiStatus.X32IsClean, out x32Severity);
                    if (apiStatus.X64Found)
                        x64Text = BuildSteamApiDisplayLine("x64", SteamApiValidator.SteamApiDll64, apiStatus.X64Path, apiStatus.X64IsClean, out x64Severity);
                }

                UpdateSteamApiStatusLabels(x32Text, x64Text, x32Severity, x64Severity);

                bool canRestore = (apiStatus.X32Found && !apiStatus.X32IsClean && apiStatus.CleanBackups.Count > 0) ||
                                 (apiStatus.X64Found && !apiStatus.X64IsClean && apiStatus.CleanBackups.Count > 0);

                UpdateRestoreDllsButton(canRestore: canRestore);

                UpdateSteamApiHealthFromStatus(apiStatus, canRestore);

                ClearExpiredPatchOpMessage();

                ReflowSteamApiPanels();
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError("Error validating Steam API DLLs", ex);
            }
        }

        private void UpdateSteamApiHealthFromStatus(SteamApiStatus apiStatus, bool canRestore)
        {
            if (apiStatus == null)
            {
                ClearSteamApiHealth();
                return;
            }

            if (!apiStatus.X32Found && !apiStatus.X64Found)
            {
                UpdateSteamApiHealthForNoDlls(canRestore);
                return;
            }

            if (canRestore)
            {
                UpdateSteamApiHealthLabel(
                    BuildSteamApiRestoreAvailableHealthMessage(apiStatus),
                    SteamApiDisplaySeverity.Success);
                return;
            }

            if (IsSteamApiInGoodStatusForRecoveryHint(apiStatus))
            {
                UpdateSteamApiHealthLabel(SteamApiHealthGoodHeadline, SteamApiDisplaySeverity.Success);
                return;
            }

            if (apiStatus.X32Found || apiStatus.X64Found)
            {
                UpdateSteamApiHealthLabel(SteamApiHealthModifiedHeadline, SteamApiDisplaySeverity.Warning);
                return;
            }

            ClearSteamApiHealth();
        }

        private void UpdateRestoreDllsButton(bool canRestore)
        {
            if (btnRestoreDlls == null)
                return;

            btnRestoreDlls.Visible = true;
            btnRestoreDlls.Text = "Restore";
            btnRestoreDlls.Enabled = canRestore;
        }

        private void UpdateSteamApiHealthForNoDlls(bool canRestore)
        {
            HideAndClearLabel(lblSteamApiHealthValue);

            if (!canRestore)
            {
                if (lblSteamApiHealthNote == null)
                    return;

                lblSteamApiHealthNote.Text = SteamApiHealthNoDllsFoundNote;
                lblSteamApiHealthNote.Visible = true;
                ApplySteamApiStatusColor(lblSteamApiHealthNote, SteamApiDisplaySeverity.Warning);
                return;
            }

            HideAndClearLabel(lblSteamApiHealthNote);
        }

        private void ClearSteamApiHealth()
        {
            HideAndClearLabel(lblSteamApiHealthValue);
            HideAndClearLabel(lblSteamApiHealthNote);
        }

        private void UpdateSteamApiHealthTwoLine(
            string headline,
            SteamApiDisplaySeverity headlineSeverity,
            string note,
            SteamApiDisplaySeverity noteSeverity)
        {
            if (lblSteamApiHealthValue == null)
                return;

            lblSteamApiHealthValue.Text = headline ?? string.Empty;
            lblSteamApiHealthValue.Visible = !string.IsNullOrWhiteSpace(headline);
            if (lblSteamApiHealthValue.Visible)
                ApplySteamApiStatusColor(lblSteamApiHealthValue, headlineSeverity);
            else
                lblSteamApiHealthValue.Height = 0;

            if (lblSteamApiHealthNote == null)
                return;

            if (string.IsNullOrWhiteSpace(note))
            {
                HideAndClearLabel(lblSteamApiHealthNote);
                return;
            }

            lblSteamApiHealthNote.Text = note;
            lblSteamApiHealthNote.Visible = true;
            ApplySteamApiStatusColor(lblSteamApiHealthNote, noteSeverity);
        }

        private void UpdateSteamApiHealthLabel(string text, SteamApiDisplaySeverity severity)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                ClearSteamApiHealth();
                return;
            }

            UpdateSteamApiHealthTwoLine(text, severity, null, SteamApiDisplaySeverity.Neutral);
        }

        private static bool IsSteamApiInGoodStatusForRecoveryHint(SteamApiStatus status)
        {
            if (status == null)
                return false;
            if (!SteamApiArchAcceptableForGoodHint(status.X32Found, status.X32IsClean, status.X32Path))
                return false;
            if (!SteamApiArchAcceptableForGoodHint(status.X64Found, status.X64IsClean, status.X64Path))
                return false;
            return true;
        }

        private static bool SteamApiArchAcceptableForGoodHint(bool found, bool isClean, string path)
        {
            if (!found)
                return true;
            if (isClean)
                return true;
            string productName = SteamApiValidator.GetFileProductName(path);
            if (string.IsNullOrWhiteSpace(productName))
                return false;
            return productName.Equals("Steam Client API", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildSteamApiRestoreAvailableHealthMessage(SteamApiStatus status)
        {
            if (status == null)
                return string.Empty;

            var lines = new List<string>();
            TryAddRestoreAvailableHealthLine(status, targetIs64Bit: false, SteamApiValidator.SteamApiDll32, "x32", lines);
            TryAddRestoreAvailableHealthLine(status, targetIs64Bit: true, SteamApiValidator.SteamApiDll64, "x64", lines);

            if (lines.Count == 0)
                return "Valid Steamworks DLL backup found.";

            return string.Join(Environment.NewLine, lines);
        }

        private static void TryAddRestoreAvailableHealthLine(
            SteamApiStatus status,
            bool targetIs64Bit,
            string canonicalDllName,
            string architecture,
            List<string> lines)
        {
            bool found = targetIs64Bit ? status.X64Found : status.X32Found;
            bool isClean = targetIs64Bit ? status.X64IsClean : status.X32IsClean;
            if (!found || isClean)
                return;

            string backupPath = SteamApiValidator.FindCleanBackupPathForBitness(status.CleanBackups, targetIs64Bit);
            if (string.IsNullOrEmpty(backupPath))
                return;

            string fileArch = $"{canonicalDllName} {architecture}";
            if (SteamApiValidator.TryGetWindowsSteamworksVersionLabel(backupPath, out string steamworksVersion) &&
                !string.IsNullOrWhiteSpace(steamworksVersion))
            {
                lines.Add(string.Format(SteamApiHealthValidBackupFoundFormat, steamworksVersion, fileArch));
                return;
            }

            lines.Add(string.Format(SteamApiHealthValidBackupFoundNoVersionFormat, fileArch));
        }

        private static string BuildSteamApiDisplayLine(
            string architecture,
            string fileName,
            string filePath,
            bool isCleanKnownHash,
            out SteamApiDisplaySeverity severity)
        {
            string fileArch = $"{fileName} {architecture}";

            if (isCleanKnownHash)
            {
                severity = SteamApiDisplaySeverity.Success;
                if (SteamApiValidator.TryGetWindowsSteamworksVersionLabel(filePath, out string steamworksVersion) &&
                    !string.IsNullOrWhiteSpace(steamworksVersion))
                {
                    return $"{SteamApiDisplayCheckMark} {steamworksVersion} ({fileArch})";
                }

                return $"{SteamApiDisplayCheckMark} {string.Format(SteamApiStatusValidKnownHash, fileArch)}";
            }

            string productName = SteamApiValidator.GetFileProductName(filePath);
            if (string.IsNullOrWhiteSpace(productName))
                productName = "Unknown Product";

            if (productName.Equals("Steam Client API", StringComparison.OrdinalIgnoreCase))
            {
                severity = SteamApiDisplaySeverity.Success;
                return $"{SteamApiDisplayQuestionMark} {string.Format(SteamApiStatusValidUnknownVersion, fileArch)}";
            }

            severity = SteamApiDisplaySeverity.Warning;
            return $"{SteamApiDisplayWarningMark} {string.Format(SteamApiStatusModified, fileArch)} â€” {productName}";
        }

        private void UpdateSteamApiStatusLabels(
            string x32Text,
            string x64Text,
            SteamApiDisplaySeverity x32Severity,
            SteamApiDisplaySeverity x64Severity)
        {
            SetSteamApiStatusLabel(lblSteamAPIStatusX32Value, x32Text, x32Severity);
            SetSteamApiStatusLabel(lblSteamAPIStatusX64Value, x64Text, x64Severity);

            ReflowSteamApiPanels();
        }

        private void SetSteamApiStatusLabel(Label label, string text, SteamApiDisplaySeverity severity)
        {
            if (label == null)
                return;

            if (string.IsNullOrWhiteSpace(text))
            {
                HideAndClearLabel(label);
                return;
            }

            label.Text = text;
            label.Visible = true;
            ApplySteamApiStatusColor(label, severity);
        }

        private void GrpBasicInfo_SizeChanged(object sender, EventArgs e)
        {
            ReflowSteamApiPanels();
        }

        private int GetLaunchModeBlockBottom()
        {
            int bottom = 0;
            foreach (Control control in new Control[] { rdoLaunchSteamClient, rdoLaunchExperimentalMode, rdoLaunchSteamDll, rdoLaunchNoEmulation })
            {
                if (control != null && control.Visible)
                    bottom = Math.Max(bottom, control.Bottom);
            }

            return bottom;
        }

        private void ReflowSteamApiPanels()
        {
            if (grpBasicInfo == null)
                return;

            int right = grpBasicInfo.ClientSize.Width - 20;
            if (btnRestoreDlls != null)
                right = Math.Min(right, btnRestoreDlls.Left - 8);

            int statusLeft = lblSteamAPIStatusX32Value != null ? lblSteamAPIStatusX32Value.Left : 150;
            int statusRowY = GetLaunchModeBlockBottom() > 0
                ? GetLaunchModeBlockBottom() + LaunchModeToSteamApiGap
                : (lblSteamAPIStatusX32Value != null ? lblSteamAPIStatusX32Value.Top : 105);

            if (lblSteamAPIStatus != null)
                lblSteamAPIStatus.Top = statusRowY;

            int statusAnchorY = statusRowY;

            bool hasX32 = SteamApiLabelHasContent(lblSteamAPIStatusX32Value);
            bool hasX64 = SteamApiLabelHasContent(lblSteamAPIStatusX64Value);
            bool hasStatus = hasX32 || hasX64;

            int y = statusAnchorY;

            if (hasX32)
                y = LayoutSteamApiLabelAt(lblSteamAPIStatusX32Value, statusLeft, y, right, 0);
            else
                CollapseSteamApiLabelHeight(lblSteamAPIStatusX32Value);

            if (hasX64)
            {
                if (hasX32)
                    y += SteamApiStatusRowGap;
                y = LayoutSteamApiLabelAt(lblSteamAPIStatusX64Value, statusLeft, y, right, 0);
            }
            else
                CollapseSteamApiLabelHeight(lblSteamAPIStatusX64Value);

            int healthLeft = lblSteamApiHealthValue != null ? lblSteamApiHealthValue.Left : statusLeft;
            int healthDesignerY = lblSteamApiHealthValue != null ? lblSteamApiHealthValue.Top : y;
            int healthY = hasStatus ? Math.Max(y + SteamApiBlockGap, healthDesignerY) : healthDesignerY;
            int healthMaxLines = hasStatus ? 0 : SteamApiHealthMaxLinesWhenNoStatus;

            if (SteamApiLabelHasContent(lblSteamApiHealthValue))
                y = LayoutSteamApiLabelAt(lblSteamApiHealthValue, healthLeft, healthY, right, healthMaxLines);
            else
            {
                CollapseSteamApiLabelHeight(lblSteamApiHealthValue);
                y = healthY;
            }

            if (SteamApiLabelHasContent(lblSteamApiHealthNote))
                y = LayoutSteamApiLabelAt(lblSteamApiHealthNote, healthLeft, y + SteamApiStatusRowGap, right, 0);
            else
                CollapseSteamApiLabelHeight(lblSteamApiHealthNote);

            int patchLeft = lblPatchOpMessage != null ? lblPatchOpMessage.Left : healthLeft;
            bool hasHealthContent = SteamApiLabelHasContent(lblSteamApiHealthValue) ||
                                    SteamApiLabelHasContent(lblSteamApiHealthNote);
            int patchY = hasHealthContent
                ? y + SteamApiStatusRowGap
                : (lblPatchOpMessage != null ? lblPatchOpMessage.Top : y);

            if (SteamApiLabelHasContent(lblPatchOpMessage))
                LayoutSteamApiLabelAt(lblPatchOpMessage, patchLeft, patchY, right, 0);
            else
                CollapseSteamApiLabelHeight(lblPatchOpMessage);
        }

        private static bool SteamApiLabelHasContent(Label label)
        {
            return label != null && label.Visible && !string.IsNullOrWhiteSpace(label.Text);
        }

        private static void CollapseSteamApiLabelHeight(Label label)
        {
            if (label != null)
                label.Height = 0;
        }

        private void HideAndClearLabel(Label label)
        {
            if (label != null)
            {
                label.Visible = false;
                label.Text = string.Empty;
            }
        }

        private void ClearExpiredPatchOpMessage()
        {
            if (lblPatchOpMessage == null)
                return;

            if (DateTime.UtcNow < _restoreMessageVisibleUntilUtc)
                return;

            HideAndClearLabel(lblPatchOpMessage);
        }

        // Uses designer Left; Top is stacked for status rows or designer-based anchors for health/patch.
        private static int LayoutSteamApiLabelAt(Label label, int left, int top, int rightEdge, int maxLines)
        {
            if (label == null)
                return top;

            label.Left = left;
            label.Top = top;

            int maxWidth = Math.Max(40, rightEdge - left);
            label.AutoSize = false;
            label.MaximumSize = Size.Empty;
            label.Width = maxWidth;

            Size measured = TextRenderer.MeasureText(
                label.Text,
                label.Font,
                new Size(maxWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);

            int height = Math.Max(label.Font.Height + 2, measured.Height);
            if (maxLines > 0)
            {
                int lineHeight = label.Font.Height + 2;
                height = Math.Min(height, lineHeight * maxLines);
            }

            label.Height = height;
            return label.Bottom;
        }

        private void ApplySteamApiStatusColor(Label label, SteamApiDisplaySeverity severity)
        {
            if (label == null)
                return;
            var colors = _themeService?.GetThemeColors(_themeService.EffectiveTheme);
            switch (severity)
            {
                case SteamApiDisplaySeverity.Success:
                    label.ForeColor = colors?.SuccessColor ?? Color.Green;
                    break;
                case SteamApiDisplaySeverity.Warning:
                    label.ForeColor = colors?.WarningColor ?? Color.Orange;
                    break;
                case SteamApiDisplaySeverity.Error:
                    label.ForeColor = colors?.ErrorColor ?? Color.Red;
                    break;
                case SteamApiDisplaySeverity.Disabled:
                    label.ForeColor = colors?.DisabledForeground ?? Color.Gray;
                    break;
                default:
                    label.ForeColor = colors?.Foreground ?? SystemColors.ControlText;
                    break;
            }
        }

        private enum SteamApiDisplaySeverity
        {
            Neutral,
            Success,
            Warning,
            Error,
            Disabled
        }

        private void OnRestoreDlls_Click(object sender, EventArgs e)
        {
            if (btnRestoreDlls != null && !btnRestoreDlls.Enabled)
                return;

            string gameFolder = txtGameFolder?.Text?.Trim();
            if (string.IsNullOrEmpty(gameFolder) || !Directory.Exists(gameFolder))
            {
                FormMessageBoxHelper.ShowIfAlive(this, "Game folder is not set or invalid.", "Operation Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                RestoreSteamApiDlls();
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError("Error restoring Steam API DLLs", ex);
                ShowRestoreOpMessage(SteamApiMessageErrorPrefix + " " + ex.Message, SteamApiDisplaySeverity.Error);
            }
        }

        private void RestoreSteamApiDlls()
        {
            if (_currentApiStatus == null || _currentApiStatus.CleanBackups.Count == 0)
            {
                ShowRestoreOpMessage(SteamApiRestoreOpNoBackups, SteamApiDisplaySeverity.Error);
                return;
            }

            int restoredCount = SteamApiValidator.TryRestoreSteamApiFromCleanBackups(_currentApiStatus, out string errorMessage);
            if (restoredCount > 0)
            {
                ShowRestoreOpMessage(SteamApiRestoreOpApplied, SteamApiDisplaySeverity.Success);
                ValidateSteamApiDlls();
            }
            else
            {
                ShowRestoreOpMessage(
                    string.IsNullOrEmpty(errorMessage) ? SteamApiRestoreOpNoMatchBackup : SteamApiMessageErrorPrefix + " " + errorMessage,
                    SteamApiDisplaySeverity.Error);
            }
        }

        private void ShowRestoreOpMessage(string message, SteamApiDisplaySeverity severity)
        {
            if (lblPatchOpMessage == null)
                return;

            if (string.IsNullOrWhiteSpace(message))
            {
                HideAndClearLabel(lblPatchOpMessage);
                ReflowSteamApiPanels();
                return;
            }

            lblPatchOpMessage.Text = message;
            lblPatchOpMessage.Visible = true;
            ApplySteamApiStatusColor(lblPatchOpMessage, severity);

            _restoreMessageVisibleUntilUtc = DateTime.UtcNow.AddMilliseconds(5000);
            StopAndDisposeRestoreMessageHideTimer();
            _restoreMessageHideTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            _restoreMessageHideTimer.Tick += RestoreMessageHideTimer_Tick;
            _restoreMessageHideTimer.Start();

            ReflowSteamApiPanels();
        }

        private void RestoreMessageHideTimer_Tick(object sender, EventArgs e)
        {
            StopAndDisposeRestoreMessageHideTimer();
            if (IsDisposed || Disposing)
                return;
            if (lblPatchOpMessage != null)
            {
                HideAndClearLabel(lblPatchOpMessage);
                ReflowSteamApiPanels();
            }
            _restoreMessageVisibleUntilUtc = DateTime.MinValue;
        }

        private void StopAndDisposeRestoreMessageHideTimer()
        {
            if (_restoreMessageHideTimer == null)
                return;
            _restoreMessageHideTimer.Stop();
            _restoreMessageHideTimer.Tick -= RestoreMessageHideTimer_Tick;
            _restoreMessageHideTimer.Dispose();
            _restoreMessageHideTimer = null;
        }

        private List<LaunchOption> _steamLaunchOptionsList;
        private List<LaunchOption> _steamLaunchOptionsAll;
        private int _steamLaunchComboRefreshSeq;
        private bool _suppressSteamLaunchComboApply;
        private const string SteamLaunchComboNoDataMessage = "\u2014 No game assets or no launch options \u2014";
        private const string SteamLaunchComboNoMatchingFilterMessage = "No launch options available";
        private const string SteamLaunchComboLoadingMessage = "Loading launch options from Steam\u2026";

        private void RequestRefreshSteamLaunchOptionsCombo()
        {
            if (IsDisposed || Disposing)
                return;
            void Kickoff()
            {
                if (IsDisposed || Disposing)
                    return;
                _ = RefreshSteamLaunchOptionsComboAsync().ForgetFaults(Program.LogService, nameof(RefreshSteamLaunchOptionsComboAsync));
            }
            if (InvokeRequired)
            {
                if (IsDisposed || Disposing)
                    return;
                BeginInvoke(new Action(Kickoff));
            }
            else
                Kickoff();
        }

        private async Task RefreshSteamLaunchOptionsComboAsync()
        {
            if (IsDisposed || Disposing)
                return;
            if (cmbSteamLaunchOptions == null || lblSteamLaunchOptions == null)
                return;

            try
            {
                cmbSteamLaunchOptions.SelectedIndexChanged -= CmbSteamLaunchOptions_SelectedIndexChanged;
                cmbSteamLaunchOptions.Items.Clear();
                _steamLaunchOptionsList = null;
                _steamLaunchOptionsAll = null;

                ulong appId = _gameConfig != null ? _gameConfig.AppId : 0;
                if (txtAppID != null && ulong.TryParse(txtAppID.Text.Trim(), out ulong parsedAppId) && parsedAppId > 0)
                    appId = parsedAppId;

                if (appId == 0)
                {
                    lblSteamLaunchOptions.Visible = false;
                    cmbSteamLaunchOptions.Visible = false;
                    cmbSteamLaunchOptions.Enabled = false;
                    cmbSteamLaunchOptions.SelectedIndexChanged += CmbSteamLaunchOptions_SelectedIndexChanged;
                    return;
                }

                int mySeq = Interlocked.Increment(ref _steamLaunchComboRefreshSeq);

                lblSteamLaunchOptions.Visible = true;
                cmbSteamLaunchOptions.Visible = true;
                cmbSteamLaunchOptions.Enabled = false;
                cmbSteamLaunchOptions.Items.Add(SteamLaunchComboLoadingMessage);
                cmbSteamLaunchOptions.SelectedIndex = 0;

                GameConfig launchConfig = (_gameConfig != null && _gameConfig.AppId == appId)
                    ? _gameConfig
                    : new GameConfig { AppId = appId };
                List<LaunchOption> all;
                try
                {
                    all = await ServiceLocator.LaunchOptionService.ExtractLaunchOptionsIncludingUserIniAsync(launchConfig).ConfigureAwait(true);

                    if (IsDisposed || Disposing || mySeq != Volatile.Read(ref _steamLaunchComboRefreshSeq))
                        return;
                }
                catch (Exception ex)
                {
                    if (IsDisposed || Disposing || mySeq != Volatile.Read(ref _steamLaunchComboRefreshSeq))
                        return;
                    LogWarningWithExceptionMessage("Steam launch options load failed for app " + appId, ex);
                    cmbSteamLaunchOptions.Items.Clear();
                    cmbSteamLaunchOptions.Enabled = false;
                    cmbSteamLaunchOptions.Items.Add(SteamLaunchComboNoDataMessage);
                    cmbSteamLaunchOptions.SelectedIndex = 0;
                    cmbSteamLaunchOptions.SelectedIndexChanged += CmbSteamLaunchOptions_SelectedIndexChanged;
                    return;
                }

                if (IsDisposed || Disposing || mySeq != Volatile.Read(ref _steamLaunchComboRefreshSeq))
                    return;

                cmbSteamLaunchOptions.Items.Clear();

                if (all == null || all.Count == 0)
                {
                    lblSteamLaunchOptions.Visible = true;
                    cmbSteamLaunchOptions.Visible = true;
                    cmbSteamLaunchOptions.Enabled = false;
                    cmbSteamLaunchOptions.Items.Add(SteamLaunchComboNoDataMessage);
                    cmbSteamLaunchOptions.SelectedIndex = 0;
                    cmbSteamLaunchOptions.SelectedIndexChanged += CmbSteamLaunchOptions_SelectedIndexChanged;
                    return;
                }

                _steamLaunchOptionsAll = all;
                lblSteamLaunchOptions.Visible = true;
                cmbSteamLaunchOptions.Visible = true;

                RebuildSteamLaunchComboFromAllOptions();
            }
            finally
            {
                if (!IsDisposed && !Disposing)
                    UpdateGameFolderInstallDirHintVisibility();
            }
        }

        private void RebuildSteamLaunchComboFromAllOptions()
        {
            if (cmbSteamLaunchOptions == null || lblSteamLaunchOptions == null)
                return;
            if (_steamLaunchOptionsAll == null || _steamLaunchOptionsAll.Count == 0)
                return;

            cmbSteamLaunchOptions.SelectedIndexChanged -= CmbSteamLaunchOptions_SelectedIndexChanged;
            cmbSteamLaunchOptions.Items.Clear();
            _steamLaunchOptionsList = null;

            bool excludeRestricted = !ServiceLocator.AppDataService.LoadApplicationSettings().FullLaunchOptions;
            var filtered = ServiceLocator.LaunchOptionService.FilterLaunchOptionsForUi(_steamLaunchOptionsAll, excludeRestricted);

            if (filtered == null || filtered.Count == 0)
            {
                cmbSteamLaunchOptions.Enabled = false;
                cmbSteamLaunchOptions.Items.Add(SteamLaunchComboNoMatchingFilterMessage);
                cmbSteamLaunchOptions.SelectedIndex = 0;
                cmbSteamLaunchOptions.SelectedIndexChanged += CmbSteamLaunchOptions_SelectedIndexChanged;
                return;
            }

            _steamLaunchOptionsList = filtered;
            cmbSteamLaunchOptions.Enabled = true;
            string gameDisplayName = LaunchOptionService.ResolveGameDisplayName(
                GetTextOrNull(txtGameName),
                _gameConfig != null ? _gameConfig.AppName : null);
            foreach (var o in filtered)
                cmbSteamLaunchOptions.Items.Add(LaunchOptionService.FormatLaunchOptionComboLabel(o, gameDisplayName));

            int selectIndex = LaunchOptionService.FindBestMatchingLaunchOptionIndex(
                filtered,
                GetTextOrNull(txtGameExecutable) ?? string.Empty,
                GetTextOrNull(txtLaunchParameters) ?? string.Empty,
                GetTextOrNull(txtWorkingDirectory) ?? string.Empty);
            if (selectIndex < 0)
                selectIndex = LaunchOptionService.FindDefaultLaunchOptionIndex(filtered);

            _suppressSteamLaunchComboApply = true;
            try
            {
                cmbSteamLaunchOptions.SelectedIndex = selectIndex;
            }
            finally
            {
                _suppressSteamLaunchComboApply = false;
            }

            cmbSteamLaunchOptions.SelectedIndexChanged += CmbSteamLaunchOptions_SelectedIndexChanged;
            SyncUserLaunchOptionUiToSelection();
        }

        private void SyncUserLaunchOptionUiToSelection()
        {
            if (cmbSteamLaunchOptions == null || _steamLaunchOptionsList == null)
                return;

            int i = cmbSteamLaunchOptions.SelectedIndex;
            if (i < 0 || i >= _steamLaunchOptionsList.Count)
            {
                if (btnRemoveUserLaunchOption != null)
                    btnRemoveUserLaunchOption.Enabled = false;
                return;
            }

            var selected = _steamLaunchOptionsList[i];
            bool isUser = LaunchOptionService.IsUserLaunchOption(selected);

            if (btnRemoveUserLaunchOption != null)
                btnRemoveUserLaunchOption.Enabled = isUser;

            if (txtUserLaunchOptionName != null && isUser)
                SetTextBoxText(txtUserLaunchOptionName, selected.Description);
        }

        private void CmbSteamLaunchOptions_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isLoading || _suppressSteamLaunchComboApply)
                return;
            if (cmbSteamLaunchOptions == null || _steamLaunchOptionsList == null)
                return;
            int i = cmbSteamLaunchOptions.SelectedIndex;
            if (i < 0 || i >= _steamLaunchOptionsList.Count)
                return;

            ApplySteamLaunchOptionToForm(_steamLaunchOptionsList[i]);
            SyncUserLaunchOptionUiToSelection();
        }

        private void ApplySteamLaunchOptionToForm(LaunchOption opt)
        {
            if (opt == null)
                return;

            string gameFolder = txtGameFolder != null ? txtGameFolder.Text.Trim() : string.Empty;

            if (txtGameExecutable != null)
            {
                SetTextBoxText(txtGameExecutable, LaunchOptionService.ToDisplayPathForGameFolder(opt.Executable, gameFolder));
            }

            if (txtLaunchParameters != null)
                SetTextBoxText(txtLaunchParameters, opt.Parameters);

            if (txtWorkingDirectory != null)
            {
                SetTextBoxText(txtWorkingDirectory, LaunchOptionService.ToDisplayPathForGameFolder(opt.WorkingDir, gameFolder));
            }

            if (!LaunchOptionService.IsUserLaunchOption(opt))
            {
                var app = new AppSettings();
                LaunchOption.ApplyBetaBranchToAppSettings(opt, app);
                if (chkBetaBranch != null)
                    chkBetaBranch.Checked = app.IsBetaBranch;
                if (txtBetaBranchName != null)
                    SetTextBoxText(txtBetaBranchName, app.BranchName ?? SteamPicsKeyNames.SteamDefaultBranchName);
                UpdateBetaBranchNameEnabled();
            }
        }

        private async void OnSaveUserLaunchOption_Click(object sender, EventArgs e)
        {
            try
            {
                if (_gameConfig == null)
                    return;
                if (txtUserLaunchOptionName == null)
                    return;

                ulong appId = LaunchOptionService.ResolveLaunchOptionAppId(_gameConfig, GetTextOrNull(txtAppID));
                if (appId == 0)
                    return;

                string customName = LaunchOptionService.NormalizeCustomLaunchOptionName(
                    txtUserLaunchOptionName.Text != null ? txtUserLaunchOptionName.Text : string.Empty);
                if (string.IsNullOrWhiteSpace(customName))
                {
                    FormMessageBoxHelper.ShowIfAlive(this, "Please enter a name for the custom launch option.", "Missing name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string exe = txtGameExecutable != null ? (txtGameExecutable.Text ?? string.Empty) : string.Empty;
                string parameters = txtLaunchParameters != null ? (txtLaunchParameters.Text ?? string.Empty) : string.Empty;
                string workingDir = txtWorkingDirectory != null ? (txtWorkingDirectory.Text ?? string.Empty) : string.Empty;

                ServiceLocator.LaunchOptionService.SaveUserLaunchOption(appId, customName, exe, parameters, workingDir);
                await RefreshSteamLaunchOptionsComboAsync().ConfigureAwait(true);

                if (IsDisposed || Disposing)
                    return;

                if (cmbSteamLaunchOptions != null && _steamLaunchOptionsList != null)
                {
                    int i = LaunchOptionService.FindUserLaunchOptionIndexByName(_steamLaunchOptionsList, customName);
                    if (i >= 0)
                        cmbSteamLaunchOptions.SelectedIndex = i;
                }
            }
            catch (Exception ex)
            {
                LogWarningWithExceptionMessage("Failed to save user launch option", ex);
            }
        }

        private async void OnRemoveUserLaunchOption_Click(object sender, EventArgs e)
        {
            try
            {
                if (_gameConfig == null)
                    return;
                if (cmbSteamLaunchOptions == null || _steamLaunchOptionsList == null)
                    return;

                ulong appId = LaunchOptionService.ResolveLaunchOptionAppId(_gameConfig, GetTextOrNull(txtAppID));
                if (appId == 0)
                    return;

                int selectedIndex = cmbSteamLaunchOptions.SelectedIndex;
                if (selectedIndex < 0 || selectedIndex >= _steamLaunchOptionsList.Count)
                    return;

                var selected = _steamLaunchOptionsList[selectedIndex];
                if (!LaunchOptionService.IsUserLaunchOption(selected))
                    return;

                string customName = LaunchOptionService.NormalizeCustomLaunchOptionName(selected.Description);
                if (string.IsNullOrWhiteSpace(customName))
                    return;

                ServiceLocator.LaunchOptionService.RemoveUserLaunchOption(appId, customName);
                await RefreshSteamLaunchOptionsComboAsync().ConfigureAwait(true);
                if (IsDisposed || Disposing)
                    return;
            }
            catch (Exception ex)
            {
                LogWarningWithExceptionMessage("Failed to remove user launch option", ex);
            }
        }

        private void TxtAppID_Leave_RefreshSteamLaunch(object sender, EventArgs e)
        {
            RequestRefreshSteamLaunchOptionsCombo();
        }

        private JsonObject _inventoryItemsRoot;
        private TextBox _inventoryInlineEditor;
        private ListViewItem _inventoryEditingItem;
        private int _inventoryEditingColumn;
        private string _inventoryEditingOriginalText;
        private const int InventoryColQuantity = 2;

        private void WireInventoryListEvents()
        {
            if (lstInventoryItems == null)
                return;
            lstInventoryItems.DrawColumnHeader += LstInventoryItems_DrawColumnHeader;
            lstInventoryItems.DrawItem += LstInventoryItems_DrawItem;
            lstInventoryItems.DrawSubItem += LstInventoryItems_DrawSubItem;
            lstInventoryItems.ColumnWidthChanged += LstInventoryItems_ColumnWidthChanged;
            lstInventoryItems.ColumnWidthChanging += LstInventoryItems_ColumnWidthChanging;
            lstInventoryItems.Resize += LstInventoryItems_Resize;
            lstInventoryItems.MouseDoubleClick += LstInventoryItems_MouseDoubleClick;
            lstInventoryItems.KeyDown += LstInventoryItems_KeyDown;
            lstInventoryItems.SelectedIndexChanged += LstInventoryItems_SelectedIndexChanged;
        }

        private sealed class InventoryGridRowBinding
        {
            public JsonObject Item { get; set; }
            public string QuantityKey { get; set; }

            public static InventoryGridRowBinding Create(JsonObject o)
            {
                GoldbergFilesService.ResolveInventoryKeys(o, out string qk, out string _);
                return new InventoryGridRowBinding { Item = o, QuantityKey = qk };
            }
        }

        private void FlushInventoryListViewItemToItem(ListViewItem item)
        {
            var binding = item.Tag as InventoryGridRowBinding;
            if (binding?.Item == null || item.SubItems.Count <= InventoryColQuantity)
                return;

            GoldbergFilesService.SetItemStringProperty(binding.Item, binding.QuantityKey, item.SubItems[InventoryColQuantity].Text);
        }

        private void SetupInventoryListView()
        {
            if (lstInventoryItems == null)
                return;

            lstInventoryItems.LabelEdit = false;
            lstInventoryItems.TabStop = true;
            ListViewColumnHelper.UpdateLastColumnWidth(lstInventoryItems);
            ApplyInventoryTabTheme();
        }

        private void EnsureInventoryInlineEditorCreated()
        {
            if (lstInventoryItems == null || _inventoryInlineEditor != null)
                return;

            _inventoryInlineEditor = new TextBox
            {
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false,
                HideSelection = false
            };
            _inventoryInlineEditor.Leave += InventoryInlineEditor_Leave;
            _inventoryInlineEditor.KeyDown += InventoryInlineEditor_KeyDown;
            lstInventoryItems.Controls.Add(_inventoryInlineEditor);
        }

        private void FinishInventoryInlineEdit(bool cancel)
        {
            if (_inventoryInlineEditor == null || !_inventoryInlineEditor.Visible)
                return;

            var item = _inventoryEditingItem;
            int col = _inventoryEditingColumn;
            string original = _inventoryEditingOriginalText ?? string.Empty;
            string text = _inventoryInlineEditor.Text ?? string.Empty;

            _inventoryInlineEditor.Visible = false;
            _inventoryEditingItem = null;

            if (item == null || col < 0 || item.SubItems.Count <= col)
                return;

            if (cancel)
                item.SubItems[col].Text = original;
            else
                item.SubItems[col].Text = text;

            if (!cancel)
            {
                FlushInventoryListViewItemToItem(item);
                if (!_isLoading)
                    CheckForChanges();
            }
        }

        private void TryBeginInventorySubItemEdit(ListViewItem item, int columnIndex)
        {
            if (lstInventoryItems == null || item == null)
                return;
            if (columnIndex != InventoryColQuantity)
                return;

            EnsureInventoryInlineEditorCreated();
            if (_inventoryInlineEditor == null)
                return;

            FinishInventoryInlineEdit(false);

            int idx = item.Index;
            if (!ListViewColumnHelper.TryGetSubItemCellBoundsByLayout(lstInventoryItems, idx, columnIndex, out Rectangle bounds))
                return;

            _inventoryEditingItem = item;
            _inventoryEditingColumn = columnIndex;
            _inventoryEditingOriginalText = item.SubItems[columnIndex].Text ?? string.Empty;

            var colors = _themeService.GetThemeColors(_themeService.EffectiveTheme);
            _inventoryInlineEditor.Font = lstInventoryItems.Font;
            _inventoryInlineEditor.BackColor = colors.FieldBackground;
            _inventoryInlineEditor.ForeColor = colors.Foreground;
            _inventoryInlineEditor.Text = _inventoryEditingOriginalText;

            Rectangle r = bounds;
            r.Inflate(-2, -1);
            int minH = TextRenderer.MeasureText("Mg", _inventoryInlineEditor.Font).Height + 4;
            if (r.Height < minH)
                r.Height = minH;
            if (r.Width < 20)
                r.Width = 20;
            _inventoryInlineEditor.SetBounds(r.X, r.Y, r.Width, r.Height);
            _inventoryInlineEditor.Visible = true;
            _inventoryInlineEditor.BringToFront();
            _inventoryInlineEditor.Focus();
            _inventoryInlineEditor.SelectAll();
        }

        private void LstInventoryItems_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            ListViewColumnHelper.DrawThemedColumnHeader(e, _themeService, null);
        }

        private void LstInventoryItems_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void LstInventoryItems_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void LstInventoryItems_ColumnWidthChanged(object sender, ColumnWidthChangedEventArgs e)
        {
            if (lstInventoryItems == null || lstInventoryItems.Columns.Count < 2)
                return;
            if (e.ColumnIndex < lstInventoryItems.Columns.Count - 1)
                ListViewColumnHelper.UpdateLastColumnWidth(lstInventoryItems);
        }

        private void LstInventoryItems_ColumnWidthChanging(object sender, ColumnWidthChangingEventArgs e)
        {
            if (lstInventoryItems == null || lstInventoryItems.Columns.Count < 2)
                return;
            if (e.ColumnIndex == lstInventoryItems.Columns.Count - 1)
            {
                e.Cancel = true;
                e.NewWidth = lstInventoryItems.Columns[e.ColumnIndex].Width;
            }
        }

        private void LstInventoryItems_Resize(object sender, EventArgs e)
        {
            if (lstInventoryItems == null)
                return;
            ListViewColumnHelper.UpdateLastColumnWidth(lstInventoryItems);
            FinishInventoryInlineEdit(false);
        }

        private void LstInventoryItems_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (lstInventoryItems == null || IsInventoryRawJsonEditorActive())
                return;

            var hit = lstInventoryItems.HitTest(e.X, e.Y);
            if (hit.Item == null)
                return;

            int col = ListViewColumnHelper.GetColumnIndexFromClientX(lstInventoryItems, e.X);
            TryBeginInventorySubItemEdit(hit.Item, col);
        }

        private void LstInventoryItems_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.F2 || lstInventoryItems == null || IsInventoryRawJsonEditorActive())
                return;
            if (lstInventoryItems.SelectedItems.Count == 0)
                return;

            TryBeginInventorySubItemEdit(lstInventoryItems.SelectedItems[0], InventoryColQuantity);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void LstInventoryItems_SelectedIndexChanged(object sender, EventArgs e)
        {
            FinishInventoryInlineEdit(false);
        }

        private void InventoryInlineEditor_Leave(object sender, EventArgs e)
        {
            if (_inventoryInlineEditor == null || !_inventoryInlineEditor.Visible)
                return;
            if (IsDisposed || Disposing)
                return;

            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || Disposing)
                    return;
                if (_inventoryInlineEditor == null || !_inventoryInlineEditor.Visible)
                    return;
                if (_inventoryInlineEditor.Focused)
                    return;
                FinishInventoryInlineEdit(false);
            }));
        }

        private void InventoryInlineEditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                FinishInventoryInlineEdit(false);
                e.Handled = true;
                e.SuppressKeyPress = true;
                lstInventoryItems?.Focus();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                FinishInventoryInlineEdit(true);
                e.Handled = true;
                e.SuppressKeyPress = true;
                lstInventoryItems?.Focus();
            }
        }

        private void LoadInventoryEditorFromDisk()
        {
            if (!HasCurrentGameWithValidAppId())
                return;

            var service = ServiceLocator.GoldbergFilesService;
            string raw = service.LoadItems(_gameConfig.AppId);
            ApplyItemsJsonToInventoryUi(raw);
        }

        private void SetInventoryRawJsonRowExpanded(bool expanded)
        {
            const int rawHeight = 100;

            if (txtInventoryRaw != null)
            {
                txtInventoryRaw.Visible = expanded;
                txtInventoryRaw.Height = expanded ? rawHeight : 0;
            }

            LayoutInventoryEditor();
        }

        private void GrpInventoryEditor_Resize(object sender, EventArgs e)
        {
            LayoutInventoryEditor();
        }

        private void LayoutInventoryEditor()
        {
            if (grpInventoryEditor == null || lstInventoryItems == null)
                return;

            const int pad = 11;
            int listTop = pnlInventoryButtons != null ? pnlInventoryButtons.Bottom + 4 : (lblInventoryHint?.Bottom ?? pad) + 4;
            int listBottom = grpInventoryEditor.ClientSize.Height - pad;

            if (txtInventoryRaw != null && txtInventoryRaw.Visible)
            {
                txtInventoryRaw.Width = Math.Max(100, grpInventoryEditor.ClientSize.Width - pad * 2);
                txtInventoryRaw.Left = pad;
                txtInventoryRaw.Top = listBottom - txtInventoryRaw.Height;
                listBottom = txtInventoryRaw.Top - 4;
            }

            lstInventoryItems.Left = pad;
            lstInventoryItems.Width = Math.Max(100, grpInventoryEditor.ClientSize.Width - pad * 2);
            lstInventoryItems.Top = listTop;
            lstInventoryItems.Height = Math.Max(40, listBottom - listTop);
        }

        private bool IsInventoryRawJsonEditorActive()
        {
            return txtInventoryRaw != null && txtInventoryRaw.Visible;
        }

        private void ApplyItemsJsonToInventoryUi(string raw)
        {
            SetInventoryRawJsonRowExpanded(false);
            if (txtInventoryRaw != null)
                ClearTextBox(txtInventoryRaw);
            FinishInventoryInlineEdit(false);
            if (lstInventoryItems != null)
            {
                lstInventoryItems.Visible = true;
                lstInventoryItems.Items.Clear();
            }

            _inventoryItemsRoot = null;
            raw = raw?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(raw))
            {
                _inventoryItemsRoot = new JsonObject();
                return;
            }

            try
            {
                _inventoryItemsRoot = ServiceLocator.GoldbergFilesService.ParseItemsJsonToMap(raw);
                RefreshInventoryGridFromRoot();
            }
            catch (Exception ex)
            {
                LogWarningWithExceptionMessage($"{PathConstants.GoldbergItemsJsonFileName} could not be parsed for grid", ex);
                if (txtInventoryRaw != null)
                    SetTextBoxText(txtInventoryRaw, raw);
                SetInventoryRawJsonRowExpanded(true);
                if (lstInventoryItems != null)
                    lstInventoryItems.Visible = false;
                _inventoryItemsRoot = null;
            }
        }

        private void RefreshInventoryGridFromRoot()
        {
            if (lstInventoryItems == null)
                return;

            FinishInventoryInlineEdit(false);
            lstInventoryItems.Items.Clear();
            if (_inventoryItemsRoot == null)
                return;

            foreach (JsonProperty p in _inventoryItemsRoot.Properties())
            {
                var o = p.Value as JsonObject;
                if (o == null)
                    continue;

                var binding = InventoryGridRowBinding.Create(o);

                var item = new ListViewItem(p.Name);
                item.SubItems.Add(o["name"]?.ToString() ?? string.Empty);
                item.SubItems.Add(o[binding.QuantityKey]?.ToString() ?? string.Empty);
                item.SubItems.Add(o["type"]?.ToString() ?? string.Empty);
                item.Tag = binding;
                lstInventoryItems.Items.Add(item);
            }

            ListViewColumnHelper.UpdateLastColumnWidth(lstInventoryItems);
        }

        private void SyncInventoryRootFromGrid()
        {
            if (lstInventoryItems == null || IsInventoryRawJsonEditorActive())
                return;

            var root = new JsonObject();
            foreach (ListViewItem item in lstInventoryItems.Items)
            {
                string id = item.Text;
                var binding = item.Tag as InventoryGridRowBinding;
                if (string.IsNullOrEmpty(id) || binding?.Item == null)
                    continue;

                FlushInventoryListViewItemToItem(item);
                root[id] = binding.Item;
            }

            _inventoryItemsRoot = root;
        }

        private string GetInventoryItemsJsonForSave()
        {
            if (IsInventoryRawJsonEditorActive())
                return txtInventoryRaw.Text ?? string.Empty;

            SyncInventoryRootFromGrid();
            if (_inventoryItemsRoot == null)
                return "{}";
            return _inventoryItemsRoot.ToJsonString(JsonFormatting.Indented);
        }

        private string GetInventoryItemsStateFingerprint()
        {
            if (IsInventoryRawJsonEditorActive())
                return "RAW\n" + (txtInventoryRaw?.Text ?? string.Empty);
            SyncInventoryRootFromGrid();
            return "GRID\n" + (_inventoryItemsRoot?.ToJsonString(JsonFormatting.None) ?? "{}");
        }

        private void OnReloadInventoryFromDisk_Click(object sender, EventArgs e)
        {
            LoadInventoryEditorFromDisk();
            if (!_isLoading)
                CheckForChanges();
        }

        private const string ModsListNoIdDisplay = "-";

        private static readonly HashSet<string> ModsListSkippedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".apng", ".bmp", ".dib", ".gif", ".heic", ".heif", ".ico", ".jfif", ".jpe", ".jpeg", ".jpg",
            ".png", ".svg", ".tif", ".tiff", ".webp",
            ".rtf", ".txt"
        };

        private List<GoldbergFilesService.ModsSummaryRow> _modsListRowsCache;
        private CancellationTokenSource _modsListResolveCts;

        private void WireModsSummaryListEvents()
        {
            if (lstModsSummary == null)
                return;
            lstModsSummary.DrawColumnHeader += LstModsSummary_DrawColumnHeader;
            lstModsSummary.DrawItem += LstModsSummary_DrawItem;
            lstModsSummary.DrawSubItem += LstModsSummary_DrawSubItem;
            lstModsSummary.ColumnWidthChanged += LstModsSummary_ColumnWidthChanged;
            lstModsSummary.ColumnWidthChanging += LstModsSummary_ColumnWidthChanging;
        }

        private void TabMods_Enter(object sender, EventArgs e)
        {
            RefreshModsSummaryList();
        }

        private void lstModsSummary_Resize(object sender, EventArgs e)
        {
            ListViewColumnHelper.UpdateLastColumnWidth(lstModsSummary);
        }

        private void LstModsSummary_ColumnWidthChanged(object sender, ColumnWidthChangedEventArgs e)
        {
            if (lstModsSummary == null || lstModsSummary.Columns.Count < 3)
                return;
            if (e.ColumnIndex < lstModsSummary.Columns.Count - 1)
                ListViewColumnHelper.UpdateLastColumnWidth(lstModsSummary);
        }

        private void LstModsSummary_ColumnWidthChanging(object sender, ColumnWidthChangingEventArgs e)
        {
            if (lstModsSummary == null || lstModsSummary.Columns.Count < 3)
                return;
            if (e.ColumnIndex == lstModsSummary.Columns.Count - 1)
            {
                e.Cancel = true;
                e.NewWidth = lstModsSummary.Columns[e.ColumnIndex].Width;
            }
        }

        private void LstModsSummary_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            ListViewColumnHelper.DrawThemedColumnHeader(e, _themeService, null);
        }

        private void LstModsSummary_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void LstModsSummary_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void OnOpenModsFolder_Click(object sender, EventArgs e)
        {
            if (!HasCurrentGameWithValidAppId())
                return;

            string modsDir = ServiceLocator.GoldbergFilesService.EnsureModsDirectory(_gameConfig.AppId);
            ShellFolderHelper.OpenFolderForOwner(this, modsDir, createIfMissing: true, "Mods folder");
        }

        private void OnCopyFilesToMods_Click(object sender, EventArgs e)
        {
            string modsDir = GetModsDirectoryForCurrentGameOrEmpty();
            if (string.IsNullOrEmpty(modsDir))
                return;
            using (var ofd = new OpenFileDialog())
            {
                ofd.Multiselect = true;
                ofd.Title = "Copy files into mods folder";
                if (ofd.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    var result = ServiceLocator.GoldbergFilesService.CopyFilesToMods(_gameConfig.AppId, ofd.FileNames);
                    if (!result.IsSuccess)
                        throw new InvalidOperationException(result.ErrorMessage);
                    RefreshModsSummaryList();
                }
                catch (Exception ex)
                {
                    LogAndShowModsWarning("Failed to copy files to mods folder", "Copy to mods", ex);
                }
            }
        }

        private void OnCopyFoldersToMods_Click(object sender, EventArgs e)
        {
            string modsDir = GetModsDirectoryForCurrentGameOrEmpty();
            if (string.IsNullOrEmpty(modsDir))
                return;
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select a folder to copy into mods. A subfolder with the same name will be created under mods.";
                if (fbd.ShowDialog(this) != DialogResult.OK)
                    return;
                string srcRoot = fbd.SelectedPath;
                if (string.IsNullOrEmpty(srcRoot))
                    return;
                string folderName = Path.GetFileName(srcRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrEmpty(folderName))
                {
                    FormMessageBoxHelper.ShowIfAlive(this, "Could not determine the folder name to create under mods.", "Copy folder to mods",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                try
                {
                    var result = ServiceLocator.GoldbergFilesService.CopyFolderToMods(_gameConfig.AppId, srcRoot);
                    if (!result.IsSuccess)
                        throw new InvalidOperationException(result.ErrorMessage);
                    RefreshModsSummaryList();
                }
                catch (Exception ex)
                {
                    LogAndShowModsWarning("Failed to copy folder to mods", "Copy folder to mods", ex);
                }
            }
        }

        private string GetModsDirectoryForCurrentGameOrEmpty()
        {
            if (!HasCurrentGameWithValidAppId())
                return string.Empty;
            return ServiceLocator.GoldbergFilesService.GetModsDirectory(_gameConfig.AppId);
        }

        private void CancelModsListResolve()
        {
            if (_modsListResolveCts == null)
                return;
            try
            {
                _modsListResolveCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
            _modsListResolveCts.Dispose();
            _modsListResolveCts = null;
        }

        private void RebuildModsSummaryListView(Dictionary<string, string> workshopTitlesOrNull)
        {
            if (lstModsSummary == null || _modsListRowsCache == null)
                return;

            var titles = workshopTitlesOrNull ?? new Dictionary<string, string>(StringComparer.Ordinal);
            lstModsSummary.BeginUpdate();
            lstModsSummary.Items.Clear();
            foreach (var row in _modsListRowsCache)
            {
                string displayName;
                if (row.ModId != ModsListNoIdDisplay && titles.TryGetValue(row.ModId, out string workshopTitle) && !string.IsNullOrWhiteSpace(workshopTitle))
                    displayName = workshopTitle.Trim();
                else
                    displayName = row.FileName ?? string.Empty;

                var item = new ListViewItem(new[] { row.ModId, displayName, string.Empty });
                item.ToolTipText = row.FileName;
                lstModsSummary.Items.Add(item);
            }
            lstModsSummary.EndUpdate();
            lstModsSummary_Resize(lstModsSummary, EventArgs.Empty);
        }

        private void RefreshModsSummaryList()
        {
            if (lstModsSummary == null || !HasCurrentGameWithValidAppId())
                return;

            CancelModsListResolve();

            var rows = ServiceLocator.GoldbergFilesService.LoadModsSummaryRows(_gameConfig.AppId, ModsListSkippedExtensions, ModsListNoIdDisplay);

            _modsListRowsCache = rows;
            RebuildModsSummaryListView(null);

            List<string> idsToResolve = rows
                .Where(r => r.ModId != ModsListNoIdDisplay)
                .Select(r => r.ModId)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            string apiKey = _steamApiKeyService.GetApiKey();
            if (string.IsNullOrEmpty(apiKey) || idsToResolve.Count == 0)
                return;

            _modsListResolveCts = new CancellationTokenSource();
            CancellationToken token = _modsListResolveCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    Dictionary<string, string> titles = await SteamWebApiService.GetPublishedFileTitlesAsync(apiKey, idsToResolve, token).ConfigureAwait(false);
                    if (token.IsCancellationRequested || IsDisposed || Disposing)
                        return;
                    BeginInvoke(new Action(() =>
                    {
                        if (token.IsCancellationRequested || IsDisposed || Disposing)
                            return;
                        RebuildModsSummaryListView(titles);
                    }));
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    LogWarningWithExceptionMessage("Workshop GetDetails failed for mods list", ex);
                }
            }, token).ForgetFaults(Program.LogService, nameof(RefreshModsSummaryList));
        }

        private const string LaunchExperimentalModeUnavailableToolTipText =
            "Experimental steam_api DLLs are missing under goldberg\\experimental.\r\n\r\nRun Goldberg Update or repair the emulator, then reopen this dialog.";

        private const string LaunchSteamClientUnavailableToolTipText =
            "Goldberg Steam client files are missing under goldberg\\steamclient_experimental.\r\n\r\nRun Goldberg Update or repair the emulator.";

        private const string LaunchSteamDllUnavailableToolTipText =
            "Steam.dll is missing from goldberg\\steam_old.\r\n\r\nRun Goldberg Update or repair the emulator, then reopen this dialog.";

        private void ApplyLaunchModeAvailability()
        {
            if (rdoLaunchSteamClient == null || rdoLaunchExperimentalMode == null || rdoLaunchSteamDll == null || rdoLaunchNoEmulation == null)
                return;

            GoldbergLaunchModeAvailability availability = _gameLaunchService.GetLaunchModeAvailability(_gameConfig);

            rdoLaunchSteamClient.Enabled = availability.SteamClientAvailable;
            if (!availability.SteamClientAvailable && toolTip != null)
                toolTip.SetToolTip(rdoLaunchSteamClient, LaunchSteamClientUnavailableToolTipText);

            rdoLaunchExperimentalMode.Enabled = availability.StandardSteamApiAvailable;
            if (!availability.StandardSteamApiAvailable && toolTip != null)
                toolTip.SetToolTip(rdoLaunchExperimentalMode, LaunchExperimentalModeUnavailableToolTipText);

            rdoLaunchSteamDll.Enabled = availability.SteamDllBesideExeAvailable;
            if (!availability.SteamDllBesideExeAvailable && toolTip != null)
                toolTip.SetToolTip(rdoLaunchSteamDll, LaunchSteamDllUnavailableToolTipText);

            GoldbergLaunchMode preferred = _gameConfig?.LaunchMode ?? GoldbergLaunchMode.SteamClient;
            GoldbergLaunchMode resolved = availability.ResolveAvailable(preferred);
            if (resolved != preferred)
            {
                SelectLaunchModeUi(resolved);
                if (_gameConfig != null)
                    _gameConfig.LaunchMode = resolved;
            }
        }

        private void SelectLaunchModeUi(GoldbergLaunchMode mode)
        {
            if (rdoLaunchSteamClient == null || rdoLaunchExperimentalMode == null || rdoLaunchSteamDll == null || rdoLaunchNoEmulation == null)
                return;

            if (mode == GoldbergLaunchMode.NoEmulation)
                rdoLaunchNoEmulation.Checked = true;
            else if (mode == GoldbergLaunchMode.SteamDllBesideExe)
                rdoLaunchSteamDll.Checked = true;
            else if (mode == GoldbergLaunchMode.StandardSteamApi)
                rdoLaunchExperimentalMode.Checked = true;
            else
                rdoLaunchSteamClient.Checked = true;
        }

        private GoldbergLaunchMode GetSelectedLaunchModeFromUi()
        {
            if (rdoLaunchNoEmulation != null && rdoLaunchNoEmulation.Checked)
                return GoldbergLaunchMode.NoEmulation;
            if (rdoLaunchSteamDll != null && rdoLaunchSteamDll.Checked)
                return GoldbergLaunchMode.SteamDllBesideExe;
            if (rdoLaunchExperimentalMode != null && rdoLaunchExperimentalMode.Checked)
                return GoldbergLaunchMode.StandardSteamApi;
            return GoldbergLaunchMode.SteamClient;
        }

        private void ApplyLaunchModeFromUiToGameConfig()
        {
            if (_gameConfig == null)
                return;

            _gameConfig.LaunchMode = GetSelectedLaunchModeFromUi();
        }

        private void RdoLaunchMode_CheckedChanged(object sender, EventArgs e)
        {
            if (_isLoading)
                return;

            var radio = sender as RadioButton;
            if (radio == null || !radio.Checked)
                return;

            ApplyLaunchModeFromUiToGameConfig();
        }

        private void WireLaunchModeRadio(RadioButton radio)
        {
            if (radio == null)
                return;
            radio.CheckedChanged += Control_Changed;
            radio.CheckedChanged += RdoLaunchMode_CheckedChanged;
        }

        private string _initialCustomStats = string.Empty;
        private string _customStatsRawJson = string.Empty;

        private async void OnRefreshStats_Click(object sender, EventArgs e)
        {
            if (!HasCurrentGameWithValidAppId())
                return;

            try
            {
                LoadAndDisplayStats();
                if (string.IsNullOrWhiteSpace(_customStatsRawJson))
                {
                    await ServiceLocator.EmulatorConfigService.TryEnsureStatsJsonAsync(_gameConfig);
                    if (IsDisposed || Disposing)
                        return;
                    LoadAndDisplayStats();
                }
            }
            catch (Exception ex)
            {
                LogAndShowErrorWithExceptionMessage($"Failed to refresh {PathConstants.GoldbergStatsJsonFileName}", ex);
            }
        }

        private void LoadAndDisplayStats()
        {
            if (!HasCurrentGameWithValidAppId())
                return;

            var service = ServiceLocator.GoldbergFilesService;
            _customStatsRawJson = service.LoadStats(_gameConfig.AppId) ?? string.Empty;

            if (lblCustomStatsDisplay != null)
            {
                lblCustomStatsDisplay.Text = service.FormatStatsForDisplay(_customStatsRawJson);
                LayoutStatsDisplayLabel();
            }
        }

        private void PnlStatsDisplayScroll_Resize(object sender, EventArgs e)
        {
            LayoutStatsDisplayLabel();
        }

        private void LayoutStatsDisplayLabel()
        {
            if (lblCustomStatsDisplay == null || pnlStatsDisplayScroll == null)
                return;
            var innerPad = lblCustomStatsDisplay.Padding.Left + lblCustomStatsDisplay.Padding.Right;
            var w = Math.Max(40, pnlStatsDisplayScroll.ClientSize.Width - innerPad);
            lblCustomStatsDisplay.MaximumSize = new Size(w, 0);
        }

        private string _initialAdditionalGoldbergFilesState = string.Empty;

        private void OnAddSubscribedGroup_Click(object sender, EventArgs e)
        {
            string groupId = txtSubscribedGroupIdEntry?.Text?.Trim();
            if (string.IsNullOrEmpty(groupId))
            {
                FormMessageBoxHelper.ShowIfAlive(this, "Enter a Steam group ID.", "Subscribed Groups", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!IsValidSteamGroupId(groupId))
            {
                FormMessageBoxHelper.ShowIfAlive(this, "Group ID must be a numeric Steam group ID.", "Subscribed Groups", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (AppendUniqueSubscribedGroupLine(lstSubscribedGroups, groupId))
                SetTextBoxText(txtSubscribedGroupIdEntry, string.Empty);
        }

        private void OnRemoveSubscribedGroup_Click(object sender, EventArgs e)
        {
            RemoveFromSubscribedGroupList(lstSubscribedGroups, txtSubscribedGroupIdEntry?.Text, matchLinePrefix: false);
        }

        private void LstSubscribedGroups_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isLoading || lstSubscribedGroups?.SelectedItem == null)
                return;

            SetTextBoxText(txtSubscribedGroupIdEntry, lstSubscribedGroups.SelectedItem.ToString());
        }

        private void OnAddSubscribedGroupClan_Click(object sender, EventArgs e)
        {
            string entry = txtSubscribedGroupClanEntry?.Text?.Trim();
            if (string.IsNullOrEmpty(entry))
            {
                FormMessageBoxHelper.ShowIfAlive(this, "Enter a clan line (group ID, name, and tag).", "Subscribed Clan Groups", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!TryFormatSubscribedGroupClanLine(entry, out string formattedLine, out string errorMessage))
            {
                FormMessageBoxHelper.ShowIfAlive(this, errorMessage, "Subscribed Clan Groups", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (AppendUniqueSubscribedGroupLine(lstSubscribedGroupsClans, formattedLine))
                SetTextBoxText(txtSubscribedGroupClanEntry, string.Empty);
        }

        private void OnRemoveSubscribedGroupClan_Click(object sender, EventArgs e)
        {
            RemoveFromSubscribedGroupList(lstSubscribedGroupsClans, txtSubscribedGroupClanEntry?.Text, matchLinePrefix: true);
        }

        private void LstSubscribedGroupsClans_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isLoading || lstSubscribedGroupsClans?.SelectedItem == null)
                return;

            string line = lstSubscribedGroupsClans.SelectedItem.ToString();
            SetTextBoxText(txtSubscribedGroupClanEntry, GetClanLineGroupId(line));
        }

        private static bool IsValidSteamGroupId(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
                return false;

            for (int i = 0; i < groupId.Length; i++)
            {
                if (groupId[i] < '0' || groupId[i] > '9')
                    return false;
            }

            return groupId.Length > 0;
        }

        private static bool TryFormatSubscribedGroupClanLine(string entry, out string formattedLine, out string errorMessage)
        {
            formattedLine = null;
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(entry))
            {
                errorMessage = "Enter a clan line (group ID, name, and tag).";
                return false;
            }

            string trimmed = entry.Trim();
            if (trimmed.IndexOf('\t') >= 0)
            {
                formattedLine = trimmed;
                return true;
            }

            string[] parts = trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && IsValidSteamGroupId(parts[0]))
            {
                formattedLine = parts[0] + "\t" + parts[1] + "\t" + parts[2];
                return true;
            }

            errorMessage = "Enter group ID, name, and clan tag separated by tabs or spaces.";
            return false;
        }

        private bool AppendUniqueSubscribedGroupLine(ListBox listBox, string line)
        {
            if (listBox == null || string.IsNullOrWhiteSpace(line))
                return false;

            foreach (var item in listBox.Items)
            {
                if (string.Equals(item?.ToString(), line, StringComparison.Ordinal))
                    return false;
            }

            listBox.Items.Add(line);
            NotifySubscribedGroupsChanged();
            return true;
        }

        private void RemoveFromSubscribedGroupList(ListBox listBox, string matchHint, bool matchLinePrefix)
        {
            if (listBox == null || listBox.Items.Count == 0)
                return;

            int index = listBox.SelectedIndex;
            if (index < 0)
            {
                string hint = matchHint?.Trim();
                if (!string.IsNullOrEmpty(hint))
                {
                    for (int i = 0; i < listBox.Items.Count; i++)
                    {
                        string line = listBox.Items[i]?.ToString() ?? string.Empty;
                        if (matchLinePrefix)
                        {
                            if (string.Equals(line, hint, StringComparison.Ordinal)
                                || line.StartsWith(hint + "\t", StringComparison.Ordinal)
                                || string.Equals(GetClanLineGroupId(line), hint, StringComparison.Ordinal))
                            {
                                index = i;
                                break;
                            }
                        }
                        else if (string.Equals(line, hint, StringComparison.Ordinal))
                        {
                            index = i;
                            break;
                        }
                    }
                }

                if (index < 0)
                    index = listBox.Items.Count - 1;
            }

            listBox.Items.RemoveAt(index);
            NotifySubscribedGroupsChanged();
        }

        private void NotifySubscribedGroupsChanged()
        {
            if (!_isLoading)
                CheckForChanges();
        }

        private void SetSubscribedGroupLines(ListBox listBox, string text)
        {
            if (listBox == null)
                return;

            listBox.BeginUpdate();
            listBox.Items.Clear();
            foreach (string line in SplitNonEmptyLines(text))
                listBox.Items.Add(line);
            listBox.EndUpdate();
        }

        private static string GetSubscribedGroupLinesText(ListBox listBox)
        {
            if (listBox == null || listBox.Items.Count == 0)
                return string.Empty;

            return string.Join(Environment.NewLine, listBox.Items.Cast<string>());
        }

        private static string GetClanLineGroupId(string line)
        {
            if (string.IsNullOrEmpty(line))
                return string.Empty;

            int tabIndex = line.IndexOf('\t');
            return tabIndex >= 0 ? line.Substring(0, tabIndex).Trim() : line.Trim();
        }

        private static List<string> SplitNonEmptyLines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();

            return text
                .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToList();
        }

        private void LoadAdditionalGoldbergFiles(bool forceFromDisk = false)
        {
            if (!HasCurrentGameWithValidAppId())
                return;
            if (!forceFromDisk && _editSidecarsApplied)
                return;

            try
            {
                var service = ServiceLocator.GoldbergFilesService;

                RefreshModsSummaryList();

                LoadGoldbergTextIntoListBox(lstSubscribedGroups, service.LoadSubscribedGroups);

                LoadGoldbergTextIntoListBox(lstSubscribedGroupsClans, service.LoadSubscribedGroupsClans);

                ReloadAchievementsPreviewFromDisk();

                LoadInventoryEditorFromDisk();
            }
            catch (Exception ex)
            {
                LogFailedLoad("additional Goldberg files", ex);
            }
        }

        private void LoadGoldbergTextIntoListBox(ListBox listBox, Func<ulong, string> loader)
        {
            if (listBox == null || loader == null || _gameConfig == null)
                return;

            SetSubscribedGroupLines(listBox, loader(_gameConfig.AppId));
        }

        private void LoadGoldbergTextIntoTextBox(TextBox textBox, Func<ulong, string> loader)
        {
            if (textBox == null || loader == null || _gameConfig == null)
                return;
            textBox.Text = loader(_gameConfig.AppId);
        }

        private void LoadSteamSettingsTextFileIntoTextBox(TextBox textBox, string fileName)
        {
            if (textBox == null || _gameConfig == null || string.IsNullOrWhiteSpace(fileName))
                return;

            textBox.Text = ServiceLocator.GoldbergFilesService.LoadSteamSettingsTextFile(_gameConfig.AppId, fileName);
        }

        private GoldbergFilesService.AdditionalFilesSaveRequest BuildAdditionalFilesSaveRequest()
        {
            return new GoldbergFilesService.AdditionalFilesSaveRequest
            {
                AppId = _gameConfig.AppId,
                IsEditMode = _isEditMode,
                HasSubscribedGroups = lstSubscribedGroups != null,
                SubscribedGroups = GetSubscribedGroupLinesText(lstSubscribedGroups),
                HasSubscribedGroupsClans = lstSubscribedGroupsClans != null,
                SubscribedGroupsClans = GetSubscribedGroupLinesText(lstSubscribedGroupsClans),
                HasAchievements = _isEditMode && !string.IsNullOrWhiteSpace(_achievementsRawJson),
                AchievementsJson = _achievementsRawJson ?? string.Empty,
                ItemsJson = GetInventoryItemsJsonForSave(),
                HasDefaultItems = false,
                DefaultItemsJson = null
            };
        }

        private void SaveAdditionalGoldbergFiles()
        {
            if (!HasCurrentGameWithValidAppId())
                return;

            try
            {
                SaveAdditionalFilesFromRequest(BuildAdditionalFilesSaveRequest(), this);
            }
            catch (Exception ex)
            {
                LogFailedSave("additional Goldberg files", ex);
            }
        }

        internal static void SaveAdditionalFilesFromRequest(
            GoldbergFilesService.AdditionalFilesSaveRequest request,
            IWin32Window messageOwner)
        {
            if (request == null || request.AppId == 0)
                return;

            var failures = ServiceLocator.GoldbergFilesService.SaveAdditionalFiles(request);
            foreach (var failure in failures)
            {
                if (failure == null || failure.Result == null || string.IsNullOrEmpty(failure.Result.ErrorMessage))
                    continue;

                string invalidJsonMessage = GoldbergFilesService.GetInvalidJsonMessageForAdditionalFile(failure.Key);
                if (!string.IsNullOrEmpty(invalidJsonMessage))
                    FormMessageBoxHelper.ShowIfAlive(messageOwner, invalidJsonMessage, "Invalid JSON", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            if (failures.Count > 0)
            {
                string detail = string.Join(Environment.NewLine, failures
                    .Select(f => f.Result != null ? f.Result.ErrorMessage : string.Empty)
                    .Where(s => !string.IsNullOrEmpty(s)));
                Program.LogService?.LogWarning("Some additional Goldberg files failed to save: " + detail);
            }
        }

        private string[] GetAdditionalGoldbergFilesStateParts()
        {
            return new[]
            {
                GetSubscribedGroupLinesText(lstSubscribedGroups),
                GetSubscribedGroupLinesText(lstSubscribedGroupsClans),
                _achievementsRawJson ?? string.Empty,
                GetInventoryItemsStateFingerprint()
            };
        }

        private async void OnSave_Click(object sender, EventArgs e)
        {
            if (_gameConfig == null)
            {
                FormMessageBoxHelper.ShowIfAlive(this, "No game configuration loaded.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            btnSave.Enabled = false;
            var restoreSaveButtonState = true;
            var formHiddenForSave = false;
            try
            {
                if (txtAppID == null || !ulong.TryParse(txtAppID.Text.Trim(), out ulong appId) || appId == 0)
                {
                    FormMessageBoxHelper.ShowIfAlive(this, "Please enter a valid non-zero Steam App ID.", "Invalid App ID", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                NormalizeExecutableAndWorkingDirRelativeToGameFolder();

                if (txtGameName != null)
                    _gameConfig.AppName = txtGameName.Text.Trim();
                if (txtGameFolder != null)
                    _gameConfig.StartFolder = txtGameFolder.Text.Trim();
                if (txtGameExecutable != null)
                    _gameConfig.Path = txtGameExecutable.Text.Trim();
                if (txtLaunchParameters != null)
                    _gameConfig.Parameters = txtLaunchParameters.Text.Trim();
                if (txtWorkingDirectory != null)
                    _gameConfig.WorkingDirectory = txtWorkingDirectory.Text.Trim();
                if (txtCustomIcon != null)
                    _gameConfig.CustomIcon = txtCustomIcon.Text.Trim();
                _gameConfig.AppId = appId;
                ApplyLaunchModeFromUiToGameConfig();

                GoldbergLaunchModeAvailability launchModeAvailability = _gameLaunchService.GetLaunchModeAvailability(_gameConfig);
                if (!launchModeAvailability.IsAvailable(_gameConfig.LaunchMode))
                {
                    FormMessageBoxHelper.ShowIfAlive(
                        this,
                        "The selected Goldberg launch mode is not available. Run Goldberg Update or choose another mode.",
                        "Launch Mode Unavailable",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                var validation = _gameDataService.ValidateGameConfig(_gameConfig);
                if (!validation.IsValid)
                {
                    FormMessageBoxHelper.ShowIfAlive(this, $"Validation failed: {validation.ErrorMessage}", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (_isEditMode)
                {
                    GameSettingsSnapshot capturedSnapshot = GetSettingsFromForm();
                    GoldbergFilesService.AdditionalFilesSaveRequest capturedAdditional = BuildAdditionalFilesSaveRequest();
                    ulong editAppId = _gameConfig.AppId;
                    Dictionary<long, string> capturedDlc = txtDLCList != null
                        ? DlcService.ParseDlcListText(txtDLCList.Text, _gameConfig.PreFetchedDlcData)
                        : null;
                    if (capturedDlc == null && _gameConfig.PreFetchedDlcData != null && _gameConfig.PreFetchedDlcData.Count > 0)
                        capturedDlc = _gameConfig.PreFetchedDlcData;

                    HideFormForSaveIfVisible(ref formHiddenForSave);

                    var editFormSaveRequest = new GameSettingsSaveRequest
                    {
                        GameConfig = _gameConfig,
                        IsEditMode = true,
                        Metadata = _metadata,
                        CustomStatsRawJson = _customStatsRawJson,
                        TaskReportService = _taskReportService,
                        BuildSnapshot = () => capturedSnapshot,
                        ResolveAchievementLanguage = ResolveLanguageForAchievementGeneration,
                        SaveDlcAndPaths = () =>
                        {
                            var saveResult = ServiceLocator.GoldbergFilesService.SaveAppConfigDlcAndPaths(editAppId, capturedDlc, null);
                            if (!saveResult.IsSuccess)
                                LogSaveResultWarning(saveResult);
                        },
                        SaveAdditionalGoldbergFiles = () => SaveAdditionalFilesFromRequest(capturedAdditional, this),
                        OnSuccessfulSaveCompleted = _onSaveCompleted
                    };

                    var editPostSaveResult = await _gameSaveWriter.SaveEditAsync(new GameSaveEditRequest
                    {
                        GameConfig = _gameConfig,
                        InitialGameConfig = _initialGameConfig,
                        FormSaveRequest = editFormSaveRequest,
                        CredentialsTouched = HaveCredentialsChanged(),
                        OnSuccessfulSaveCompleted = _onSaveCompleted
                    }).ConfigureAwait(true);

                    if (IsDisposed || Disposing)
                        return;

                    if (!editPostSaveResult.IsSuccess)
                    {
                        if (editPostSaveResult.HasCustomStatsJsonError)
                        {
                            FormMessageBoxHelper.ShowIfAlive(this, "Custom stats contain invalid JSON. Please fix the format before saving.", "Invalid JSON", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        else if (!string.IsNullOrWhiteSpace(editPostSaveResult.ErrorMessage))
                        {
                            FormMessageBoxHelper.ShowIfAlive(this, $"Failed to save game: {editPostSaveResult.ErrorMessage}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        return;
                    }

                    (_taskReportService as TaskReportService)?.Clear();
                    StoreInitialState();
                    btnSave.Enabled = false;
                    restoreSaveButtonState = false;
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                    return;
                }

                if (!TryConfirmNoDuplicateNewEntry())
                {
                    if (EditExistingGameGuid != Guid.Empty)
                        restoreSaveButtonState = false;
                    return;
                }

                PendingAddSave = BuildPendingAddSave();
                HideFormForSaveIfVisible(ref formHiddenForSave);
                restoreSaveButtonState = false;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                LogAndShowErrorWithExceptionMessage("Error saving game", ex);
            }
            finally
            {
                if (restoreSaveButtonState)
                    RestoreFormAfterFailedSaveIfHidden(formHiddenForSave);

                if (restoreSaveButtonState && IsHandleCreated && !IsDisposed && !Disposing)
                    CheckForChanges();
            }
        }

        private void HideFormForSaveIfVisible(ref bool formHiddenForSave)
        {
            if (formHiddenForSave || !Visible || IsDisposed || Disposing)
                return;

            Hide();
            formHiddenForSave = true;
        }

        private void RestoreFormAfterFailedSaveIfHidden(bool formHiddenForSave)
        {
            if (!formHiddenForSave || IsDisposed || Disposing || Visible)
                return;

            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void OnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private bool TryConfirmNoDuplicateNewEntry()
        {
            if (string.IsNullOrWhiteSpace(_gameConfig?.Path))
                return true;

            GameConfig duplicatePath = _gameDataService.FindDuplicateByExecutable(_gameConfig);
            if (duplicatePath != null)
                return HandleDuplicatePrompt(DuplicateExecutableDialogHelper.Show(this, duplicatePath), duplicatePath.GameGuid);

            if (_gameConfig.AppId > 0)
            {
                GameConfig duplicateAppId = _gameDataService.GetGameByAppIdAndPath(_gameConfig.AppId, _gameConfig.Path);
                if (duplicateAppId != null)
                    return HandleDuplicatePrompt(ShowDuplicateAppIdDialog(duplicateAppId, _gameConfig.AppId), duplicateAppId.GameGuid);
            }

            return true;
        }

        private bool HandleDuplicatePrompt(DialogResult result, Guid gameGuid)
        {
            if (result == DialogResult.Yes)
            {
                EditExistingGameGuid = gameGuid;
                DialogResult = DialogResult.Retry;
                Close();
            }

            return false;
        }

        private DialogResult ShowDuplicateAppIdDialog(GameConfig duplicateGame, ulong appId)
        {
            return FormMessageBoxHelper.ShowDialogIfAlive(
                this,
                $"A game with App ID {appId} already exists:\n\n{duplicateGame.AppName}\n\nWould you like to edit the existing game instead?",
                "Duplicate App ID",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);
        }

        private PendingAddGameSave BuildPendingAddSave()
        {
            GameSettingsSnapshot snapshot = GetSettingsFromForm();
            ulong appId = _gameConfig.AppId;
            Dictionary<long, string> dlcData = txtDLCList != null
                ? DlcService.ParseDlcListText(txtDLCList.Text, _gameConfig.PreFetchedDlcData)
                : null;
            if (dlcData == null && _gameConfig.PreFetchedDlcData != null && _gameConfig.PreFetchedDlcData.Count > 0)
                dlcData = _gameConfig.PreFetchedDlcData;

            return new PendingAddGameSave
            {
                GameConfig = _gameConfig,
                Metadata = _metadata,
                AchievementPreview = _addBundle?.AchievementPreview ?? AchievementPreviewKind.NoApiKey,
                SettingsSnapshot = snapshot,
                CustomStatsRawJson = _customStatsRawJson ?? string.Empty,
                CredentialsTouched = HaveCredentialsChanged(),
                AdditionalFilesSaveRequest = BuildAdditionalFilesSaveRequest(),
                SaveDlcAndPaths = () =>
                {
                    var saveResult = ServiceLocator.GoldbergFilesService.SaveAppConfigDlcAndPaths(appId, dlcData, null);
                    if (!saveResult.IsSuccess)
                        LogSaveResultWarning(saveResult);
                },
                OnAssetsDownloaded = _onSaveCompleted,
                OnSuccessfulSaveCompleted = _onSaveCompleted
            };
        }

        private void StoreInitialState()
        {
            if (_gameConfig != null)
            {
                _initialGameConfig = new GameConfig
                {
                    GameGuid = _gameConfig.GameGuid,
                    AppId = _gameConfig.AppId,
                    AppName = _gameConfig.AppName,
                    StartFolder = _gameConfig.StartFolder,
                    Path = _gameConfig.Path,
                    Parameters = _gameConfig.Parameters,
                    WorkingDirectory = _gameConfig.WorkingDirectory,
                    CustomIcon = _gameConfig.CustomIcon,
                    LaunchMode = _gameConfig.LaunchMode
                };
                if (txtAppID != null && ulong.TryParse(txtAppID.Text.Trim(), out ulong appIdFromForm))
                    _initialGameConfig.AppId = appIdFromForm;
                if (txtGameName != null)
                    _initialGameConfig.AppName = txtGameName.Text.Trim();
                if (txtGameFolder != null)
                    _initialGameConfig.StartFolder = txtGameFolder.Text.Trim();
                if (txtGameExecutable != null)
                    _initialGameConfig.Path = txtGameExecutable.Text.Trim();
                if (txtLaunchParameters != null)
                    _initialGameConfig.Parameters = txtLaunchParameters.Text.Trim();
                if (txtWorkingDirectory != null)
                    _initialGameConfig.WorkingDirectory = txtWorkingDirectory.Text.Trim();
                if (txtCustomIcon != null)
                    _initialGameConfig.CustomIcon = (txtCustomIcon.Text ?? string.Empty).Trim();
                ApplyLaunchModeFromUiToGameConfig();
                if (_gameConfig != null)
                    _initialGameConfig.LaunchMode = _gameConfig.LaunchMode;
            }

            _initialSettingsSnapshot = GetSettingsFromForm();
            _initialCredentialTicket = _initialSettingsSnapshot?.User?.Ticket ?? string.Empty;
            _initialCredentialAlt = _initialSettingsSnapshot?.User?.AltSteamId ?? string.Empty;
            _initialDlcList = txtDLCList?.Text ?? string.Empty;
            _initialCustomStats = _customStatsRawJson ?? string.Empty;
            _initialAdditionalGoldbergFilesState = GoldbergFilesService.ComposeAdditionalFilesState(GetAdditionalGoldbergFilesStateParts());

        }

        private void Control_Changed(object sender, EventArgs e)
        {
            if (!_isLoading)
                CheckForChanges();
        }

        private bool HaveCredentialsChanged()
        {
            GameSettingsSnapshot current = GetSettingsFromForm();
            return !string.Equals(current.User?.Ticket ?? string.Empty, _initialCredentialTicket ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(current.User?.AltSteamId ?? string.Empty, _initialCredentialAlt ?? string.Empty, StringComparison.Ordinal);
        }

        private void CheckForChanges()
        {
            if (!_isEditMode)
            {
                btnSave.Enabled = true;
                return;
            }

            btnSave.Enabled = HasChanges();
        }

        private bool HasChanges()
        {
            if (_gameConfig != null && _initialGameConfig != null)
            {
                if (txtGameName != null && txtGameName.Text.Trim() != (_initialGameConfig.AppName ?? string.Empty))
                    return true;
                if (txtGameFolder != null && txtGameFolder.Text.Trim() != (_initialGameConfig.StartFolder ?? string.Empty))
                    return true;
                if (!LaunchOptionService.LaunchFieldsMatchBaseline(
                    txtGameExecutable != null ? txtGameExecutable.Text : null,
                    txtLaunchParameters != null ? txtLaunchParameters.Text : null,
                    txtWorkingDirectory != null ? txtWorkingDirectory.Text : null,
                    _initialGameConfig))
                    return true;
                if (txtAppID != null && ulong.TryParse(txtAppID.Text.Trim(), out ulong appId) && appId != _initialGameConfig.AppId)
                    return true;
                if (txtCustomIcon != null && (txtCustomIcon.Text ?? string.Empty).Trim() != (_initialGameConfig.CustomIcon ?? string.Empty).Trim())
                    return true;
                if (GetSelectedLaunchModeFromUi() != _initialGameConfig.LaunchMode)
                    return true;
            }

            if (_isEditMode && _initialSettingsSnapshot != null)
            {
                var currentSnapshot = GetSettingsFromForm();
                if (!AreSettingsSnapshotsEqual(currentSnapshot, _initialSettingsSnapshot))
                    return true;

                if ((txtDLCList?.Text ?? string.Empty) != _initialDlcList)
                    return true;
                if ((_customStatsRawJson ?? string.Empty) != _initialCustomStats)
                    return true;
                if (GoldbergFilesService.ComposeAdditionalFilesState(GetAdditionalGoldbergFilesStateParts()) != _initialAdditionalGoldbergFilesState)
                    return true;
            }

            return false;
        }

        private bool AreSettingsSnapshotsEqual(GameSettingsSnapshot a, GameSettingsSnapshot b)
        {
            if (a == null && b == null)
                return true;
            if (a == null || b == null)
                return false;

            if (!MainSettingsScopes.NetworkSlicesEqual(a.Main, b.Main))
                return false;

            if (!MainSettingsScopes.StatsAchievementsSlicesEqual(a.Main, b.Main))
                return false;

            if ((a.User.AccountName ?? string.Empty) != (b.User.AccountName ?? string.Empty) ||
                (a.User.AccountSteamId ?? string.Empty) != (b.User.AccountSteamId ?? string.Empty) ||
                (a.User.Ticket ?? string.Empty) != (b.User.Ticket ?? string.Empty) ||
                (a.User.AltSteamId ?? string.Empty) != (b.User.AltSteamId ?? string.Empty) ||
                a.User.AltSteamIdCount != b.User.AltSteamIdCount ||
                (a.User.Language ?? string.Empty) != (b.User.Language ?? string.Empty) ||
                (a.User.IpCountry ?? string.Empty) != (b.User.IpCountry ?? string.Empty) ||
                (a.User.ClanTag ?? string.Empty) != (b.User.ClanTag ?? string.Empty))
                return false;

            if (a.App.UnlockAllDLC != b.App.UnlockAllDLC ||
                a.App.IsBetaBranch != b.App.IsBetaBranch ||
                (a.App.BranchName ?? string.Empty).Trim() != (b.App.BranchName ?? string.Empty).Trim())
                return false;

            return true;
        }

        private string _globalLanguagePlaceholder = string.Empty;

        private string ResolveLanguageForAchievementGeneration(GameSettingsSnapshot snapshot)
        {
            if (cmbForceLanguage != null &&
                cmbForceLanguage.SelectedItem != null &&
                cmbForceLanguage.SelectedItem.ToString() == SteamLanguageDisplayHelper.UseGlobalSettingOption)
            {
                string language = _emulatorConfigService.GetLanguageForAchievements(_gameConfig.AppId);
                snapshot.User.Language = language;
                return language;
            }

            if (!string.IsNullOrEmpty(snapshot.User.Language))
                return snapshot.User.Language;

            string fallback = _emulatorConfigService.GetLanguageForAchievements(_gameConfig.AppId);
            snapshot.User.Language = fallback;
            return fallback;
        }

        private void InitializeLanguagePlaceholderFromGlobal(UserSettings globalSettings)
        {
            _globalLanguagePlaceholder = !string.IsNullOrEmpty(globalSettings.Language)
                ? SteamLanguageDisplayHelper.ToSimpleDisplayName(globalSettings.Language)
                : "English";
        }

        private void InitializeLanguageComboBoxPlaceholder()
        {
            if (cmbForceLanguage == null)
                return;

            cmbForceLanguage.DrawMode = DrawMode.OwnerDrawFixed;
            cmbForceLanguage.DrawItem += CmbForceLanguage_DrawItem;
            cmbForceLanguage.SelectedIndexChanged += CmbForceLanguage_SelectedIndexChanged;
            cmbForceLanguage.Paint += CmbForceLanguage_Paint;
            UpdateLanguageComboBoxPlaceholder();
        }

        private void CmbForceLanguage_Paint(object sender, PaintEventArgs e)
        {
            if (cmbForceLanguage == null || cmbForceLanguage.SelectedIndex < 0 || cmbForceLanguage.DroppedDown)
                return;

            string selectedText = cmbForceLanguage.Items[cmbForceLanguage.SelectedIndex].ToString();

            if (selectedText == SteamLanguageDisplayHelper.UseGlobalSettingOption && !string.IsNullOrEmpty(_globalLanguagePlaceholder))
            {
                using (var brush = new SolidBrush(cmbForceLanguage.BackColor))
                    e.Graphics.FillRectangle(brush, e.ClipRectangle);

                ControlPaint.DrawBorder(e.Graphics, e.ClipRectangle,
                    cmbForceLanguage.Enabled ? SystemColors.WindowFrame : SystemColors.ControlDark,
                    ButtonBorderStyle.Solid);

                var buttonRect = new Rectangle(cmbForceLanguage.Width - 17, 0, 17, cmbForceLanguage.Height);
                ControlPaint.DrawComboButton(e.Graphics, buttonRect,
                    cmbForceLanguage.Enabled ? ButtonState.Normal : ButtonState.Inactive);

                var textRect = new Rectangle(3, 0, cmbForceLanguage.Width - 20, cmbForceLanguage.Height);
                var placeholderColor = _themeService != null
                    ? _themeService.GetThemeColors(_themeService.EffectiveTheme).DisabledForeground
                    : Color.Gray;
                TextRenderer.DrawText(e.Graphics, _globalLanguagePlaceholder, cmbForceLanguage.Font, textRect,
                    placeholderColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }

        private void CmbForceLanguage_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (cmbForceLanguage == null || e.Index < 0)
                return;

            e.DrawBackground();

            string itemText = cmbForceLanguage.Items[e.Index].ToString();
            Color textColor = e.ForeColor;

            if (itemText == SteamLanguageDisplayHelper.UseGlobalSettingOption && !string.IsNullOrEmpty(_globalLanguagePlaceholder))
            {
                var placeholderColor = _themeService != null
                    ? _themeService.GetThemeColors(_themeService.EffectiveTheme).DisabledForeground
                    : Color.Gray;
                using (var placeholderBrush = new SolidBrush(placeholderColor))
                    e.Graphics.DrawString(_globalLanguagePlaceholder, e.Font, placeholderBrush, e.Bounds.X, e.Bounds.Y);
            }
            else
            {
                using (var brush = new SolidBrush(textColor))
                    e.Graphics.DrawString(itemText, e.Font, brush, e.Bounds.X, e.Bounds.Y);
            }

            e.DrawFocusRectangle();
        }

        private void CmbForceLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_isLoading)
            {
                UpdateLanguageComboBoxPlaceholder();
                CheckForChanges();
            }
        }

        private void UpdateLanguageComboBoxPlaceholder()
        {
            if (cmbForceLanguage == null)
                return;
            cmbForceLanguage.Invalidate();
        }

        private void LoadSupportedLanguagesFromFile()
        {
            if (!HasCurrentGameWithValidAppId() || cmbForceLanguage == null)
                return;

            string fallbackLanguages = _metadata != null ? _metadata.SupportedLanguages : null;
            try
            {
                var service = ServiceLocator.GoldbergFilesService;
                var languages = service.LoadSupportedLanguages(_gameConfig.AppId);

                if (!string.IsNullOrEmpty(languages))
                {
                    PopulateLanguageDropdown(languages);
                    return;
                }
            }
            catch (Exception ex)
            {
                LogFailedLoad(PathConstants.GoldbergSupportedLanguagesFileName, ex);
            }

            if (!string.IsNullOrEmpty(fallbackLanguages))
                PopulateLanguageDropdown(fallbackLanguages);
        }

        private void PopulateLanguageDropdown(string supportedLanguages)
        {
            if (cmbForceLanguage == null || string.IsNullOrEmpty(supportedLanguages))
                return;

            var languages = SteamLanguageDisplayHelper.ParseSupportedLanguageDisplayList(supportedLanguages, _globalLanguagePlaceholder);

            if (cmbForceLanguage.Items.Count == 0)
            {
                cmbForceLanguage.Items.Add(SteamLanguageDisplayHelper.UseGlobalSettingOption);
                foreach (var lang in languages)
                    cmbForceLanguage.Items.Add(lang);

                cmbForceLanguage.SelectedIndex = 0;
            }
            else
            {
                if (!string.IsNullOrEmpty(_globalLanguagePlaceholder))
                {
                    int globalLangIndex = cmbForceLanguage.Items.IndexOf(_globalLanguagePlaceholder);
                    if (globalLangIndex >= 0)
                    {
                        if (cmbForceLanguage.SelectedIndex == globalLangIndex)
                        {
                            int useGlobalIndex = GetUseGlobalSettingIndex();
                            if (useGlobalIndex >= 0)
                                cmbForceLanguage.SelectedIndex = useGlobalIndex;
                        }
                        cmbForceLanguage.Items.RemoveAt(globalLangIndex);
                    }
                }

                foreach (var lang in languages)
                {
                    if (!cmbForceLanguage.Items.Cast<string>().Any(i => i.Equals(lang, StringComparison.OrdinalIgnoreCase)))
                        cmbForceLanguage.Items.Add(lang);
                }

                if (cmbForceLanguage.SelectedIndex < 0)
                {
                    int index = GetUseGlobalSettingIndex();
                    if (index >= 0)
                        cmbForceLanguage.SelectedIndex = index;
                }
            }
        }

        private int GetUseGlobalSettingIndex()
        {
            return cmbForceLanguage != null
                ? cmbForceLanguage.Items.IndexOf(SteamLanguageDisplayHelper.UseGlobalSettingOption)
                : -1;
        }

        private void InitializeCredentialPlaceholders(UserSettings globalSettings)
        {
            _placeholderHelper.SetupPlaceholder(txtForceAccountName, globalSettings.AccountName);
            _placeholderHelper.SetupPlaceholder(txtForceSteamId, globalSettings.AccountSteamId);
            _placeholderHelper.SetupPlaceholder(txtAltSteamId, globalSettings.AltSteamId);
        }

        private void UpdateAltSteamIdPlaceholder()
        {
            if (txtAltSteamId == null)
                return;

            string forceVal = null;
            if (txtForceSteamId != null && !string.IsNullOrWhiteSpace(txtForceSteamId.Text))
            {
                forceVal = _placeholderHelper.IsPlaceholderText(txtForceSteamId)
                    ? _placeholderHelper.GetPlaceholderText(txtForceSteamId)
                    : txtForceSteamId.Text.Trim();
            }

            string altPlaceholder;
            if (!string.IsNullOrEmpty(forceVal))
                altPlaceholder = forceVal;
            else
                altPlaceholder = LoadGlobalUserSettings().AltSteamId;

            if (string.IsNullOrWhiteSpace(txtAltSteamId.Text) || _placeholderHelper.IsPlaceholderText(txtAltSteamId))
                _placeholderHelper.UpdatePlaceholderAndDisplay(txtAltSteamId, altPlaceholder);
            else
                _placeholderHelper.SetPlaceholderText(txtAltSteamId, altPlaceholder);
        }

        private void TxtForceSteamId_TextChanged(object sender, EventArgs e)
        {
            if (txtAltSteamId != null && (string.IsNullOrWhiteSpace(txtAltSteamId.Text) || _placeholderHelper.IsPlaceholderText(txtAltSteamId)))
                UpdateAltSteamIdPlaceholder();
        }

        private void TxtForceSteamId_Leave(object sender, EventArgs e)
        {
            UpdateAltSteamIdPlaceholder();
        }

        private UserSettings LoadGlobalUserSettings()
        {
            try
            {
                var goldbergCfgService = ServiceLocator.GoldbergCfgService;
                if (goldbergCfgService != null)
                    return goldbergCfgService.LoadGlobalUserSettings();
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to load global user settings for placeholders: {ex.Message}");
            }

            return new UserSettings();
        }

    }
}
