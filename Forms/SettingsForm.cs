using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Properties;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Forms
{
    public partial class SettingsForm : Form
    {
        private readonly string _avatarPath;
        private readonly AppDataService _appDataService;
        private readonly GoldbergCfgService _goldbergCfgService;
        private readonly ThemeService _themeService;
        private readonly SteamApiKeyService _apiKeyService;
        private const string SoundPreviewPlaySymbol = "▶";
        private const string SoundPreviewStopSymbol = "⏹";

        private readonly object _sound1PreviewSync = new object();
        private readonly object _sound2PreviewSync = new object();
        private int _sound1PreviewSessionId;
        private int _sound2PreviewSessionId;
        private bool _sound1PreviewActive;
        private bool _sound2PreviewActive;
        private string _initialApiKey;
        private Models.OverlaySettings _initialOverlaySettings;
        private Models.UserSettings _initialUserSettings;
        private MainSettings _initialMainSettings;
        private bool _isLoading = false;
        private static readonly Models.OverlaySettings _overlayDefaults = new Models.OverlaySettings();
        private string _persistedCustomLocalSavePath = string.Empty;
        private string _persistedSavesFolderName = ApplicationConstants.DefaultSavesFolderName;
        private int _previousSaveLocationIndex = -1;

        private const int SaveLocationDefault = 0;
        private const int SaveLocationPortable = 1;
        private const int SaveLocationSteamUserdata = 2;
        private const int SaveLocationCustom = 3;
        private Dictionary<string, string> _steamIdProfiles;
        private string _lastValidatedApiKey = string.Empty;
        private bool _lastApiKeyValidationSucceeded = false;
        private bool _isApiKeyValidationInProgress = false;
        private readonly Timer _apiKeyValidationClearTimer;
        private const int ApiKeyValidationMessageDisplayMs = 3000;

        private enum PendingAvatarChange
        {
            None,
            ReplaceFromFile,
            ResetToDefault
        }

        private PendingAvatarChange _pendingAvatarChange = PendingAvatarChange.None;
        private string _pendingAvatarSourcePath;

        public event EventHandler ApiKeyValidationStatusChanged;

        public SettingsForm() 
            : this(ServiceLocator.AppDataService, ServiceLocator.GoldbergCfgService, ServiceLocator.ThemeService)
        {
        }

        public SettingsForm(AppDataService appDataService, GoldbergCfgService goldbergCfgService)
            : this(appDataService, goldbergCfgService, ServiceLocator.ThemeService)
        {
        }

        public SettingsForm(AppDataService appDataService, GoldbergCfgService goldbergCfgService, ThemeService themeService)
            : this(appDataService, goldbergCfgService, themeService, null)
        {
        }

        public SettingsForm(AppDataService appDataService, GoldbergCfgService goldbergCfgService, ThemeService themeService, SteamApiKeyService apiKeyService)
        {
            InitializeComponent();
            InitializeSaveLocationComboItems();

            _appDataService = appDataService ?? throw new ArgumentNullException(nameof(appDataService));
            _goldbergCfgService = goldbergCfgService ?? throw new ArgumentNullException(nameof(goldbergCfgService));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _apiKeyService = apiKeyService ?? ServiceLocator.SteamApiKeyService;
            _avatarPath = PathConstants.GlobalAccountAvatarPath;
            _apiKeyValidationClearTimer = new Timer { Interval = ApiKeyValidationMessageDisplayMs };
            _apiKeyValidationClearTimer.Tick += OnApiKeyValidationClearTimer_Tick;

            if (DesignTimeHelper.IsDesignTime)
                return;

            WireEmulatorTab();

            ApplyTheme();
            ConfigureSoundPreviewPlayStopButtons();
            _themeService.ThemeChanged += ThemeService_ThemeChanged;

            InitializeTooltips();

            if (grpSteamWebApi != null)
            {
                grpSteamWebApi.Layout += GrpSteamWebApi_Layout;
                LayoutApiKeyValidationLabel();
            }

            if (btnRemoveSteamIdProfile != null)
            {
                btnRemoveSteamIdProfile.Text = "🗑";
                toolTip?.SetToolTip(btnRemoveSteamIdProfile, "Remove selected Steam ID profile");
                btnRemoveSteamIdProfile.Click += OnRemoveSteamIdProfile_Click;
            }

            if (txtSteamID != null)
            {
                txtSteamID.SelectedIndexChanged += TxtSteamID_SelectedIndexChanged;
            }
        }

        private void LoadSteamIdProfilesIntoCombo()
        {
            if (txtSteamID == null)
                return;

            _steamIdProfiles = _appDataService.LoadSteamIdProfiles();

            var currentText = txtSteamID.Text;
            _isLoading = true;
            try
            {
                txtSteamID.BeginUpdate();
                txtSteamID.Items.Clear();

                foreach (var pair in _steamIdProfiles
                    .OrderBy(p => p.Value, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => p.Key, StringComparer.Ordinal))
                {
                    txtSteamID.Items.Add(new SteamIdProfileListItem(pair.Key, pair.Value));
                }
            }
            finally
            {
                txtSteamID.EndUpdate();
                txtSteamID.Text = currentText;
                _isLoading = false;
            }
        }

        private void TxtSteamID_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isLoading || txtSteamID == null)
                return;

            if (!(txtSteamID.SelectedItem is SteamIdProfileListItem profile))
                return;

            _isLoading = true;
            try
            {
                txtSteamID.Text = profile.SteamId;
                if (txtUsername != null)
                    txtUsername.Text = profile.DisplayName ?? string.Empty;
            }
            finally
            {
                _isLoading = false;
            }

            CheckForChanges();

            if (cmbSaveLocation != null && cmbSaveLocation.SelectedIndex == SaveLocationSteamUserdata)
                RefreshSavePathDisplayText();
        }

        private void OnRemoveSteamIdProfile_Click(object sender, EventArgs e)
        {
            if (txtSteamID == null)
                return;

            var steamId = ResolveSteamIdFromCombo(txtSteamID);
            if (steamId.Length == 0)
                return;

            var confirm = FormMessageBoxHelper.ShowDialogIfAlive(this,
                $"Remove profile for SteamID '{steamId}'?",
                "Remove Profile",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
                return;

            var result = _appDataService.RemoveSteamIdProfile(steamId);
            if (!result.IsValid)
            {
                FormMessageBoxHelper.ShowIfAlive(this, result.ErrorMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            LoadSteamIdProfilesIntoCombo();

            _isLoading = true;
            try
            {
                txtSteamID.SelectedIndex = -1;
                txtSteamID.Text = string.Empty;
            }
            finally
            {
                _isLoading = false;
            }

            CheckForChanges();
        }

        private void InitializeTooltips()
        {
            if (toolTip == null)
                return;

            InitializeSteamSettingsTooltips();

            if (btnRandomizeSteamID != null)
            {
                toolTip.SetToolTip(
                    btnRandomizeSteamID,
                    "Generate a random Steam64 ID in the valid individual account range.");
            }

            if (lblProfileHint != null)
            {
                toolTip.SetToolTip(
                    lblProfileHint,
                    "Saved Steam ID profiles store your Steam64 ID and account name." + Environment.NewLine +
                    "Pick a profile to fill both fields; Save updates the profile for the current pair." + Environment.NewLine +
                    "Changing a game's App ID can make it store saves as a new user.");
            }

            if (lblApiKeyHint != null)
            {
                toolTip.SetToolTip(
                    lblApiKeyHint,
                    "Steam Web API key is required for:" + Environment.NewLine +
                    Environment.NewLine +
                    "- Achievements generation" + Environment.NewLine +
                    "- Items generation" + Environment.NewLine +
                    "- Workshop item descriptions");
            }

            ToolTipHelper.SetIfPresent(toolTip, btnRemoveApiKey, "Remove the saved Steam Web API key");
            ToolTipHelper.SetIfPresent(toolTip, btnSetAvatar, "Choose a custom avatar image (applied when you click Save)");
            ToolTipHelper.SetIfPresent(toolTip, btnClearAvatar, "Restore the default avatar (applied when you click Save)");
            ToolTipHelper.SetIfPresent(toolTip, btnResetColorBackground, "Reset background color to default");
            ToolTipHelper.SetIfPresent(toolTip, btnResetColorElements, "Reset elements color to default");
            ToolTipHelper.SetIfPresent(toolTip, btnResetColorHoveredElements, "Reset hovered elements color to default");
            ToolTipHelper.SetIfPresent(toolTip, btnResetColorActiveElements, "Reset active elements color to default");
            ToolTipHelper.SetIfPresent(toolTip, btnResetNotificationColor, "Reset notification color to default");
            ToolTipHelper.SetIfPresent(toolTip, btnResetColorStatsText, "Reset stats text color to default");
            ToolTipHelper.SetIfPresent(toolTip, btnResetColorStatsBackground, "Reset stats background color to default");
            ToolTipHelper.SetIfPresent(toolTip, btnOpenSoundsFolder, "Open the global overlay sounds folder in File Explorer");
            ToolTipHelper.SetIfPresent(toolTip, btnOpenFontsFolder, "Open the global overlay fonts folder in File Explorer");
            ToolTipHelper.SetIfPresent(toolTip, btnOpenControllerFolder, "Open the controller glyph images folder in File Explorer");
        }

        private void InitializeSteamSettingsTooltips()
        {
            if (toolTip == null)
                return;

            var bindings = new (Control Control, string Text)[]
            {
                (chkEnableExperimentalOverlay, "1=Enable the experimental overlay. May cause crashes."),
                (numHookDelay, "Seconds to wait before hooking the renderer (DirectX, OpenGL, etc.)."),
                (numRendererDetectorTimeout, "Timeout for renderer detection."),
                (chkDisableWarningAny, "1=Disable all overlay warnings."),
                (chkDisableWarningBadAppId, "1=Disable the bad App ID warning in the overlay."),
                (chkDisableWarningLocalSave, "1=Disable the overlay 'local_save_path detected' warning. Auto-enabled only for Steam userdata (Steam client) save location."),
                (cmbFontOverride, "Custom TrueType font path (relative to steam_settings/fonts or global GSE fonts)."),
                (numFontSize, "Global overlay font size."),
                (numFontSpacingX, "Extra horizontal spacing between font glyphs."),
                (numFontSpacingY, "Extra vertical spacing between font glyphs."),
                (numIconSize, "Achievement icon size in the overlay."),
                (chkUploadAchievementsToGPU, "1=Upload achievement icons to the GPU for display (disable if it causes FPS drops)."),
                (numNotificationRounding, "Notification corner roundness."),
                (numNotificationMarginX, "Horizontal margin for notifications."),
                (numNotificationMarginY, "Vertical margin for notifications."),
                (numNotificationAnimation, "Notification animation duration in seconds (0=off)."),
                (numNotificationDurationProgress, "Duration for achievement progress notifications."),
                (numNotificationDurationAchievement, "Duration for achievement unlocked notifications."),
                (numNotificationDurationInvitation, "Duration for friend invitation notifications."),
                (numNotificationDurationChat, "Duration for chat message notifications."),
                (cmbAchievementDateTimeFormat, "strftime format for achievement unlock date/time."),
                (chkDisableAchievementNotification, "1=Disable achievement notifications."),
                (chkDisableFriendNotification, "1=Disable friend invitation and message notifications."),
                (chkDisableAchievementProgress, "1=Disable achievement progress notifications."),
                (cmbPosAchievement, "Screen position for achievement notifications."),
                (cmbPosInvitation, "Screen position for invitation notifications."),
                (cmbPosChatMsg, "Screen position for chat notifications."),
                (numFpsAveragingWindow, "Frames used to average FPS/framerate display."),
                (chkAlwaysShowUserInfo, "1=Always show user info on the overlay."),
                (chkAlwaysShowFPS, "1=Always show FPS on the overlay."),
                (chkAlwaysShowFrametime, "1=Always show frametime on the overlay."),
                (chkAlwaysShowPlaytime, "1=Always show playtime on the overlay."),
                (numStatsPosX, "FPS overlay horizontal position (0=left, 1=right)."),
                (numStatsPosY, "FPS overlay vertical position (0=top, 1=bottom)."),
                (txtUsername, "Account name reported to games. Saved with the Steam ID as a profile when you click Save."),
                (txtSteamID, "Steam64 account ID. The list shows saved profiles as name and ID; you can still type a new ID."),
                (cmbLanguage, "Language reported to games (must be in supported_languages.txt)."),
                (cmbCountry, "ISO 3166-1 alpha-2 country code."),
                (cmbSaveLocation, "Sets [user::saves] in global configs.user.ini (gbe_fork format). Default: omit local_save_path (emulator uses global GSE Saves). Portable: local_save_path=./ . Steam userdata: absolute Steam\\userdata\\{Steam3AccountID}\\. Custom: your absolute path. Per-game configs.user.ini never stores save location."),
                (txtLocalSavePath, "Maps to configs.user.ini [user::saves] local_save_path when saved (Custom Path only; other modes show a preview)."),
                (txtSavesFolderName, "Maps to configs.user.ini [user::saves] saves_folder_name when local_save_path is empty (Default and Portable). Omitted for Steam userdata.")
            };

            foreach (var binding in bindings)
                ToolTipHelper.SetIfPresent(toolTip, binding.Control, ToolTipHelper.FormatDescription(binding.Text));
        }

        public void SetSelectedTab(int tabIndex)
        {
            if (tabIndex >= 0 && tabIndex < tabControl.TabCount)
                tabControl.SelectedIndex = tabIndex;
        }

        private void GrpSteamWebApi_Layout(object sender, LayoutEventArgs e)
        {
            LayoutApiKeyValidationLabel();
        }

        private void LayoutApiKeyValidationLabel()
        {
            if (lblApiKeyValidation == null || txtSteamWebApiKey == null || lnkSteamWebApiKey == null)
                return;

            const int gapBelowLink = 4;
            lblApiKeyValidation.Location = new Point(txtSteamWebApiKey.Left, lnkSteamWebApiKey.Bottom + gapBelowLink);
            lblApiKeyValidation.MaximumSize = new Size(Math.Max(100, txtSteamWebApiKey.Width), 0);
        }

        private void ApplyApiKeyStatusLabelOnLoad()
        {
            var status = _apiKeyService.GetStatus();
            var colors = _themeService?.GetThemeColors(_themeService.EffectiveTheme);
            var err = colors?.ErrorColor ?? Color.Red;
            var ok = colors?.SuccessColor ?? Color.Green;

            if (!status.HasKey)
                ClearApiKeyValidationMessage();
            else if (status.IsValid)
            {
                _lastValidatedApiKey = (_apiKeyService.GetApiKey() ?? string.Empty).Trim();
                _lastApiKeyValidationSucceeded = true;
                SetApiKeyValidationMessage("Current API key is valid.", ok);
            }
            else if (!status.HasValidFormat)
                SetApiKeyValidationMessage("Current API key is invalid (wrong format).", err);
            else
                SetApiKeyValidationMessage($"Current API key is invalid: {status.ErrorMessage}", err);
        }

        private void ClearApiKeyValidationMessage()
        {
            if (lblApiKeyValidation == null)
                return;

            _apiKeyValidationClearTimer?.Stop();
            var colors = _themeService?.GetThemeColors(_themeService.EffectiveTheme);
            lblApiKeyValidation.Text = string.Empty;
            lblApiKeyValidation.ForeColor = colors?.Foreground ?? SystemColors.ControlText;
        }

        private void SetApiKeyValidationMessage(string text, Color foreColor)
        {
            if (lblApiKeyValidation == null)
                return;

            lblApiKeyValidation.Text = text ?? string.Empty;
            lblApiKeyValidation.ForeColor = foreColor;
            LayoutApiKeyValidationLabel();
            if (string.IsNullOrEmpty(lblApiKeyValidation.Text))
            {
                _apiKeyValidationClearTimer?.Stop();
                return;
            }

            _apiKeyValidationClearTimer.Stop();
            _apiKeyValidationClearTimer.Start();
        }

        private void OnApiKeyValidationClearTimer_Tick(object sender, EventArgs e)
        {
            _apiKeyValidationClearTimer.Stop();
            ClearApiKeyValidationMessage();
        }

        private void SettingsForm_Load(object sender, EventArgs e)
        {
            LayoutApiKeyValidationLabel();
            txtSteamWebApiKey.Text = _apiKeyService.GetApiKey();
            ApplyApiKeyStatusLabelOnLoad();

            LoadAvatarImage();
            LoadFontList();

            cmbSound1File.SelectedIndexChanged += CmbSound1File_SelectedIndexChanged;
            cmbSound2File.SelectedIndexChanged += CmbSound2File_SelectedIndexChanged;

            LoadSoundList();
            LoadLanguageList();

            _isLoading = true;
            LoadGoldbergSettings();
            _previousSaveLocationIndex = cmbSaveLocation.SelectedIndex;
            StoreInitialState();
            WireUpChangeEvents();
            _isLoading = false;

            LoadSteamIdProfilesIntoCombo();
            btnSave.Enabled = false;
        }

        private void CmbSound1File_SelectedIndexChanged(object sender, EventArgs e)
        {
            CancelSound1Preview();
            CopySelectedSoundToDefault(cmbSound1File, PathConstants.SteamClientUiAchievementNotificationWav);
        }

        private void CmbSound2File_SelectedIndexChanged(object sender, EventArgs e)
        {
            CancelSound2Preview();
            CopySelectedSoundToDefault(cmbSound2File, PathConstants.SteamClientUiFriendNotificationWav);
        }

        private void CopySelectedSoundToDefault(ComboBox soundComboBox, string targetFileName)
        {
            if (_loadingSoundList || soundComboBox.SelectedIndex == -1)
                return;

            try
            {
                var selectedSound = soundComboBox.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(selectedSound))
                    return;

                var soundsPath = PathConstants.GlobalSoundsPath;
                var sourcePath = Path.Combine(soundsPath, selectedSound);
                var targetPath = Path.Combine(soundsPath, targetFileName);

                if (selectedSound != targetFileName && File.Exists(sourcePath))
                    File.Copy(sourcePath, targetPath, true);
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to copy selected sound to default: {ex.Message}");
            }
        }

        private async void OnSave_Click(object sender, EventArgs e)
        {
            var colors = _themeService?.GetThemeColors(_themeService.EffectiveTheme);
            var fg = colors?.Foreground ?? SystemColors.ControlText;
            var err = colors?.ErrorColor ?? Color.Red;
            var ok = colors?.SuccessColor ?? Color.Green;
            var warn = colors?.WarningColor ?? Color.Orange;

            ClearApiKeyValidationMessage();

            var apiKey = txtSteamWebApiKey.Text.Trim();
            var initialApiKey = (_initialApiKey ?? string.Empty).Trim();
            bool hasApiKeyChanged = !string.Equals(apiKey, initialApiKey, StringComparison.Ordinal);

            ValidationResult apiKeyResult = ValidationResult.Success();
            bool shouldPersistApiKey = hasApiKeyChanged;
            string apiKeySkipReason = null;

            if (hasApiKeyChanged && !string.IsNullOrEmpty(apiKey))
            {
                if (_isApiKeyValidationInProgress && !string.Equals(_lastValidatedApiKey, apiKey, StringComparison.Ordinal))
                {
                    shouldPersistApiKey = false;
                    apiKeySkipReason = "Validation is still in progress.";
                    SetApiKeyValidationMessage("Validation still running. Saving other settings; API key remains unchanged.", warn);
                }
                else if (!_apiKeyService.IsValidFormat(apiKey))
                {
                    shouldPersistApiKey = false;
                    apiKeySkipReason = "Invalid API key format.";
                    SetApiKeyValidationMessage("Invalid format. Saving other settings; API key remains unchanged.", warn);
                }
                else
                {
                    bool isValidatedKey = string.Equals(_lastValidatedApiKey, apiKey, StringComparison.Ordinal);
                    if (!isValidatedKey || !_lastApiKeyValidationSucceeded)
                    {
                        shouldPersistApiKey = false;
                        apiKeySkipReason = "API key has not passed validation.";
                        SetApiKeyValidationMessage("Validation failed or missing. Saving other settings; API key remains unchanged.", warn);
                    }
                    else
                    {
                        SetApiKeyValidationMessage("API key is valid.", ok);
                    }
                }
            }
            else
            {
                ClearApiKeyValidationMessage();
            }

            if (shouldPersistApiKey)
            {
                apiKeyResult = _apiKeyService.SetApiKey(apiKey);
                if (apiKeyResult.IsValid)
                    ApiKeyValidationStatusChanged?.Invoke(this, EventArgs.Empty);
            }

            var goldbergResult = SaveGoldbergSettings();
            var avatarResult = await ApplyPendingAvatarAsync().ConfigureAwait(true);

            if (apiKeyResult.IsValid && goldbergResult.IsSuccess && avatarResult.IsValid)
            {
                if (!string.IsNullOrEmpty(apiKeySkipReason))
                {
                    FormMessageBoxHelper.ShowIfAlive(this,
                        $"Settings were saved, but the API key was not updated.\nReason: {apiKeySkipReason}",
                        "API Key Not Updated",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }

                var steamId = txtSteamID != null ? txtSteamID.Text.Trim() : string.Empty;
                var name = txtUsername != null ? txtUsername.Text.Trim() : string.Empty;
                if (!string.IsNullOrEmpty(steamId) && !string.IsNullOrEmpty(name))
                {
                    _appDataService.UpsertSteamIdProfile(steamId, name);
                    LoadSteamIdProfilesIntoCombo();
                }

                StoreInitialState();
                btnSave.Enabled = false;
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                var errorMessages = new List<string>();
                if (!apiKeyResult.IsValid)
                    errorMessages.Add($"API Key: {apiKeyResult.ErrorMessage}");
                if (!goldbergResult.IsSuccess)
                    errorMessages.Add($"Goldberg Settings: {goldbergResult.ErrorMessage}");
                if (!avatarResult.IsValid)
                    errorMessages.Add($"Avatar: {avatarResult.ErrorMessage}");

                FormMessageBoxHelper.ShowIfAlive(this, $"Failed to save some settings:\n{string.Join("\n", errorMessages)}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnCancel_Click(object sender, EventArgs e)
        {
            DiscardPendingAvatarChanges();
            CheckForChanges();
        }

        private void OnRandomizeSteamID_Click(object sender, EventArgs e)
        {
            txtSteamID.Text = GenerateRandomSteam64Id();
            if (txtUsername != null)
                txtUsername.Text = ApplicationConstants.DefaultAccountName;
            if (cmbSaveLocation != null && cmbSaveLocation.SelectedIndex == SaveLocationSteamUserdata)
                RefreshSavePathDisplayText();
            if (!_isLoading)
                CheckForChanges();
        }

        private static string GenerateRandomSteam64Id()
        {
            var rng = new Random();
            ulong range = ApplicationConstants.SteamId64Max - ApplicationConstants.SteamId64Base + 1;
            ulong steamId = ApplicationConstants.SteamId64Base + (ulong)(rng.NextDouble() * range);
            return steamId.ToString();
        }

        private void OnColorElement_Click(object sender, EventArgs e) => ShowColorDialog(btnColorElements);

        private void OnColorElementHovered_Click(object sender, EventArgs e) => ShowColorDialog(btnColorHoveredElements);

        private void OnColorElementActive_Click(object sender, EventArgs e) => ShowColorDialog(btnColorActiveElements);

        private void OnColorStatsBackground_Click(object sender, EventArgs e) => ShowColorDialog(btnColorStatsBackground);

        private void OnColorStatsText_Click(object sender, EventArgs e) => ShowColorDialog(btnColorStatsText);

        private void OnColorNotification_Click(object sender, EventArgs e) => ShowColorDialog(btnColorNotification);

        private void OnColorBackground_Click(object sender, EventArgs e) => ShowColorDialog(btnColorBackground);

        private void ShowColorDialog(Button colorButton)
        {
            using (var colorDialog = new ColorDialog())
            {
                colorDialog.Color = colorButton.BackColor;
                colorDialog.FullOpen = true;
                if (colorDialog.ShowDialog() != DialogResult.OK)
                    return;
                var originalColor = colorButton.BackColor;
                var newColor = colorDialog.Color;
                colorButton.BackColor = Color.FromArgb(originalColor.A, newColor.R, newColor.G, newColor.B);
                if (!_isLoading)
                    CheckForChanges();
            }
        }

        private void OnResetColorNotification_Click(object sender, EventArgs e) =>
            btnColorNotification.BackColor = OverlayColorHelper.GetDefaultOverlayColor(_overlayDefaults, "Notification");

        private void OnResetColorBackground_Click(object sender, EventArgs e) =>
            btnColorBackground.BackColor = OverlayColorHelper.GetDefaultOverlayColor(_overlayDefaults, "Background");

        private void OnResetColorElements_Click(object sender, EventArgs e) =>
            btnColorElements.BackColor = OverlayColorHelper.GetDefaultOverlayColor(_overlayDefaults, "Element");

        private void OnResetColorHoveredElements_Click(object sender, EventArgs e) =>
            btnColorHoveredElements.BackColor = OverlayColorHelper.GetDefaultOverlayColor(_overlayDefaults, "ElementHovered");

        private void OnResetColorActiveElements_Click(object sender, EventArgs e) =>
            btnColorActiveElements.BackColor = OverlayColorHelper.GetDefaultOverlayColor(_overlayDefaults, "ElementActive");

        private void OnResetColorStatsBackground_Click(object sender, EventArgs e) =>
            btnColorStatsBackground.BackColor = OverlayColorHelper.GetDefaultOverlayColor(_overlayDefaults, "StatsBackground");

        private void OnResetColorStatsText_Click(object sender, EventArgs e) =>
            btnColorStatsText.BackColor = OverlayColorHelper.GetDefaultOverlayColor(_overlayDefaults, "StatsText");

        private void OnBrowseSavePath_Click(object sender, EventArgs e)
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select custom save folder location";
                folderDialog.ShowNewFolderButton = true;
                
                string browseStart = txtLocalSavePath.Text.Trim();
                if (!string.IsNullOrEmpty(browseStart) && Directory.Exists(browseStart))
                {
                    folderDialog.SelectedPath = browseStart;
                }

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    txtLocalSavePath.Text = folderDialog.SelectedPath;
                }
            }
        }

        private void OnOpenSaveFolder_Click(object sender, EventArgs e)
        {
            try
            {
                string folderPath = GetCurrentSaveFolderPath();
                
                if (string.IsNullOrEmpty(folderPath))
                {
                    string message = cmbSaveLocation != null && cmbSaveLocation.SelectedIndex == SaveLocationSteamUserdata
                        ? "Could not open the Steam userdata folder. Set a valid Steam64 ID on the User tab and ensure Steam is installed."
                        : "Unable to determine save folder location.";
                    FormMessageBoxHelper.ShowIfAlive(this, message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!Directory.Exists(folderPath))
                {
                    try
                    {
                        Directory.CreateDirectory(folderPath);
                    }
                    catch (Exception ex)
                    {
                        FormMessageBoxHelper.ShowIfAlive(this, $"Failed to create folder: {ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }

                ShellFolderHelper.OpenFolderForOwner(
                    this,
                    folderPath,
                    createIfMissing: true,
                    "Error",
                    "Failed to open save folder",
                    restrictToAppInstallTree: false);
            }
            catch (Exception ex)
            {
                FormMessageBoxHelper.ShowIfAlive(this, $"Failed to open save folder: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GetCurrentSaveFolderPath()
        {
            string savesFolderName = string.IsNullOrWhiteSpace(txtSavesFolderName.Text)
                ? ApplicationConstants.DefaultSavesFolderName
                : txtSavesFolderName.Text.Trim();

            if (cmbSaveLocation.SelectedIndex == SaveLocationDefault)
                return PathConstants.GetUserSavesRoot(savesFolderName);
            if (cmbSaveLocation.SelectedIndex == SaveLocationPortable)
                return Path.Combine(PathConstants.AppBaseDirectory, savesFolderName);

            if (cmbSaveLocation.SelectedIndex == SaveLocationSteamUserdata)
                return TryGetSteamUserdataAccountFolderPath();

            if (cmbSaveLocation.SelectedIndex == SaveLocationCustom)
            {
                string customPath = txtLocalSavePath.Text.Trim();
                if (!string.IsNullOrEmpty(customPath))
                {
                    string steamId = txtSteamID != null ? txtSteamID.Text.Trim() : string.Empty;
                    if (GoldbergSavePathHelper.UsesSteamUserdataLayout(customPath, steamId))
                        return TryGetSteamUserdataAccountFolderPath();

                    return Path.IsPathRooted(customPath)
                        ? Path.Combine(customPath, savesFolderName)
                        : Path.Combine(PathConstants.AppBaseDirectory, customPath, savesFolderName);
                }
            }

            return string.Empty;
        }

        private string TryGetSteamUserdataAccountFolderPath()
        {
            string steamId = txtSteamID != null ? txtSteamID.Text.Trim() : string.Empty;
            return GoldbergSavePathHelper.TryResolveSteamUserdataAccountDirectory(steamId, out string accountPath)
                ? accountPath
                : string.Empty;
        }

        private string GetPersistedLocalSavePathFromUi()
        {
            if (cmbSaveLocation.SelectedIndex == SaveLocationPortable)
                return "./";
            if (cmbSaveLocation.SelectedIndex == SaveLocationSteamUserdata)
                return TryGetSteamUserdataAccountFolderPath();

            if (cmbSaveLocation.SelectedIndex == SaveLocationCustom)
                return txtLocalSavePath.Text.Trim();
            return string.Empty;
        }

        private void OnBrowseFont_Click(object sender, EventArgs e)
        {
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Font Files (*.ttf;*.otf;*.woff;*.woff2)|*.ttf;*.otf;*.woff;*.woff2|All Files (*.*)|*.*";
                openFileDialog.Title = "Select Font File";
                openFileDialog.FilterIndex = 1;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var fontsPath = PathConstants.GlobalFontsPath;
                        Directory.CreateDirectory(fontsPath);

                        var fileName = Path.GetFileName(openFileDialog.FileName);
                        var destPath = Path.Combine(fontsPath, fileName);

                        File.Copy(openFileDialog.FileName, destPath, true);

                        LoadFontList();

                        int fontIndex = cmbFontOverride.FindStringExact(fileName);
                        if (fontIndex >= 0)
                            cmbFontOverride.SelectedIndex = fontIndex;
                    }
                    catch (Exception ex)
                    {
                        FormMessageBoxHelper.ShowIfAlive(this, $"Failed to copy font: {ex.Message}", "Error", 
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void LoadFontList()
        {
            try
            {
                var fontsPath = PathConstants.GlobalFontsPath;
                cmbFontOverride.Items.Clear();
                cmbFontOverride.Items.Add("Default");

                if (!Directory.Exists(fontsPath))
                    Directory.CreateDirectory(fontsPath);

                var fontExtensions = new[] { ".ttf", ".otf", ".woff", ".woff2" };
                var fontFiles = Directory.GetFiles(fontsPath)
                    .Where(file => fontExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    .Select(Path.GetFileName)
                    .OrderBy(name => name)
                    .ToArray();

                foreach (var fontFile in fontFiles)
                    cmbFontOverride.Items.Add(fontFile);
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to load font list: {ex.Message}");
            }
        }

        private void OnOpenSoundsFolder_Click(object sender, EventArgs e) =>
            OpenGlobalAssetFolder(PathConstants.GlobalSoundsPath, "sounds");

        private void OpenGlobalAssetFolder(string folderPath, string folderLabel)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath))
                {
                    FormMessageBoxHelper.ShowIfAlive(this, $"Unable to determine {folderLabel} folder location.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                ShellFolderHelper.OpenFolderForOwner(this, folderPath, createIfMissing: true, "Error",
                    $"Failed to open {folderLabel} folder");
            }
            catch (Exception ex)
            {
                FormMessageBoxHelper.ShowIfAlive(this, $"Failed to open {folderLabel} folder: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool _loadingSoundList = false;

        private void LoadSoundList()
        {
            try
            {
                _loadingSoundList = true;

                var soundsPath = PathConstants.GlobalSoundsPath;
                cmbSound1File.Items.Clear();
                cmbSound2File.Items.Clear();

                if (!Directory.Exists(soundsPath))
                    Directory.CreateDirectory(soundsPath);

                var soundExtensions = new[] { ".wav", ".mp3", ".ogg" };
                var soundFiles = Directory.GetFiles(soundsPath)
                    .Where(file => soundExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    .Select(Path.GetFileName)
                    .Where(name => !PathConstants.IsGoldbergOverlayNotificationSoundFileName(name))
                    .OrderBy(name => name)
                    .ToArray();

                foreach (var soundFile in soundFiles)
                {
                    cmbSound1File.Items.Add(soundFile);
                    cmbSound2File.Items.Add(soundFile);
                }

                SelectSoundComboDefault(cmbSound1File, PathConstants.SteamClientUiAchievementSourceWav);
                SelectSoundComboDefault(cmbSound2File, PathConstants.SteamClientUiFriendSourceWav);
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to load sound list: {ex.Message}");
            }
            finally
            {
                _loadingSoundList = false;
            }
        }

        private static void SelectSoundComboDefault(ComboBox cmb, string preferredFileName)
        {
            if (cmb.SelectedIndex != -1)
                return;
            int idx = cmb.FindStringExact(preferredFileName);
            cmb.SelectedIndex = idx >= 0 ? idx : (cmb.Items.Count > 0 ? 0 : -1);
        }

        private void OnSound1Browse_Click(object sender, EventArgs e)
        {
            BrowseAndCopySound(cmbSound1File, PathConstants.SteamClientUiAchievementNotificationWav);
        }

        private void OnSound2Browse_Click(object sender, EventArgs e)
        {
            BrowseAndCopySound(cmbSound2File, PathConstants.SteamClientUiFriendNotificationWav);
        }

        private void BrowseAndCopySound(ComboBox soundComboBox, string targetFileName)
        {
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Sound Files (*.wav;*.mp3;*.ogg)|*.wav;*.mp3;*.ogg|All Files (*.*)|*.*";
                openFileDialog.Title = "Select Sound File";
                openFileDialog.FilterIndex = 1;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var soundsPath = PathConstants.GlobalSoundsPath;
                        Directory.CreateDirectory(soundsPath);

                        var sourceFileName = Path.GetFileName(openFileDialog.FileName);
                        var sourcePath = openFileDialog.FileName;

                        File.Copy(sourcePath, Path.Combine(soundsPath, sourceFileName), true);

                        var targetPath = Path.Combine(soundsPath, targetFileName);
                        if (sourceFileName != targetFileName)
                            File.Copy(sourcePath, targetPath, true);

                        LoadSoundList();

                        int soundIndex = soundComboBox.FindStringExact(sourceFileName);
                        if (soundIndex >= 0)
                            soundComboBox.SelectedIndex = soundIndex;
                    }
                    catch (Exception ex)
                    {
                        FormMessageBoxHelper.ShowIfAlive(this, $"Failed to copy sound: {ex.Message}", "Error", 
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void OnSound1Default_Click(object sender, EventArgs e)
        {
            SetDefaultSound(cmbSound1File, PathConstants.SteamClientUiAchievementSourceWav, PathConstants.SteamClientUiAchievementNotificationWav);
        }

        private void OnSound2Default_Click(object sender, EventArgs e)
        {
            SetDefaultSound(cmbSound2File, PathConstants.SteamClientUiFriendSourceWav, PathConstants.SteamClientUiFriendNotificationWav);
        }

        private void SetDefaultSound(ComboBox soundComboBox, string libraryFileName, string overlayFileName)
        {
            int libraryIndex = soundComboBox.FindStringExact(libraryFileName);
            if (libraryIndex >= 0)
            {
                soundComboBox.SelectedIndex = libraryIndex;
                return;
            }

            var soundsPath = PathConstants.GlobalSoundsPath;
            var overlayPath = Path.Combine(soundsPath, overlayFileName);
            if (File.Exists(overlayPath))
            {
                FormMessageBoxHelper.ShowIfAlive(this,
                    $"Default library sound '{libraryFileName}' is not in the list. The emulator overlay file is already present.",
                    "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            FormMessageBoxHelper.ShowIfAlive(this, $"Default sound file '{libraryFileName}' not found in the sounds folder.", "File Not Found",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void OnSound1PlayStop_Click(object sender, EventArgs e)
        {
            ToggleSound1Preview();
        }

        private void OnSound2PlayStop_Click(object sender, EventArgs e)
        {
            ToggleSound2Preview();
        }

        private void ToggleSound1Preview()
        {
            lock (_sound1PreviewSync)
            {
                if (IsSound1PreviewPlaying())
                {
                    CancelSound1PreviewLocked();
                    return;
                }
            }

            if (!TryResolveSoundPreviewPath(cmbSound1File, PathConstants.SteamClientUiAchievementNotificationWav, out string soundPath, out _))
                return;

            CancelSound2Preview();
            BeginSound1Preview(soundPath);
        }

        private void ToggleSound2Preview()
        {
            lock (_sound2PreviewSync)
            {
                if (IsSound2PreviewPlaying())
                {
                    CancelSound2PreviewLocked();
                    return;
                }
            }

            if (!TryResolveSoundPreviewPath(cmbSound2File, PathConstants.SteamClientUiFriendNotificationWav, out string soundPath, out _))
                return;

            CancelSound1Preview();
            BeginSound2Preview(soundPath);
        }

        private bool IsSound1PreviewPlaying()
        {
            return _sound1PreviewActive || btnSound1PlayStop.Text == SoundPreviewStopSymbol;
        }

        private bool IsSound2PreviewPlaying()
        {
            return _sound2PreviewActive || btnSound2PlayStop.Text == SoundPreviewStopSymbol;
        }

        private void BeginSound1Preview(string soundPath)
        {
            lock (_sound1PreviewSync)
            {
                _sound1PreviewSessionId++;
                int sessionId = _sound1PreviewSessionId;
                _sound1PreviewActive = true;
                SetSoundPreviewPlayStopButton(btnSound1PlayStop, playing: true);

                if (!WavPreviewPlaybackHelper.TryPlay(sessionId, soundPath, this, OnSound1PreviewPlaybackEnded))
                {
                    _sound1PreviewActive = false;
                    SetSoundPreviewPlayStopButton(btnSound1PlayStop, playing: false);
                }
            }
        }

        private void BeginSound2Preview(string soundPath)
        {
            lock (_sound2PreviewSync)
            {
                _sound2PreviewSessionId++;
                int sessionId = _sound2PreviewSessionId;
                _sound2PreviewActive = true;
                SetSoundPreviewPlayStopButton(btnSound2PlayStop, playing: true);

                if (!WavPreviewPlaybackHelper.TryPlay(sessionId, soundPath, this, OnSound2PreviewPlaybackEnded))
                {
                    _sound2PreviewActive = false;
                    SetSoundPreviewPlayStopButton(btnSound2PlayStop, playing: false);
                }
            }
        }

        private void OnSound1PreviewPlaybackEnded(int sessionId)
        {
            lock (_sound1PreviewSync)
            {
                if (sessionId != _sound1PreviewSessionId || !_sound1PreviewActive)
                    return;
                _sound1PreviewActive = false;
                SetSoundPreviewPlayStopButton(btnSound1PlayStop, playing: false);
            }
        }

        private void OnSound2PreviewPlaybackEnded(int sessionId)
        {
            lock (_sound2PreviewSync)
            {
                if (sessionId != _sound2PreviewSessionId || !_sound2PreviewActive)
                    return;
                _sound2PreviewActive = false;
                SetSoundPreviewPlayStopButton(btnSound2PlayStop, playing: false);
            }
        }

        private bool TryResolveSoundPreviewPath(
            ComboBox soundComboBox,
            string defaultFileName,
            out string soundPath,
            out string displayName)
        {
            soundPath = null;
            displayName = soundComboBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(displayName))
                displayName = defaultFileName;

            soundPath = Path.Combine(PathConstants.GlobalSoundsPath, displayName);

            if (!File.Exists(soundPath))
            {
                FormMessageBoxHelper.ShowIfAlive(this, $"Sound file not found: {displayName}", "File Not Found",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!displayName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                FormMessageBoxHelper.ShowIfAlive(this,
                    "Sound preview only supports .wav files. The selected sound format is not supported for preview.",
                    "Format Not Supported", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            return true;
        }

        private static void SetSoundPreviewPlayStopButton(Button playStopButton, bool playing)
        {
            playStopButton.Text = playing ? SoundPreviewStopSymbol : SoundPreviewPlaySymbol;
        }

        private void CancelSound1Preview()
        {
            lock (_sound1PreviewSync)
                CancelSound1PreviewLocked();
        }

        private void CancelSound2Preview()
        {
            lock (_sound2PreviewSync)
                CancelSound2PreviewLocked();
        }

        private void CancelSound1PreviewLocked()
        {
            _sound1PreviewSessionId++;
            _sound1PreviewActive = false;
            SetSoundPreviewPlayStopButton(btnSound1PlayStop, playing: false);
            WavPreviewPlaybackHelper.Stop();
        }

        private void CancelSound2PreviewLocked()
        {
            _sound2PreviewSessionId++;
            _sound2PreviewActive = false;
            SetSoundPreviewPlayStopButton(btnSound2PlayStop, playing: false);
            WavPreviewPlaybackHelper.Stop();
        }

        private void OnOpenFontsFolder_Click(object sender, EventArgs e) =>
            OpenGlobalAssetFolder(PathConstants.GlobalFontsPath, "fonts");

        private void OnOpenControllerFolder_Click(object sender, EventArgs e) =>
            OpenGlobalAssetFolder(PathConstants.GlobalControllerGlyphsPath, "controller glyphs");

        private void cmbSaveLocation_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isLoading)
                return;

            int previousIndex = _previousSaveLocationIndex;
            _previousSaveLocationIndex = cmbSaveLocation.SelectedIndex;

            UpdateSaveLocationControls(previousIndex);
            ApplyLocalSaveWarningForSaveLocationMode();
            CheckForChanges();
        }

        private void ApplyLocalSaveWarningForSaveLocationMode()
        {
            if (chkDisableWarningLocalSave == null)
                return;

            chkDisableWarningLocalSave.Checked = cmbSaveLocation.SelectedIndex == SaveLocationSteamUserdata;
        }

        private void InitializeSaveLocationComboItems()
        {
            cmbSaveLocation.Items.Clear();
            cmbSaveLocation.Items.Add(
                string.Format("Default (%appdata%\\{0}\\)", ApplicationConstants.DefaultSavesFolderName));
            cmbSaveLocation.Items.Add("Portable");
            cmbSaveLocation.Items.Add("Steam userdata (Steam client)");
            cmbSaveLocation.Items.Add("Custom Path");
        }

        private void InitializeSaveLocationComboBox(string localSavePath, string accountSteamId)
        {
            if (GoldbergSavePathHelper.UsesSteamUserdataLayout(localSavePath, accountSteamId))
                cmbSaveLocation.SelectedIndex = SaveLocationSteamUserdata;
            else if (string.IsNullOrEmpty(localSavePath))
                cmbSaveLocation.SelectedIndex = SaveLocationDefault;
            else if (IsPortablePath(localSavePath))
                cmbSaveLocation.SelectedIndex = SaveLocationPortable;
            else
            {
                cmbSaveLocation.SelectedIndex = SaveLocationCustom;
                txtLocalSavePath.Text = localSavePath;
            }

            UpdateSaveLocationControls(-1);
        }

        private string GetCustomBasePathFromIni(string localSavePath, string accountSteamId)
        {
            if (string.IsNullOrEmpty(localSavePath))
                return string.Empty;
            string trimmed = localSavePath.Trim();
            if (IsPortablePath(trimmed) || GoldbergSavePathHelper.UsesSteamUserdataLayout(trimmed, accountSteamId))
                return string.Empty;
            return trimmed;
        }

        private void RefreshPersistedSaveLocationFromIni(UserSettings userSettings)
        {
            if (userSettings == null)
                return;
            _persistedCustomLocalSavePath = GetCustomBasePathFromIni(userSettings.LocalSavePath, userSettings.AccountSteamId) ?? string.Empty;
            _persistedSavesFolderName = string.IsNullOrWhiteSpace(userSettings.SavesFolderName)
                ? ApplicationConstants.DefaultSavesFolderName
                : userSettings.SavesFolderName.Trim();
        }

        private void SyncPersistedSaveLocationFromInitialState()
        {
            _persistedCustomLocalSavePath = GetCustomBasePathFromIni(_initialUserSettings.LocalSavePath, _initialUserSettings.AccountSteamId) ?? string.Empty;
            _persistedSavesFolderName = string.IsNullOrWhiteSpace(_initialUserSettings.SavesFolderName)
                ? ApplicationConstants.DefaultSavesFolderName
                : _initialUserSettings.SavesFolderName.Trim();
        }

        private bool IsPortablePath(string path) => GoldbergSavePathHelper.IsPortableLocalSavePath(path);

        private string GetEffectiveSavesFolderNameForSavePath()
        {
            string name = txtSavesFolderName != null ? txtSavesFolderName.Text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(name))
                name = ApplicationConstants.DefaultSavesFolderName;
            return name.Replace('/', '\\').Trim('\\');
        }

        private void RefreshSavePathDisplayText()
        {
            int idx = cmbSaveLocation.SelectedIndex;
            if (idx == SaveLocationDefault)
            {
                string folder = GetEffectiveSavesFolderNameForSavePath();
                txtLocalSavePath.Text = "%appdata%\\" + folder + "\\";
            }
            else if (idx == SaveLocationPortable)
            {
                string folder = GetEffectiveSavesFolderNameForSavePath();
                txtLocalSavePath.Text = ".\\" + folder + "\\";
            }
            else if (idx == SaveLocationSteamUserdata)
                txtLocalSavePath.Text = GetSteamUserdataPathDisplayText();
        }

        private string GetSteamUserdataPathDisplayText()
        {
            string steamId = txtSteamID != null ? txtSteamID.Text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(steamId))
                return "Set a Steam64 ID on the User tab first.";

            string display = GoldbergSavePathHelper.FormatSteamUserdataDisplayPath(steamId);
            return string.IsNullOrEmpty(display)
                ? "Steam64 ID is invalid. Use a valid Steam64 account ID on the User tab."
                : display;
        }

        private void UpdateSaveLocationControls(int previousSaveLocationIndex = -1)
        {
            bool isCustom = cmbSaveLocation.SelectedIndex == SaveLocationCustom;
            bool isDefault = cmbSaveLocation.SelectedIndex == SaveLocationDefault;
            bool isSteamUserdata = cmbSaveLocation.SelectedIndex == SaveLocationSteamUserdata;
            txtLocalSavePath.ReadOnly = !isCustom;
            txtLocalSavePath.Enabled = true;
            btnBrowseSavePath.Enabled = isCustom;
            btnOpenSaveFolder.Enabled = true;
            lblSavesFolderName.Enabled = !isDefault && !isSteamUserdata;
            txtSavesFolderName.Enabled = !isDefault && !isSteamUserdata;
            lblLocalSavePath.Text = isSteamUserdata ? "Steam Path:" : "Save Path:";

            if (isCustom)
            {
                txtLocalSavePath.Text = _persistedCustomLocalSavePath ?? string.Empty;
                if (previousSaveLocationIndex == SaveLocationDefault)
                    txtSavesFolderName.Text = _persistedSavesFolderName;
            }
            else
                RefreshSavePathDisplayText();
        }

        private void OnSteamWebApiKey_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string url = ApplicationConstants.SteamWebApiKeyRegistrationUrl;

            if (!PathValidationHelper.IsSafeUrl(url))
            {
                FormMessageBoxHelper.ShowIfAlive(this, "Invalid URL format detected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            try
            {
                System.Diagnostics.Process.Start(url);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Failed to open Steam API key page: {ex.Message}", ex);
                FormMessageBoxHelper.ShowIfAlive(this, "Failed to open Steam API key registration page.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnRemoveApiKey_Click(object sender, EventArgs e)
        {
            if (FormMessageBoxHelper.ShowDialogIfAlive(this,
                    "Are you sure you want to remove the Steam Web API key?",
                    "Confirm Removal",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            txtSteamWebApiKey.Text = string.Empty;
            ClearApiKeyValidationMessage();

            var result = _apiKeyService.RemoveApiKey();

            if (result.IsValid)
            {
                FormMessageBoxHelper.ShowIfAlive(this, "Steam Web API key has been removed.", "API Key Removed", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                FormMessageBoxHelper.ShowIfAlive(this, $"Failed to remove API key: {result.ErrorMessage}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void tabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
        }

        private void OnElement_Click(object sender, EventArgs e)
        {
        }

        private void LoadAvatarImage()
        {
            LoadAvatarPreview(_avatarPath);
        }

        private void LoadAvatarPreview(string imagePath)
        {
            try
            {
                if (picAvatar.Image != null && picAvatar.Image != Resources.gold_steam_128_logo)
                    picAvatar.Image.Dispose();

                if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                {
                    using (var fileStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
                        picAvatar.Image = Image.FromStream(fileStream);
                }
                else
                    picAvatar.Image = Resources.gold_steam_128_logo;
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to load avatar image: {ex.Message}");
                picAvatar.Image = Resources.gold_steam_128_logo;
            }
        }

        private void OnSetAvatar_Click(object sender, EventArgs e)
        {
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Image Files (*.jpg;*.jpeg;*.png;*.bmp;*.gif)|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All Files (*.*)|*.*";
                openFileDialog.Title = "Select Avatar Image";
                openFileDialog.FilterIndex = 1;

                if (openFileDialog.ShowDialog() != DialogResult.OK)
                    return;

                try
                {
                    _pendingAvatarChange = PendingAvatarChange.ReplaceFromFile;
                    _pendingAvatarSourcePath = openFileDialog.FileName;
                    LoadAvatarPreview(_pendingAvatarSourcePath);
                    CheckForChanges();
                }
                catch (Exception ex)
                {
                    FormMessageBoxHelper.ShowIfAlive(this, $"Failed to preview avatar: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OnClearAvatar_Click(object sender, EventArgs e)
        {
            _pendingAvatarChange = PendingAvatarChange.ResetToDefault;
            _pendingAvatarSourcePath = null;
            LoadAvatarPreview(null);
            CheckForChanges();
        }

        private async Task<ValidationResult> ApplyPendingAvatarAsync()
        {
            switch (_pendingAvatarChange)
            {
                case PendingAvatarChange.ReplaceFromFile:
                    return _appDataService.SaveGlobalAccountAvatarFromFile(_pendingAvatarSourcePath);
                case PendingAvatarChange.ResetToDefault:
                    return await _appDataService.ResetGlobalAccountAvatarAsync().ConfigureAwait(true);
                default:
                    return ValidationResult.Success();
            }
        }

        private void DiscardPendingAvatarChanges()
        {
            if (_pendingAvatarChange == PendingAvatarChange.None)
                return;

            _pendingAvatarChange = PendingAvatarChange.None;
            _pendingAvatarSourcePath = null;
            LoadAvatarImage();
        }

        private void ApplyTheme()
        {
            if (_themeService != null)
            {
                _themeService.ApplyTheme(this);
                ConfigureSoundPreviewPlayStopButtons();
                RefreshApiKeyValidationColor();
            }
        }

        private void ConfigureSoundPreviewPlayStopButtons()
        {
            ConfigureSoundPreviewPlayStopButton(btnSound1PlayStop);
            ConfigureSoundPreviewPlayStopButton(btnSound2PlayStop);
        }

        private void ConfigureSoundPreviewPlayStopButton(Button playStopButton)
        {
            if (playStopButton == null)
                return;

            playStopButton.Font = new Font("Segoe UI Symbol", 9F, FontStyle.Regular, GraphicsUnit.Point);
            playStopButton.Padding = Padding.Empty;
            playStopButton.TextAlign = ContentAlignment.MiddleCenter;
            playStopButton.UseCompatibleTextRendering = false;
            playStopButton.Paint -= SoundPreviewPlayStopButton_Paint;
            playStopButton.Paint += SoundPreviewPlayStopButton_Paint;
            playStopButton.Invalidate();
        }

        private void SoundPreviewPlayStopButton_Paint(object sender, PaintEventArgs e)
        {
            var button = (Button)sender;
            ThemeColors colors = _themeService != null
                ? _themeService.GetThemeColors(_themeService.EffectiveTheme)
                : null;

            Color backColor = button.Enabled
                ? (colors?.ControlBackground ?? button.BackColor)
                : (colors?.DisabledBackground ?? button.BackColor);
            Color foreColor = button.Enabled
                ? (colors?.ControlForeground ?? button.ForeColor)
                : (colors?.DisabledForeground ?? button.ForeColor);
            Color borderColor = colors?.Border ?? button.FlatAppearance.BorderColor;

            e.Graphics.Clear(backColor);
            var borderRect = new Rectangle(0, 0, button.Width - 1, button.Height - 1);
            ControlPaint.DrawBorder(e.Graphics, borderRect, borderColor, ButtonBorderStyle.Solid);

            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
            TextRenderer.DrawText(e.Graphics, button.Text, button.Font, button.ClientRectangle, foreColor, backColor, flags);
        }

        private void RefreshApiKeyValidationColor()
        {
            if (lblApiKeyValidation == null || _themeService == null || _apiKeyService == null) return;
            var status = _apiKeyService.GetStatus();
            var colors = _themeService.GetThemeColors(_themeService.EffectiveTheme);
            if (!status.HasKey)
                lblApiKeyValidation.ForeColor = colors.Foreground;
            else if (!status.HasValidFormat || !status.IsValid)
                lblApiKeyValidation.ForeColor = colors.ErrorColor;
            else
                lblApiKeyValidation.ForeColor = colors.SuccessColor;
        }

        private void ThemeService_ThemeChanged(object sender, ThemeChangedEventArgs e)
        {
            if (IsDisposed || Disposing)
                return;
            if (InvokeRequired)
            {
                Invoke(new Action(ApplyTheme));
            }
            else
            {
                ApplyTheme();
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            CancelSound1Preview();
            CancelSound2Preview();

            if (grpSteamWebApi != null)
                grpSteamWebApi.Layout -= GrpSteamWebApi_Layout;

            if (_apiKeyValidationClearTimer != null)
            {
                _apiKeyValidationClearTimer.Stop();
                _apiKeyValidationClearTimer.Tick -= OnApiKeyValidationClearTimer_Tick;
                _apiKeyValidationClearTimer.Dispose();
            }

            if (_themeService != null)
            {
                _themeService.ThemeChanged -= ThemeService_ThemeChanged;
            }
            base.OnFormClosed(e);
        }

        private void txtSteamWebApiKey_TextChanged(object sender, EventArgs e)
        {
            Control_Changed(sender, e);

            if (_isLoading)
                return;

            var rawText = txtSteamWebApiKey.Text;
            var apiKey = rawText.Trim();

            var colors = _themeService?.GetThemeColors(_themeService.EffectiveTheme);
            var fg = colors?.Foreground ?? SystemColors.ControlText;
            var warn = colors?.WarningColor ?? Color.Orange;

            if (string.IsNullOrEmpty(apiKey))
            {
                ClearApiKeyValidationMessage();
                return;
            }

            if (rawText.Contains(" "))
            {
                SetApiKeyValidationMessage("API key cannot contain spaces.", warn);
                return;
            }

            if (apiKey.Any(c => !((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))))
            {
                SetApiKeyValidationMessage("API key must contain only uppercase letters (A-Z) and numbers (0-9).", warn);
                return;
            }

            if (!_apiKeyService.IsValidFormat(apiKey))
            {
                ClearApiKeyValidationMessage();
                return;
            }

            ValidateApiKeyAsync(apiKey);
        }

        private async void ValidateApiKeyAsync(string apiKey)
        {
            var colors = _themeService?.GetThemeColors(_themeService.EffectiveTheme);
            var fg = colors?.Foreground ?? SystemColors.ControlText;
            var err = colors?.ErrorColor ?? Color.Red;
            var ok = colors?.SuccessColor ?? Color.Green;

            try
            {
                _isApiKeyValidationInProgress = true;
                SetApiKeyValidationMessage("Validating API key...", fg);

                var validationResult = await Task.Run(() => _apiKeyService.ValidateKey(apiKey));

                if (IsDisposed || Disposing)
                    return;

                if (txtSteamWebApiKey.Text.Trim() != apiKey)
                    return;

                _lastValidatedApiKey = apiKey;
                _lastApiKeyValidationSucceeded = validationResult.IsValid;

                if (validationResult.IsValid)
                    SetApiKeyValidationMessage("API key is valid.", ok);
                else
                    SetApiKeyValidationMessage($"Validation failed: {validationResult.ErrorMessage}", err);
            }
            catch (Exception ex)
            {
                if (IsDisposed || Disposing)
                    return;

                if (txtSteamWebApiKey.Text.Trim() != apiKey)
                    return;

                Program.LogService?.LogError($"Steam API key validation failed: {ex.Message}", ex);
                _lastValidatedApiKey = apiKey;
                _lastApiKeyValidationSucceeded = false;
                SetApiKeyValidationMessage($"Validation error: {ex.Message}", err);
            }
            finally
            {
                _isApiKeyValidationInProgress = false;
            }
        }

        private void WireUpChangeEvents()
        {
            txtSteamWebApiKey.TextChanged += txtSteamWebApiKey_TextChanged;

            txtUsername.TextChanged += Control_Changed;
            txtSteamID.TextChanged += TxtSteamID_TextChanged;
            cmbLanguage.SelectedIndexChanged += Control_Changed;
            cmbCountry.SelectedIndexChanged += Control_Changed;
            txtLocalSavePath.TextChanged += Control_Changed;

            chkEnableExperimentalOverlay.CheckedChanged += Control_Changed;
            numHookDelay.ValueChanged += Control_Changed;
            numRendererDetectorTimeout.ValueChanged += Control_Changed;
            chkDisableWarningAny.CheckedChanged += Control_Changed;
            chkDisableWarningBadAppId.CheckedChanged += Control_Changed;

            cmbFontOverride.SelectedIndexChanged += Control_Changed;
            numFontSize.ValueChanged += Control_Changed;
            numFontSpacingX.ValueChanged += Control_Changed;
            numFontSpacingY.ValueChanged += Control_Changed;
            btnColorBackground.BackColorChanged += Control_Changed;
            btnColorElements.BackColorChanged += Control_Changed;
            btnColorHoveredElements.BackColorChanged += Control_Changed;
            btnColorActiveElements.BackColorChanged += Control_Changed;
            numNotificationRounding.ValueChanged += Control_Changed;
            numNotificationMarginX.ValueChanged += Control_Changed;
            numNotificationMarginY.ValueChanged += Control_Changed;

            btnColorNotification.BackColorChanged += Control_Changed;
            numNotificationAnimation.ValueChanged += Control_Changed;
            numNotificationDurationInvitation.ValueChanged += Control_Changed;
            numNotificationDurationChat.ValueChanged += Control_Changed;
            chkDisableFriendNotification.CheckedChanged += Control_Changed;
            numNotificationDurationAchievement.ValueChanged += Control_Changed;
            numNotificationDurationProgress.ValueChanged += Control_Changed;
            cmbAchievementDateTimeFormat.SelectedIndexChanged += Control_Changed;
            cmbPosAchievement.SelectedIndexChanged += Control_Changed;
            numIconSize.ValueChanged += Control_Changed;
            chkUploadAchievementsToGPU.CheckedChanged += Control_Changed;
            chkDisableAchievementNotification.CheckedChanged += Control_Changed;
            chkDisableAchievementProgress.CheckedChanged += Control_Changed;
            cmbPosInvitation.SelectedIndexChanged += Control_Changed;
            cmbPosChatMsg.SelectedIndexChanged += Control_Changed;

            numFpsAveragingWindow.ValueChanged += Control_Changed;
            chkAlwaysShowUserInfo.CheckedChanged += Control_Changed;
            chkAlwaysShowFPS.CheckedChanged += Control_Changed;
            chkAlwaysShowFrametime.CheckedChanged += Control_Changed;
            chkAlwaysShowPlaytime.CheckedChanged += Control_Changed;
            numStatsPosX.ValueChanged += Control_Changed;
            numStatsPosY.ValueChanged += Control_Changed;
            btnColorStatsBackground.BackColorChanged += Control_Changed;
            btnColorStatsText.BackColorChanged += Control_Changed;

            chkDisableWarningLocalSave.CheckedChanged += Control_Changed;
            txtSavesFolderName.TextChanged += TxtSavesFolderName_TextChanged;

        }

        private void TxtSavesFolderName_TextChanged(object sender, EventArgs e)
        {
            if (!_isLoading && cmbSaveLocation.SelectedIndex == SaveLocationPortable)
                RefreshSavePathDisplayText();
            Control_Changed(sender, e);
        }

        private void TxtSteamID_TextChanged(object sender, EventArgs e)
        {
            if (!_isLoading)
            {
                if (cmbSaveLocation != null && cmbSaveLocation.SelectedIndex == SaveLocationSteamUserdata)
                    RefreshSavePathDisplayText();
                CheckForChanges();
            }
        }

        private void Control_Changed(object sender, EventArgs e)
        {
            if (!_isLoading)
                CheckForChanges();
        }

        private void CheckForChanges()
        {
            btnSave.Enabled = HasChanges() && IsSaveManagementValidForSave();
        }

        private bool IsSaveManagementValidForSave()
        {
            if (cmbSaveLocation.SelectedIndex == SaveLocationCustom)
            {
                return GoldbergSavePathHelper.ValidateCustomLocalSavePath(
                    txtLocalSavePath.Text,
                    txtSteamID != null ? txtSteamID.Text.Trim() : string.Empty).IsValid;
            }

            return true;
        }

        private bool HasChanges()
        {
            if (txtSteamWebApiKey.Text != _initialApiKey)
                return true;

            if (_pendingAvatarChange != PendingAvatarChange.None)
                return true;

            string currentLocalSavePath = GetPersistedLocalSavePathFromUi();
            string normalizedCurrent = GoldbergSavePathHelper.NormalizePersistedLocalSavePath(
                currentLocalSavePath, txtSteamID != null ? txtSteamID.Text.Trim() : string.Empty);
            string normalizedInitial = GoldbergSavePathHelper.NormalizePersistedLocalSavePath(
                _initialUserSettings.LocalSavePath, _initialUserSettings.AccountSteamId);

            if (txtUsername.Text != _initialUserSettings.AccountName ||
                txtSteamID.Text != _initialUserSettings.AccountSteamId ||
                SteamLanguageDisplayHelper.ToLanguageCode(cmbLanguage.Text) != _initialUserSettings.Language ||
                cmbCountry.Text != _initialUserSettings.IpCountry ||
                !string.Equals(normalizedCurrent, normalizedInitial, StringComparison.OrdinalIgnoreCase) ||
                txtSavesFolderName.Text.Trim() != _initialUserSettings.SavesFolderName)
                return true;

            if (!AreOverlaySettingsEqual(BuildOverlaySettingsFromControls(), _initialOverlaySettings))
                return true;

            if (_initialMainSettings != null &&
                !AreEmulatorSettingsEqual(BuildEmulatorSettings(), _initialMainSettings))
                return true;

            return false;
        }

        private bool AreOverlaySettingsEqual(Models.OverlaySettings a, Models.OverlaySettings b)
        {
            return a.EnableExperimentalOverlay == b.EnableExperimentalOverlay &&
                   a.HookDelaySec == b.HookDelaySec &&
                   a.RendererDetectorTimeoutSec == b.RendererDetectorTimeoutSec &&
                   a.DisableAchievementNotification == b.DisableAchievementNotification &&
                   a.DisableFriendNotification == b.DisableFriendNotification &&
                   a.DisableAchievementProgress == b.DisableAchievementProgress &&
                   a.DisableWarningAny == b.DisableWarningAny &&
                   a.DisableWarningBadAppId == b.DisableWarningBadAppId &&
                   a.DisableWarningLocalSave == b.DisableWarningLocalSave &&
                   a.UploadAchievementsIconsToGpu == b.UploadAchievementsIconsToGpu &&
                   a.FpsAveragingWindow == b.FpsAveragingWindow &&
                   a.OverlayAlwaysShowUserInfo == b.OverlayAlwaysShowUserInfo &&
                   a.OverlayAlwaysShowFps == b.OverlayAlwaysShowFps &&
                   a.OverlayAlwaysShowFrametime == b.OverlayAlwaysShowFrametime &&
                   a.OverlayAlwaysShowPlaytime == b.OverlayAlwaysShowPlaytime &&
                   a.FontOverride == b.FontOverride &&
                   Math.Abs(a.FontSize - b.FontSize) < 0.001f &&
                   Math.Abs(a.IconSize - b.IconSize) < 0.001f &&
                   Math.Abs(a.FontGlyphExtraSpacingX - b.FontGlyphExtraSpacingX) < 0.001f &&
                   Math.Abs(a.FontGlyphExtraSpacingY - b.FontGlyphExtraSpacingY) < 0.001f &&
                   Math.Abs(a.NotificationR - b.NotificationR) < 0.001f &&
                   Math.Abs(a.NotificationG - b.NotificationG) < 0.001f &&
                   Math.Abs(a.NotificationB - b.NotificationB) < 0.001f &&
                   Math.Abs(a.NotificationA - b.NotificationA) < 0.001f &&
                   Math.Abs(a.NotificationRounding - b.NotificationRounding) < 0.001f &&
                   Math.Abs(a.NotificationMarginX - b.NotificationMarginX) < 0.001f &&
                   Math.Abs(a.NotificationMarginY - b.NotificationMarginY) < 0.001f &&
                   Math.Abs(a.BackgroundR - b.BackgroundR) < 0.001f &&
                   Math.Abs(a.BackgroundG - b.BackgroundG) < 0.001f &&
                   Math.Abs(a.BackgroundB - b.BackgroundB) < 0.001f &&
                   Math.Abs(a.BackgroundA - b.BackgroundA) < 0.001f &&
                   Math.Abs(a.ElementR - b.ElementR) < 0.001f &&
                   Math.Abs(a.ElementG - b.ElementG) < 0.001f &&
                   Math.Abs(a.ElementB - b.ElementB) < 0.001f &&
                   Math.Abs(a.ElementA - b.ElementA) < 0.001f &&
                   Math.Abs(a.ElementHoveredR - b.ElementHoveredR) < 0.001f &&
                   Math.Abs(a.ElementHoveredG - b.ElementHoveredG) < 0.001f &&
                   Math.Abs(a.ElementHoveredB - b.ElementHoveredB) < 0.001f &&
                   Math.Abs(a.ElementHoveredA - b.ElementHoveredA) < 0.001f &&
                   Math.Abs(a.ElementActiveR - b.ElementActiveR) < 0.001f &&
                   Math.Abs(a.ElementActiveG - b.ElementActiveG) < 0.001f &&
                   Math.Abs(a.ElementActiveB - b.ElementActiveB) < 0.001f &&
                   Math.Abs(a.ElementActiveA - b.ElementActiveA) < 0.001f &&
                   Math.Abs(a.NotificationAnimation - b.NotificationAnimation) < 0.001f &&
                   Math.Abs(a.NotificationDurationProgress - b.NotificationDurationProgress) < 0.001f &&
                   Math.Abs(a.NotificationDurationAchievement - b.NotificationDurationAchievement) < 0.001f &&
                   Math.Abs(a.NotificationDurationInvitation - b.NotificationDurationInvitation) < 0.001f &&
                   Math.Abs(a.NotificationDurationChat - b.NotificationDurationChat) < 0.001f &&
                   a.AchievementUnlockDatetimeFormat == b.AchievementUnlockDatetimeFormat &&
                   a.PosAchievement == b.PosAchievement &&
                   a.PosInvitation == b.PosInvitation &&
                   a.PosChatMsg == b.PosChatMsg &&
                   Math.Abs(a.StatsPosX - b.StatsPosX) < 0.001f &&
                   Math.Abs(a.StatsPosY - b.StatsPosY) < 0.001f &&
                   Math.Abs(a.StatsBackgroundR - b.StatsBackgroundR) < 0.001f &&
                   Math.Abs(a.StatsBackgroundG - b.StatsBackgroundG) < 0.001f &&
                   Math.Abs(a.StatsBackgroundB - b.StatsBackgroundB) < 0.001f &&
                   Math.Abs(a.StatsBackgroundA - b.StatsBackgroundA) < 0.001f &&
                   Math.Abs(a.StatsTextR - b.StatsTextR) < 0.001f &&
                   Math.Abs(a.StatsTextG - b.StatsTextG) < 0.001f &&
                   Math.Abs(a.StatsTextB - b.StatsTextB) < 0.001f &&
                   Math.Abs(a.StatsTextA - b.StatsTextA) < 0.001f;
        }

        private static void SetNudIntIfDiff(NumericUpDown n, int value, int def)
        {
            if (value != def)
                AssignNudValue(n, value);
        }

        private static void SetNudFloatIfDiff(NumericUpDown n, float value, float def)
        {
            if (Math.Abs(value - def) >= 0.001f)
                AssignNudValue(n, (decimal)value);
        }

        // configs.ini may contain 0 or other out-of-range values; clamp to the control limits.
        private static void AssignNudValue(NumericUpDown n, decimal value)
        {
            if (value < n.Minimum)
                value = n.Minimum;
            else if (value > n.Maximum)
                value = n.Maximum;
            n.Value = value;
        }

        private static void SetChkIfDiff(CheckBox c, bool value, bool def)
        {
            if (value != def)
                c.Checked = value;
        }

        private static void SelectCmbOptional(ComboBox cmb, string value, string def)
        {
            if (string.IsNullOrEmpty(value) || string.Equals(value, def, StringComparison.Ordinal))
                return;
            int i = cmb.FindStringExact(value);
            if (i >= 0)
                cmb.SelectedIndex = i;
        }

        private void SelectCmbAchievementPos(string value, string def)
        {
            string v = string.IsNullOrEmpty(value) ? def : value;
            int i = cmbPosAchievement.FindStringExact(v);
            cmbPosAchievement.SelectedIndex = i >= 0 ? i : cmbPosAchievement.FindStringExact(def);
        }

        private void LoadLanguageList()
        {
            try
            {
                cmbLanguage.Items.Clear();
                foreach (var language in SteamLanguageDisplayHelper.SupportedLanguages)
                    cmbLanguage.Items.Add(language);
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to load language list: {ex.Message}");
            }
        }

        private void LoadGoldbergSettings()
        {
            try
            {
                var overlaySettings = _goldbergCfgService.LoadGlobalOverlaySettings();
                var mainSettings = _goldbergCfgService.LoadGlobalMainSettings();
                var userSettings = _goldbergCfgService.LoadGlobalUserSettings();
                LoadEmulatorSettings(mainSettings);
                LoadUserSettings(userSettings);
                LoadOverlayGeneral(overlaySettings);
                LoadOverlayVisual(overlaySettings);
                LoadOverlayNotifications(overlaySettings);
                LoadOverlayMetrics(overlaySettings);
                LoadSaveManagement(overlaySettings, userSettings);
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to load Goldberg settings: {ex.Message}");
                FormMessageBoxHelper.ShowIfAlive(this, $"Failed to load settings: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void LoadUserSettings(UserSettings userSettings)
        {
            txtUsername.Text = userSettings.AccountName;
            txtSteamID.Text = userSettings.AccountSteamId;
            if (!string.IsNullOrEmpty(userSettings.Language))
            {
                string nativeName = SteamLanguageDisplayHelper.ToDisplayName(userSettings.Language);
                int langIndex = cmbLanguage.FindStringExact(nativeName);
                if (langIndex >= 0)
                    cmbLanguage.SelectedIndex = langIndex;
            }
            if (!string.IsNullOrEmpty(userSettings.IpCountry))
            {
                int countryIndex = cmbCountry.FindStringExact(userSettings.IpCountry);
                if (countryIndex >= 0)
                    cmbCountry.SelectedIndex = countryIndex;
            }
        }

        private void LoadOverlayGeneral(OverlaySettings o)
        {
            var d = _overlayDefaults;
            chkEnableExperimentalOverlay.Checked = o.EnableExperimentalOverlay;
            SetNudIntIfDiff(numHookDelay, o.HookDelaySec, d.HookDelaySec);
            SetNudIntIfDiff(numRendererDetectorTimeout, o.RendererDetectorTimeoutSec, d.RendererDetectorTimeoutSec);
            SetChkIfDiff(chkDisableWarningAny, o.DisableWarningAny, d.DisableWarningAny);
            SetChkIfDiff(chkDisableWarningBadAppId, o.DisableWarningBadAppId, d.DisableWarningBadAppId);
        }

        private void LoadOverlayVisual(OverlaySettings o)
        {
            var d = _overlayDefaults;
            if (!string.IsNullOrEmpty(o.FontOverride))
            {
                int fontIndex = cmbFontOverride.FindStringExact(o.FontOverride);
                if (fontIndex >= 0)
                    cmbFontOverride.SelectedIndex = fontIndex;
            }
            EnsureFontComboSelection();
            SetNudFloatIfDiff(numFontSize, o.FontSize, d.FontSize);
            SetNudFloatIfDiff(numFontSpacingX, o.FontGlyphExtraSpacingX, d.FontGlyphExtraSpacingX);
            SetNudFloatIfDiff(numFontSpacingY, o.FontGlyphExtraSpacingY, d.FontGlyphExtraSpacingY);
            btnColorBackground.BackColor = OverlayColorHelper.RgbaToColor(o.BackgroundR, o.BackgroundG, o.BackgroundB, o.BackgroundA);
            btnColorElements.BackColor = OverlayColorHelper.RgbaToColor(o.ElementR, o.ElementG, o.ElementB, o.ElementA);
            btnColorHoveredElements.BackColor = OverlayColorHelper.RgbaToColor(o.ElementHoveredR, o.ElementHoveredG, o.ElementHoveredB, o.ElementHoveredA);
            if (o.ElementActiveR >= 0 && o.ElementActiveG >= 0 && o.ElementActiveB >= 0)
                btnColorActiveElements.BackColor = OverlayColorHelper.RgbaToColor(o.ElementActiveR, o.ElementActiveG, o.ElementActiveB, o.ElementActiveA);
            else
                btnColorActiveElements.BackColor = Color.Gray;
            SetNudFloatIfDiff(numNotificationRounding, o.NotificationRounding, d.NotificationRounding);
            SetNudFloatIfDiff(numNotificationMarginX, o.NotificationMarginX, d.NotificationMarginX);
            SetNudFloatIfDiff(numNotificationMarginY, o.NotificationMarginY, d.NotificationMarginY);
        }

        private void EnsureFontComboSelection()
        {
            if (cmbFontOverride.SelectedIndex != -1)
                return;
            int i = cmbFontOverride.FindStringExact("Default");
            cmbFontOverride.SelectedIndex = i >= 0 ? i : (cmbFontOverride.Items.Count > 0 ? 0 : -1);
        }

        private void LoadOverlayNotifications(OverlaySettings o)
        {
            var d = _overlayDefaults;
            btnColorNotification.BackColor = OverlayColorHelper.RgbaToColor(o.NotificationR, o.NotificationG, o.NotificationB, o.NotificationA);
            SetNudFloatIfDiff(numNotificationAnimation, o.NotificationAnimation, d.NotificationAnimation);
            SetNudFloatIfDiff(numNotificationDurationInvitation, o.NotificationDurationInvitation, d.NotificationDurationInvitation);
            SetNudFloatIfDiff(numNotificationDurationChat, o.NotificationDurationChat, d.NotificationDurationChat);
            SetChkIfDiff(chkDisableFriendNotification, o.DisableFriendNotification, d.DisableFriendNotification);
            SetNudFloatIfDiff(numNotificationDurationAchievement, o.NotificationDurationAchievement, d.NotificationDurationAchievement);
            SetNudFloatIfDiff(numNotificationDurationProgress, o.NotificationDurationProgress, d.NotificationDurationProgress);
            if (o.AchievementUnlockDatetimeFormat != d.AchievementUnlockDatetimeFormat)
                cmbAchievementDateTimeFormat.Text = o.AchievementUnlockDatetimeFormat;
            SelectCmbAchievementPos(o.PosAchievement, d.PosAchievement);
            SetNudFloatIfDiff(numIconSize, o.IconSize, d.IconSize);
            SetChkIfDiff(chkUploadAchievementsToGPU, o.UploadAchievementsIconsToGpu, d.UploadAchievementsIconsToGpu);
            SetChkIfDiff(chkDisableAchievementNotification, o.DisableAchievementNotification, d.DisableAchievementNotification);
            SetChkIfDiff(chkDisableAchievementProgress, o.DisableAchievementProgress, d.DisableAchievementProgress);
            SelectCmbOptional(cmbPosInvitation, o.PosInvitation, d.PosInvitation);
            SelectCmbOptional(cmbPosChatMsg, o.PosChatMsg, d.PosChatMsg);
        }

        private void LoadOverlayMetrics(OverlaySettings o)
        {
            var d = _overlayDefaults;
            btnColorStatsBackground.BackColor = OverlayColorHelper.RgbaToColor(o.StatsBackgroundR, o.StatsBackgroundG, o.StatsBackgroundB, o.StatsBackgroundA);
            btnColorStatsText.BackColor = OverlayColorHelper.RgbaToColor(o.StatsTextR, o.StatsTextG, o.StatsTextB, o.StatsTextA);
            SetNudIntIfDiff(numFpsAveragingWindow, o.FpsAveragingWindow, d.FpsAveragingWindow);
            SetChkIfDiff(chkAlwaysShowUserInfo, o.OverlayAlwaysShowUserInfo, d.OverlayAlwaysShowUserInfo);
            SetChkIfDiff(chkAlwaysShowFPS, o.OverlayAlwaysShowFps, d.OverlayAlwaysShowFps);
            SetChkIfDiff(chkAlwaysShowFrametime, o.OverlayAlwaysShowFrametime, d.OverlayAlwaysShowFrametime);
            SetChkIfDiff(chkAlwaysShowPlaytime, o.OverlayAlwaysShowPlaytime, d.OverlayAlwaysShowPlaytime);
            SetNudFloatIfDiff(numStatsPosX, o.StatsPosX, d.StatsPosX);
            SetNudFloatIfDiff(numStatsPosY, o.StatsPosY, d.StatsPosY);
        }

        private void LoadSaveManagement(OverlaySettings o, UserSettings userSettings)
        {
            SetChkIfDiff(chkDisableWarningLocalSave, o.DisableWarningLocalSave, _overlayDefaults.DisableWarningLocalSave);
            RefreshPersistedSaveLocationFromIni(userSettings);
            txtSavesFolderName.Text = _persistedSavesFolderName;
            InitializeSaveLocationComboBox(userSettings.LocalSavePath, userSettings.AccountSteamId);
            ApplyLocalSaveWarningForSaveLocationMode();
        }

        private OverlaySettings BuildOverlaySettingsFromControls()
        {
            var d = _overlayDefaults;
            bool activeGray = btnColorActiveElements.BackColor == Color.Gray;
            var overlay = new OverlaySettings
            {
                EnableExperimentalOverlay = chkEnableExperimentalOverlay.Checked,
                HookDelaySec = (int)numHookDelay.Value,
                RendererDetectorTimeoutSec = (int)numRendererDetectorTimeout.Value,
                DisableAchievementNotification = chkDisableAchievementNotification.Checked,
                DisableFriendNotification = chkDisableFriendNotification.Checked,
                DisableAchievementProgress = chkDisableAchievementProgress.Checked,
                DisableWarningAny = chkDisableWarningAny.Checked,
                DisableWarningBadAppId = chkDisableWarningBadAppId.Checked,
                DisableWarningLocalSave = chkDisableWarningLocalSave.Checked,
                UploadAchievementsIconsToGpu = chkUploadAchievementsToGPU.Checked,
                FpsAveragingWindow = (int)numFpsAveragingWindow.Value,
                OverlayAlwaysShowUserInfo = chkAlwaysShowUserInfo.Checked,
                OverlayAlwaysShowFps = chkAlwaysShowFPS.Checked,
                OverlayAlwaysShowFrametime = chkAlwaysShowFrametime.Checked,
                OverlayAlwaysShowPlaytime = chkAlwaysShowPlaytime.Checked,
                FontOverride = cmbFontOverride.Text == "Default" ? string.Empty : cmbFontOverride.Text,
                FontSize = (float)numFontSize.Value,
                IconSize = (float)numIconSize.Value,
                FontGlyphExtraSpacingX = (float)numFontSpacingX.Value,
                FontGlyphExtraSpacingY = (float)numFontSpacingY.Value,
                NotificationR = OverlayColorHelper.ColorToR(btnColorNotification.BackColor),
                NotificationG = OverlayColorHelper.ColorToG(btnColorNotification.BackColor),
                NotificationB = OverlayColorHelper.ColorToB(btnColorNotification.BackColor),
                NotificationA = OverlayColorHelper.ColorToA(btnColorNotification.BackColor),
                NotificationRounding = (float)numNotificationRounding.Value,
                NotificationMarginX = (float)numNotificationMarginX.Value,
                NotificationMarginY = (float)numNotificationMarginY.Value,
                BackgroundR = OverlayColorHelper.ColorToR(btnColorBackground.BackColor),
                BackgroundG = OverlayColorHelper.ColorToG(btnColorBackground.BackColor),
                BackgroundB = OverlayColorHelper.ColorToB(btnColorBackground.BackColor),
                BackgroundA = OverlayColorHelper.ColorToA(btnColorBackground.BackColor),
                ElementR = OverlayColorHelper.ColorToR(btnColorElements.BackColor),
                ElementG = OverlayColorHelper.ColorToG(btnColorElements.BackColor),
                ElementB = OverlayColorHelper.ColorToB(btnColorElements.BackColor),
                ElementA = OverlayColorHelper.ColorToA(btnColorElements.BackColor),
                ElementHoveredR = OverlayColorHelper.ColorToR(btnColorHoveredElements.BackColor),
                ElementHoveredG = OverlayColorHelper.ColorToG(btnColorHoveredElements.BackColor),
                ElementHoveredB = OverlayColorHelper.ColorToB(btnColorHoveredElements.BackColor),
                ElementHoveredA = OverlayColorHelper.ColorToA(btnColorHoveredElements.BackColor),
                ElementActiveR = activeGray ? -1.0f : OverlayColorHelper.ColorToR(btnColorActiveElements.BackColor),
                ElementActiveG = activeGray ? -1.0f : OverlayColorHelper.ColorToG(btnColorActiveElements.BackColor),
                ElementActiveB = activeGray ? -1.0f : OverlayColorHelper.ColorToB(btnColorActiveElements.BackColor),
                ElementActiveA = activeGray ? -1.0f : OverlayColorHelper.ColorToA(btnColorActiveElements.BackColor),
                NotificationAnimation = (float)numNotificationAnimation.Value,
                NotificationDurationProgress = (float)numNotificationDurationProgress.Value,
                NotificationDurationAchievement = (float)numNotificationDurationAchievement.Value,
                NotificationDurationInvitation = (float)numNotificationDurationInvitation.Value,
                NotificationDurationChat = (float)numNotificationDurationChat.Value,
                AchievementUnlockDatetimeFormat = cmbAchievementDateTimeFormat.Text,
                PosAchievement = string.IsNullOrEmpty(cmbPosAchievement.Text) ? d.PosAchievement : cmbPosAchievement.Text,
                PosInvitation = string.IsNullOrEmpty(cmbPosInvitation.Text) ? d.PosInvitation : cmbPosInvitation.Text,
                PosChatMsg = string.IsNullOrEmpty(cmbPosChatMsg.Text) ? d.PosChatMsg : cmbPosChatMsg.Text,
                StatsPosX = (float)numStatsPosX.Value,
                StatsPosY = (float)numStatsPosY.Value,
                StatsBackgroundR = OverlayColorHelper.ColorToR(btnColorStatsBackground.BackColor),
                StatsBackgroundG = OverlayColorHelper.ColorToG(btnColorStatsBackground.BackColor),
                StatsBackgroundB = OverlayColorHelper.ColorToB(btnColorStatsBackground.BackColor),
                StatsBackgroundA = OverlayColorHelper.ColorToA(btnColorStatsBackground.BackColor),
                StatsTextR = OverlayColorHelper.ColorToR(btnColorStatsText.BackColor),
                StatsTextG = OverlayColorHelper.ColorToG(btnColorStatsText.BackColor),
                StatsTextB = OverlayColorHelper.ColorToB(btnColorStatsText.BackColor),
                StatsTextA = OverlayColorHelper.ColorToA(btnColorStatsText.BackColor)
            };
            return overlay;
        }

        private Models.SaveResult SaveGoldbergSettings()
        {
            try
            {
                var overlaySettings = BuildOverlaySettingsFromControls();
                var mainSettings = BuildEmulatorSettings();
                string steamIdForSaves = txtSteamID != null ? txtSteamID.Text.Trim() : string.Empty;
                UserIniSaveLocationHelper.ResolveGlobalSaveFields(
                    out string localSavePath,
                    out string savesFolderName,
                    GetPersistedLocalSavePathFromUi(),
                    txtSavesFolderName.Text,
                    cmbSaveLocation.SelectedIndex == SaveLocationSteamUserdata,
                    steamIdForSaves);

                if (cmbSaveLocation.SelectedIndex == SaveLocationSteamUserdata)
                {
                    if (!SteamIdHelper.TryGetSteam3AccountId(txtSteamID != null ? txtSteamID.Text.Trim() : string.Empty, out _))
                    {
                        return Models.SaveResult.Failure(
                            "Steam userdata requires a valid Steam64 ID on the User tab (converted to Steam3AccountID for userdata folders).");
                    }

                    if (!GoldbergSavePathHelper.TryEnsureSteamUserdataAccountDirectory(txtSteamID.Text.Trim()))
                    {
                        return Models.SaveResult.Failure(
                            "Steam userdata save location requires a detected Steam installation and a valid Steam64 ID.");
                    }
                }

                if (cmbSaveLocation.SelectedIndex == SaveLocationCustom)
                {
                    var customPathResult = GoldbergSavePathHelper.ValidateCustomLocalSavePath(
                        localSavePath,
                        txtSteamID != null ? txtSteamID.Text.Trim() : string.Empty);
                    if (!customPathResult.IsValid)
                        return Models.SaveResult.Failure(customPathResult.ErrorMessage);
                }

                var userSettings = new UserSettings
                {
                    AccountName = txtUsername.Text,
                    AccountSteamId = txtSteamID.Text,
                    Language = SteamLanguageDisplayHelper.ToLanguageCode(cmbLanguage.Text),
                    IpCountry = cmbCountry.Text,
                    LocalSavePath = localSavePath,
                    SavesFolderName = savesFolderName
                };
                var overlayResult = _goldbergCfgService.SaveGlobalOverlaySettings(overlaySettings);
                if (!overlayResult.IsSuccess)
                    return overlayResult;
                var mainResult = _goldbergCfgService.SaveGlobalMainSettings(mainSettings);
                if (!mainResult.IsSuccess)
                    return mainResult;
                var userResult = _goldbergCfgService.SaveGlobalUserSettings(userSettings);
                if (!userResult.IsSuccess)
                    return userResult;

                ServiceLocator.EmulatorConfigService?.StripSaveLocationFromAllPerGameUserIni();
                return userResult;
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to save Goldberg settings: {ex.Message}");
                return Models.SaveResult.Failure($"Failed to save settings: {ex.Message}");
            }
        }

        private void StoreInitialState()
        {
            _initialApiKey = txtSteamWebApiKey.Text;
            _initialOverlaySettings = BuildOverlaySettingsFromControls();
            _initialMainSettings = BuildEmulatorSettings();
            _initialUserSettings = new UserSettings
            {
                AccountName = txtUsername.Text,
                AccountSteamId = txtSteamID.Text,
                Language = SteamLanguageDisplayHelper.ToLanguageCode(cmbLanguage.Text),
                IpCountry = cmbCountry.Text,
                LocalSavePath = GetPersistedLocalSavePathFromUi(),
                SavesFolderName = txtSavesFolderName.Text.Trim()
            };
            _pendingAvatarChange = PendingAvatarChange.None;
            _pendingAvatarSourcePath = null;
            SyncPersistedSaveLocationFromInitialState();
        }

        private static string ResolveSteamIdFromCombo(ComboBox combo)
        {
            if (combo == null)
                return string.Empty;
            if (combo.SelectedItem is SteamIdProfileListItem profile)
                return profile.SteamId;
            return (combo.Text ?? string.Empty).Trim();
        }

        private sealed class SteamIdProfileListItem
        {
            public string SteamId { get; }
            public string DisplayName { get; }

            public SteamIdProfileListItem(string steamId, string displayName)
            {
                SteamId = steamId != null ? steamId.Trim() : string.Empty;
                DisplayName = displayName != null ? displayName.Trim() : string.Empty;
            }

            public override string ToString() => SteamId;
        }
    }
}
