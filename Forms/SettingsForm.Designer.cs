using SmartGoldbergEmu.Properties;

namespace SmartGoldbergEmu.Forms
{
    partial class SettingsForm
    {

        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (picAvatar?.Image != null && picAvatar.Image != Resources.gold_steam_128_logo)
                {
                    picAvatar.Image.Dispose();
                }
                
                if (components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabUserAccount = new System.Windows.Forms.TabPage();
            this.grpSteamWebApi = new System.Windows.Forms.GroupBox();
            this.lblApiKeyHint = new System.Windows.Forms.Label();
            this.btnRemoveApiKey = new System.Windows.Forms.Button();
            this.txtSteamWebApiKey = new System.Windows.Forms.TextBox();
            this.lblSteamWebApiKey = new System.Windows.Forms.Label();
            this.lnkSteamWebApiKey = new System.Windows.Forms.LinkLabel();
            this.lblApiKeyValidation = new System.Windows.Forms.Label();
            this.grpAccountIdentity = new System.Windows.Forms.GroupBox();
            this.lblProfileHint = new System.Windows.Forms.Label();
            this.btnRemoveSteamIdProfile = new System.Windows.Forms.Button();
            this.btnClearAvatar = new System.Windows.Forms.Button();
            this.picAvatar = new System.Windows.Forms.PictureBox();
            this.cmbCountry = new System.Windows.Forms.ComboBox();
            this.lblCountry = new System.Windows.Forms.Label();
            this.btnSetAvatar = new System.Windows.Forms.Button();
            this.cmbLanguage = new System.Windows.Forms.ComboBox();
            this.lblLanguage = new System.Windows.Forms.Label();
            this.btnRandomizeSteamID = new System.Windows.Forms.Button();
            this.txtSteamID = new System.Windows.Forms.ComboBox();
            this.lblSteamID = new System.Windows.Forms.Label();
            this.txtUsername = new System.Windows.Forms.TextBox();
            this.lblUsername = new System.Windows.Forms.Label();
            this.tabSaveManagement = new System.Windows.Forms.TabPage();
            this.grpSaveLocation = new System.Windows.Forms.GroupBox();
            this.chkDisableWarningLocalSave = new System.Windows.Forms.CheckBox();
            this.btnBrowseSavePath = new System.Windows.Forms.Button();
            this.txtSavesFolderName = new System.Windows.Forms.TextBox();
            this.lblSavesFolderName = new System.Windows.Forms.Label();
            this.btnOpenSaveFolder = new System.Windows.Forms.Button();
            this.txtLocalSavePath = new System.Windows.Forms.TextBox();
            this.lblLocalSavePath = new System.Windows.Forms.Label();
            this.cmbSaveLocation = new System.Windows.Forms.ComboBox();
            this.lblSaveLocation = new System.Windows.Forms.Label();
            this.tabOverlay = new System.Windows.Forms.TabPage();
            this.grpAdvancedOverlay = new System.Windows.Forms.GroupBox();
            this.btnOpenControllerFolder = new System.Windows.Forms.Button();
            this.numRendererDetectorTimeout = new System.Windows.Forms.NumericUpDown();
            this.chkDisableWarningBadAppId = new System.Windows.Forms.CheckBox();
            this.lblRendererDetectorTimeout = new System.Windows.Forms.Label();
            this.numHookDelay = new System.Windows.Forms.NumericUpDown();
            this.lblHookDelay = new System.Windows.Forms.Label();
            this.chkEnableExperimentalOverlay = new System.Windows.Forms.CheckBox();
            this.chkDisableWarningAny = new System.Windows.Forms.CheckBox();
            this.grpFontSettings = new System.Windows.Forms.GroupBox();
            this.btnOpenFontsFolder = new System.Windows.Forms.Button();
            this.btnBrowseFont = new System.Windows.Forms.Button();
            this.cmbFontOverride = new System.Windows.Forms.ComboBox();
            this.lblFontOverride = new System.Windows.Forms.Label();
            this.numFontSize = new System.Windows.Forms.NumericUpDown();
            this.lblFontSize = new System.Windows.Forms.Label();
            this.numFontSpacingY = new System.Windows.Forms.NumericUpDown();
            this.numFontSpacingX = new System.Windows.Forms.NumericUpDown();
            this.lblFontSpacing = new System.Windows.Forms.Label();
            this.grpOverlayAppearance = new System.Windows.Forms.GroupBox();
            this.numNotificationMarginY = new System.Windows.Forms.NumericUpDown();
            this.numNotificationMarginX = new System.Windows.Forms.NumericUpDown();
            this.lblNotificationMargin = new System.Windows.Forms.Label();
            this.numNotificationRounding = new System.Windows.Forms.NumericUpDown();
            this.lblNotificationRounding = new System.Windows.Forms.Label();
            this.btnColorBackground = new System.Windows.Forms.Button();
            this.lblBackground = new System.Windows.Forms.Label();
            this.btnResetColorBackground = new System.Windows.Forms.Button();
            this.btnColorElements = new System.Windows.Forms.Button();
            this.lblElements = new System.Windows.Forms.Label();
            this.btnResetColorElements = new System.Windows.Forms.Button();
            this.btnColorHoveredElements = new System.Windows.Forms.Button();
            this.lblHoveredElements = new System.Windows.Forms.Label();
            this.btnResetColorHoveredElements = new System.Windows.Forms.Button();
            this.btnColorActiveElements = new System.Windows.Forms.Button();
            this.lblActiveElements = new System.Windows.Forms.Label();
            this.btnResetColorActiveElements = new System.Windows.Forms.Button();
            this.tabNotifications = new System.Windows.Forms.TabPage();
            this.grpSoundSettings = new System.Windows.Forms.GroupBox();
            this.btnOpenSoundsFolder = new System.Windows.Forms.Button();
            this.btnSound2Browse = new System.Windows.Forms.Button();
            this.cmbSound2File = new System.Windows.Forms.ComboBox();
            this.btnSound1Browse = new System.Windows.Forms.Button();
            this.cmbSound1File = new System.Windows.Forms.ComboBox();
            this.btnSound2Default = new System.Windows.Forms.Button();
            this.btnSound2PlayStop = new System.Windows.Forms.Button();
            this.lblSound2 = new System.Windows.Forms.Label();
            this.btnSound1Default = new System.Windows.Forms.Button();
            this.btnSound1PlayStop = new System.Windows.Forms.Button();
            this.lblSound1 = new System.Windows.Forms.Label();
            this.grpNotificationSettings = new System.Windows.Forms.GroupBox();
            this.btnResetNotificationColor = new System.Windows.Forms.Button();
            this.chkDisableFriendNotification = new System.Windows.Forms.CheckBox();
            this.lblNotificationDurationInvitation = new System.Windows.Forms.Label();
            this.numNotificationDurationInvitation = new System.Windows.Forms.NumericUpDown();
            this.lblNotificationDurationChat = new System.Windows.Forms.Label();
            this.numNotificationDurationChat = new System.Windows.Forms.NumericUpDown();
            this.lblNotificationAnimation = new System.Windows.Forms.Label();
            this.numNotificationAnimation = new System.Windows.Forms.NumericUpDown();
            this.lblNotification = new System.Windows.Forms.Label();
            this.btnColorNotification = new System.Windows.Forms.Button();
            this.lblPosInvitation = new System.Windows.Forms.Label();
            this.cmbPosInvitation = new System.Windows.Forms.ComboBox();
            this.lblPosChatMsg = new System.Windows.Forms.Label();
            this.cmbPosChatMsg = new System.Windows.Forms.ComboBox();
            this.grpAchievements = new System.Windows.Forms.GroupBox();
            this.chkUploadAchievementsToGPU = new System.Windows.Forms.CheckBox();
            this.chkDisableAchievementNotification = new System.Windows.Forms.CheckBox();
            this.chkDisableAchievementProgress = new System.Windows.Forms.CheckBox();
            this.lblNotificationDurationAchievement = new System.Windows.Forms.Label();
            this.numNotificationDurationAchievement = new System.Windows.Forms.NumericUpDown();
            this.lblNotificationDurationProgress = new System.Windows.Forms.Label();
            this.numNotificationDurationProgress = new System.Windows.Forms.NumericUpDown();
            this.lblAchievementDateTimeFormat = new System.Windows.Forms.Label();
            this.cmbAchievementDateTimeFormat = new System.Windows.Forms.ComboBox();
            this.lblPosAchievement = new System.Windows.Forms.Label();
            this.cmbPosAchievement = new System.Windows.Forms.ComboBox();
            this.lblIconSize = new System.Windows.Forms.Label();
            this.numIconSize = new System.Windows.Forms.NumericUpDown();
            this.tabMetrics = new System.Windows.Forms.TabPage();
            this.grpFPSDisplay = new System.Windows.Forms.GroupBox();
            this.chkAlwaysShowPlaytime = new System.Windows.Forms.CheckBox();
            this.chkAlwaysShowFrametime = new System.Windows.Forms.CheckBox();
            this.chkAlwaysShowFPS = new System.Windows.Forms.CheckBox();
            this.chkAlwaysShowUserInfo = new System.Windows.Forms.CheckBox();
            this.numFpsAveragingWindow = new System.Windows.Forms.NumericUpDown();
            this.lblFpsAveragingWindow = new System.Windows.Forms.Label();
            this.btnColorStatsText = new System.Windows.Forms.Button();
            this.btnResetColorStatsText = new System.Windows.Forms.Button();
            this.lblStatsText = new System.Windows.Forms.Label();
            this.lblStatsBackground = new System.Windows.Forms.Label();
            this.btnColorStatsBackground = new System.Windows.Forms.Button();
            this.btnResetColorStatsBackground = new System.Windows.Forms.Button();
            this.lblStatsPosition = new System.Windows.Forms.Label();
            this.numStatsPosX = new System.Windows.Forms.NumericUpDown();
            this.numStatsPosY = new System.Windows.Forms.NumericUpDown();
            this.tabEmulator = new System.Windows.Forms.TabPage();
            this.grpEmulatorWorkarounds = new System.Windows.Forms.GroupBox();
            this.chkUse32BitInventoryItemIds = new System.Windows.Forms.CheckBox();
            this.chkFreeWeekend = new System.Windows.Forms.CheckBox();
            this.chkEnableSteamPreownedIds = new System.Windows.Forms.CheckBox();
            this.chkDisableSteamoverlaygameidEnvVar = new System.Windows.Forms.CheckBox();
            this.chkForceSteamhttpSuccess = new System.Windows.Forms.CheckBox();
            this.chkAchievementsBypass = new System.Windows.Forms.CheckBox();
            this.grpEmulatorStats = new System.Windows.Forms.GroupBox();
            this.btnBrowseSteamGameStatsReportsDir = new System.Windows.Forms.Button();
            this.txtSteamGameStatsReportsDir = new System.Windows.Forms.TextBox();
            this.lblStatsReportsFolder = new System.Windows.Forms.Label();
            this.numIconsPerIteration = new System.Windows.Forms.NumericUpDown();
            this.lblIconsPerIteration = new System.Windows.Forms.Label();
            this.chkRecordPlaytime = new System.Windows.Forms.CheckBox();
            this.chkSaveOnlyHigherStatAchievementProgress = new System.Windows.Forms.CheckBox();
            this.chkStatAchievementProgressFunctionality = new System.Windows.Forms.CheckBox();
            this.chkAllowUnknownStats = new System.Windows.Forms.CheckBox();
            this.chkDisableLeaderboardsCreateUnknown = new System.Windows.Forms.CheckBox();
            this.grpEmulatorSession = new System.Windows.Forms.GroupBox();
            this.chkSteamDeck = new System.Windows.Forms.CheckBox();
            this.chkEnableVoiceChat = new System.Windows.Forms.CheckBox();
            this.chkEnableAccountAvatar = new System.Windows.Forms.CheckBox();
            this.chkGameCoordinatorToken = new System.Windows.Forms.CheckBox();
            this.chkModernAuthTicket = new System.Windows.Forms.CheckBox();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.tabControl.SuspendLayout();
            this.tabUserAccount.SuspendLayout();
            this.grpSteamWebApi.SuspendLayout();
            this.grpAccountIdentity.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picAvatar)).BeginInit();
            this.tabSaveManagement.SuspendLayout();
            this.grpSaveLocation.SuspendLayout();
            this.tabOverlay.SuspendLayout();
            this.grpAdvancedOverlay.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numRendererDetectorTimeout)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numHookDelay)).BeginInit();
            this.grpFontSettings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numFontSize)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numFontSpacingY)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numFontSpacingX)).BeginInit();
            this.grpOverlayAppearance.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numNotificationMarginY)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numNotificationMarginX)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numNotificationRounding)).BeginInit();
            this.tabNotifications.SuspendLayout();
            this.grpSoundSettings.SuspendLayout();
            this.grpNotificationSettings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numNotificationDurationInvitation)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numNotificationDurationChat)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numNotificationAnimation)).BeginInit();
            this.grpAchievements.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numNotificationDurationAchievement)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numNotificationDurationProgress)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numIconSize)).BeginInit();
            this.tabMetrics.SuspendLayout();
            this.grpFPSDisplay.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numFpsAveragingWindow)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numStatsPosX)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numStatsPosY)).BeginInit();
            this.tabEmulator.SuspendLayout();
            this.grpEmulatorWorkarounds.SuspendLayout();
            this.grpEmulatorStats.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numIconsPerIteration)).BeginInit();
            this.grpEmulatorSession.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl
            // 
            this.tabControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl.Controls.Add(this.tabUserAccount);
            this.tabControl.Controls.Add(this.tabSaveManagement);
            this.tabControl.Controls.Add(this.tabOverlay);
            this.tabControl.Controls.Add(this.tabNotifications);
            this.tabControl.Controls.Add(this.tabMetrics);
            this.tabControl.Controls.Add(this.tabEmulator);
            this.tabControl.Location = new System.Drawing.Point(-1, 0);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(600, 519);
            this.tabControl.TabIndex = 0;
            this.tabControl.SelectedIndexChanged += new System.EventHandler(this.tabControl_SelectedIndexChanged);
            // 
            // tabUserAccount
            // 
            this.tabUserAccount.Controls.Add(this.grpSteamWebApi);
            this.tabUserAccount.Controls.Add(this.grpAccountIdentity);
            this.tabUserAccount.Location = new System.Drawing.Point(4, 22);
            this.tabUserAccount.Name = "tabUserAccount";
            this.tabUserAccount.Padding = new System.Windows.Forms.Padding(12);
            this.tabUserAccount.Size = new System.Drawing.Size(592, 533);
            this.tabUserAccount.TabIndex = 0;
            this.tabUserAccount.Text = "User Account";
            this.tabUserAccount.UseVisualStyleBackColor = true;
            // 
            // grpSteamWebApi
            // 
            this.grpSteamWebApi.Controls.Add(this.lblApiKeyHint);
            this.grpSteamWebApi.Controls.Add(this.btnRemoveApiKey);
            this.grpSteamWebApi.Controls.Add(this.txtSteamWebApiKey);
            this.grpSteamWebApi.Controls.Add(this.lblSteamWebApiKey);
            this.grpSteamWebApi.Controls.Add(this.lnkSteamWebApiKey);
            this.grpSteamWebApi.Controls.Add(this.lblApiKeyValidation);
            this.grpSteamWebApi.Location = new System.Drawing.Point(12, 194);
            this.grpSteamWebApi.Name = "grpSteamWebApi";
            this.grpSteamWebApi.Padding = new System.Windows.Forms.Padding(12);
            this.grpSteamWebApi.Size = new System.Drawing.Size(568, 118);
            this.grpSteamWebApi.TabIndex = 1;
            this.grpSteamWebApi.TabStop = false;
            this.grpSteamWebApi.Text = "Steam Web API Key (Optional)";
            // 
            // lblApiKeyHint
            // 
            this.lblApiKeyHint.AutoSize = true;
            this.lblApiKeyHint.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblApiKeyHint.Location = new System.Drawing.Point(516, 26);
            this.lblApiKeyHint.Name = "lblApiKeyHint";
            this.lblApiKeyHint.Size = new System.Drawing.Size(19, 17);
            this.lblApiKeyHint.TabIndex = 10;
            this.lblApiKeyHint.Text = "❓";
            // 
            // btnRemoveApiKey
            // 
            this.btnRemoveApiKey.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRemoveApiKey.Location = new System.Drawing.Point(487, 23);
            this.btnRemoveApiKey.Name = "btnRemoveApiKey";
            this.btnRemoveApiKey.Size = new System.Drawing.Size(23, 23);
            this.btnRemoveApiKey.TabIndex = 3;
            this.btnRemoveApiKey.Text = "🗑";
            this.btnRemoveApiKey.UseVisualStyleBackColor = true;
            this.btnRemoveApiKey.Click += new System.EventHandler(this.OnRemoveApiKey_Click);
            // 
            // txtSteamWebApiKey
            // 
            this.txtSteamWebApiKey.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtSteamWebApiKey.Location = new System.Drawing.Point(226, 25);
            this.txtSteamWebApiKey.MaxLength = 32;
            this.txtSteamWebApiKey.Name = "txtSteamWebApiKey";
            this.txtSteamWebApiKey.Size = new System.Drawing.Size(257, 20);
            this.txtSteamWebApiKey.TabIndex = 2;
            // 
            // lblSteamWebApiKey
            // 
            this.lblSteamWebApiKey.AutoSize = true;
            this.lblSteamWebApiKey.Location = new System.Drawing.Point(147, 28);
            this.lblSteamWebApiKey.Name = "lblSteamWebApiKey";
            this.lblSteamWebApiKey.Size = new System.Drawing.Size(48, 13);
            this.lblSteamWebApiKey.TabIndex = 1;
            this.lblSteamWebApiKey.Text = "API Key:";
            // 
            // lnkSteamWebApiKey
            // 
            this.lnkSteamWebApiKey.AutoSize = true;
            this.lnkSteamWebApiKey.Location = new System.Drawing.Point(223, 48);
            this.lnkSteamWebApiKey.Name = "lnkSteamWebApiKey";
            this.lnkSteamWebApiKey.Size = new System.Drawing.Size(124, 13);
            this.lnkSteamWebApiKey.TabIndex = 4;
            this.lnkSteamWebApiKey.TabStop = true;
            this.lnkSteamWebApiKey.Text = "Get Steam Web API Key";
            this.lnkSteamWebApiKey.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.OnSteamWebApiKey_LinkClicked);
            // 
            // lblApiKeyValidation
            // 
            this.lblApiKeyValidation.AutoSize = true;
            this.lblApiKeyValidation.Location = new System.Drawing.Point(226, 65);
            this.lblApiKeyValidation.MaximumSize = new System.Drawing.Size(257, 0);
            this.lblApiKeyValidation.Name = "lblApiKeyValidation";
            this.lblApiKeyValidation.Size = new System.Drawing.Size(0, 13);
            this.lblApiKeyValidation.TabIndex = 5;
            // 
            // grpAccountIdentity
            // 
            this.grpAccountIdentity.Controls.Add(this.lblProfileHint);
            this.grpAccountIdentity.Controls.Add(this.btnRemoveSteamIdProfile);
            this.grpAccountIdentity.Controls.Add(this.btnClearAvatar);
            this.grpAccountIdentity.Controls.Add(this.picAvatar);
            this.grpAccountIdentity.Controls.Add(this.cmbCountry);
            this.grpAccountIdentity.Controls.Add(this.lblCountry);
            this.grpAccountIdentity.Controls.Add(this.btnSetAvatar);
            this.grpAccountIdentity.Controls.Add(this.cmbLanguage);
            this.grpAccountIdentity.Controls.Add(this.lblLanguage);
            this.grpAccountIdentity.Controls.Add(this.btnRandomizeSteamID);
            this.grpAccountIdentity.Controls.Add(this.txtSteamID);
            this.grpAccountIdentity.Controls.Add(this.lblSteamID);
            this.grpAccountIdentity.Controls.Add(this.txtUsername);
            this.grpAccountIdentity.Controls.Add(this.lblUsername);
            this.grpAccountIdentity.Location = new System.Drawing.Point(12, 12);
            this.grpAccountIdentity.Name = "grpAccountIdentity";
            this.grpAccountIdentity.Padding = new System.Windows.Forms.Padding(12);
            this.grpAccountIdentity.Size = new System.Drawing.Size(568, 168);
            this.grpAccountIdentity.TabIndex = 0;
            this.grpAccountIdentity.TabStop = false;
            this.grpAccountIdentity.Text = "Account Identity";
            // 
            // lblProfileHint
            // 
            this.lblProfileHint.AutoSize = true;
            this.lblProfileHint.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblProfileHint.Location = new System.Drawing.Point(516, 48);
            this.lblProfileHint.Name = "lblProfileHint";
            this.lblProfileHint.Size = new System.Drawing.Size(19, 17);
            this.lblProfileHint.TabIndex = 9;
            this.lblProfileHint.Text = "❓";
            // 
            // btnRemoveSteamIdProfile
            // 
            this.btnRemoveSteamIdProfile.Location = new System.Drawing.Point(460, 45);
            this.btnRemoveSteamIdProfile.Name = "btnRemoveSteamIdProfile";
            this.btnRemoveSteamIdProfile.Size = new System.Drawing.Size(23, 23);
            this.btnRemoveSteamIdProfile.TabIndex = 3;
            this.btnRemoveSteamIdProfile.Text = "🗑";
            this.toolTip.SetToolTip(this.btnRemoveSteamIdProfile, "Remove selected Steam ID profile");
            this.btnRemoveSteamIdProfile.UseVisualStyleBackColor = true;
            this.btnRemoveSteamIdProfile.Click += new System.EventHandler(this.OnRemoveSteamIdProfile_Click);
            // 
            // btnClearAvatar
            // 
            this.btnClearAvatar.Location = new System.Drawing.Point(108, 133);
            this.btnClearAvatar.Name = "btnClearAvatar";
            this.btnClearAvatar.Size = new System.Drawing.Size(23, 23);
            this.btnClearAvatar.TabIndex = 8;
            this.btnClearAvatar.Text = "🗙";
            this.btnClearAvatar.UseVisualStyleBackColor = true;
            this.btnClearAvatar.Click += new System.EventHandler(this.OnClearAvatar_Click);
            // 
            // picAvatar
            // 
            this.picAvatar.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picAvatar.Image = global::SmartGoldbergEmu.Properties.Resources.gold_steam_128_logo;
            this.picAvatar.Location = new System.Drawing.Point(23, 19);
            this.picAvatar.Name = "picAvatar";
            this.picAvatar.Size = new System.Drawing.Size(108, 108);
            this.picAvatar.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.picAvatar.TabIndex = 4;
            this.picAvatar.TabStop = false;
            // 
            // cmbCountry
            // 
            this.cmbCountry.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbCountry.FormattingEnabled = true;
            this.cmbCountry.Items.AddRange(new object[] {
            "US",
            "GB",
            "DE",
            "FR",
            "ES",
            "IT",
            "RU",
            "JP",
            "CN",
            "KR"});
            this.cmbCountry.Location = new System.Drawing.Point(226, 104);
            this.cmbCountry.Name = "cmbCountry";
            this.cmbCountry.Size = new System.Drawing.Size(309, 21);
            this.cmbCountry.TabIndex = 6;
            // 
            // lblCountry
            // 
            this.lblCountry.AutoSize = true;
            this.lblCountry.Location = new System.Drawing.Point(147, 107);
            this.lblCountry.Name = "lblCountry";
            this.lblCountry.Size = new System.Drawing.Size(46, 13);
            this.lblCountry.TabIndex = 6;
            this.lblCountry.Text = "Country:";
            // 
            // btnSetAvatar
            // 
            this.btnSetAvatar.Location = new System.Drawing.Point(23, 133);
            this.btnSetAvatar.Name = "btnSetAvatar";
            this.btnSetAvatar.Size = new System.Drawing.Size(79, 23);
            this.btnSetAvatar.TabIndex = 7;
            this.btnSetAvatar.Text = "Set Avatar";
            this.btnSetAvatar.UseVisualStyleBackColor = true;
            this.btnSetAvatar.Click += new System.EventHandler(this.OnSetAvatar_Click);
            // 
            // cmbLanguage
            // 
            this.cmbLanguage.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbLanguage.FormattingEnabled = true;
            this.cmbLanguage.Location = new System.Drawing.Point(226, 74);
            this.cmbLanguage.Name = "cmbLanguage";
            this.cmbLanguage.Size = new System.Drawing.Size(309, 21);
            this.cmbLanguage.TabIndex = 5;
            // 
            // lblLanguage
            // 
            this.lblLanguage.AutoSize = true;
            this.lblLanguage.Location = new System.Drawing.Point(147, 77);
            this.lblLanguage.Name = "lblLanguage";
            this.lblLanguage.Size = new System.Drawing.Size(58, 13);
            this.lblLanguage.TabIndex = 4;
            this.lblLanguage.Text = "Language:";
            // 
            // btnRandomizeSteamID
            // 
            this.btnRandomizeSteamID.Location = new System.Drawing.Point(489, 45);
            this.btnRandomizeSteamID.Name = "btnRandomizeSteamID";
            this.btnRandomizeSteamID.Size = new System.Drawing.Size(23, 23);
            this.btnRandomizeSteamID.TabIndex = 4;
            this.btnRandomizeSteamID.Text = "🎲";
            this.toolTip.SetToolTip(this.btnRandomizeSteamID, "Generate a random Steam64 ID");
            this.btnRandomizeSteamID.UseVisualStyleBackColor = true;
            this.btnRandomizeSteamID.Click += new System.EventHandler(this.OnRandomizeSteamID_Click);
            // 
            // txtSteamID
            // 
            this.txtSteamID.FormattingEnabled = true;
            this.txtSteamID.Location = new System.Drawing.Point(226, 46);
            this.txtSteamID.Name = "txtSteamID";
            this.txtSteamID.Size = new System.Drawing.Size(230, 21);
            this.txtSteamID.TabIndex = 2;
            // 
            // lblSteamID
            // 
            this.lblSteamID.AutoSize = true;
            this.lblSteamID.Location = new System.Drawing.Point(147, 50);
            this.lblSteamID.Name = "lblSteamID";
            this.lblSteamID.Size = new System.Drawing.Size(54, 13);
            this.lblSteamID.TabIndex = 1;
            this.lblSteamID.Text = "Steam ID:";
            // 
            // txtUsername
            // 
            this.txtUsername.Location = new System.Drawing.Point(226, 19);
            this.txtUsername.MaxLength = 32;
            this.txtUsername.Name = "txtUsername";
            this.txtUsername.Size = new System.Drawing.Size(309, 20);
            this.txtUsername.TabIndex = 1;
            // 
            // lblUsername
            // 
            this.lblUsername.AutoSize = true;
            this.lblUsername.Location = new System.Drawing.Point(147, 22);
            this.lblUsername.Name = "lblUsername";
            this.lblUsername.Size = new System.Drawing.Size(58, 13);
            this.lblUsername.TabIndex = 0;
            this.lblUsername.Text = "Username:";
            // 
            // tabSaveManagement
            // 
            this.tabSaveManagement.Controls.Add(this.grpSaveLocation);
            this.tabSaveManagement.Location = new System.Drawing.Point(4, 22);
            this.tabSaveManagement.Name = "tabSaveManagement";
            this.tabSaveManagement.Padding = new System.Windows.Forms.Padding(12);
            this.tabSaveManagement.Size = new System.Drawing.Size(592, 533);
            this.tabSaveManagement.TabIndex = 1;
            this.tabSaveManagement.Text = "Save Management";
            this.tabSaveManagement.UseVisualStyleBackColor = true;
            // 
            // grpSaveLocation
            // 
            this.grpSaveLocation.Controls.Add(this.chkDisableWarningLocalSave);
            this.grpSaveLocation.Controls.Add(this.btnBrowseSavePath);
            this.grpSaveLocation.Controls.Add(this.txtSavesFolderName);
            this.grpSaveLocation.Controls.Add(this.lblSavesFolderName);
            this.grpSaveLocation.Controls.Add(this.btnOpenSaveFolder);
            this.grpSaveLocation.Controls.Add(this.txtLocalSavePath);
            this.grpSaveLocation.Controls.Add(this.lblLocalSavePath);
            this.grpSaveLocation.Controls.Add(this.cmbSaveLocation);
            this.grpSaveLocation.Controls.Add(this.lblSaveLocation);
            this.grpSaveLocation.Location = new System.Drawing.Point(12, 12);
            this.grpSaveLocation.Name = "grpSaveLocation";
            this.grpSaveLocation.Padding = new System.Windows.Forms.Padding(12);
            this.grpSaveLocation.Size = new System.Drawing.Size(568, 148);
            this.grpSaveLocation.TabIndex = 0;
            this.grpSaveLocation.TabStop = false;
            this.grpSaveLocation.Text = "Save Location";
            // 
            // chkDisableWarningLocalSave
            // 
            this.chkDisableWarningLocalSave.AutoSize = true;
            this.chkDisableWarningLocalSave.Location = new System.Drawing.Point(150, 112);
            this.chkDisableWarningLocalSave.Name = "chkDisableWarningLocalSave";
            this.chkDisableWarningLocalSave.Size = new System.Drawing.Size(161, 17);
            this.chkDisableWarningLocalSave.TabIndex = 8;
            this.chkDisableWarningLocalSave.Text = "Disable Local Save Warning";
            this.chkDisableWarningLocalSave.UseVisualStyleBackColor = true;
            // 
            // btnBrowseSavePath
            // 
            this.btnBrowseSavePath.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseSavePath.Location = new System.Drawing.Point(495, 53);
            this.btnBrowseSavePath.Name = "btnBrowseSavePath";
            this.btnBrowseSavePath.Size = new System.Drawing.Size(23, 23);
            this.btnBrowseSavePath.TabIndex = 4;
            this.btnBrowseSavePath.Text = "🔍";
            this.btnBrowseSavePath.UseVisualStyleBackColor = true;
            this.btnBrowseSavePath.Click += new System.EventHandler(this.OnBrowseSavePath_Click);
            // 
            // txtSavesFolderName
            // 
            this.txtSavesFolderName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtSavesFolderName.Location = new System.Drawing.Point(150, 81);
            this.txtSavesFolderName.Name = "txtSavesFolderName";
            this.txtSavesFolderName.Size = new System.Drawing.Size(397, 20);
            this.txtSavesFolderName.TabIndex = 7;
            // 
            // lblSavesFolderName
            // 
            this.lblSavesFolderName.AutoSize = true;
            this.lblSavesFolderName.Location = new System.Drawing.Point(20, 84);
            this.lblSavesFolderName.Name = "lblSavesFolderName";
            this.lblSavesFolderName.Size = new System.Drawing.Size(103, 13);
            this.lblSavesFolderName.TabIndex = 5;
            this.lblSavesFolderName.Text = "Saves Folder Name:";
            // 
            // btnOpenSaveFolder
            // 
            this.btnOpenSaveFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOpenSaveFolder.Location = new System.Drawing.Point(524, 53);
            this.btnOpenSaveFolder.Name = "btnOpenSaveFolder";
            this.btnOpenSaveFolder.Size = new System.Drawing.Size(23, 23);
            this.btnOpenSaveFolder.TabIndex = 5;
            this.btnOpenSaveFolder.Text = "📁";
            this.btnOpenSaveFolder.UseVisualStyleBackColor = true;
            this.btnOpenSaveFolder.Click += new System.EventHandler(this.OnOpenSaveFolder_Click);
            // 
            // txtLocalSavePath
            // 
            this.txtLocalSavePath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtLocalSavePath.Location = new System.Drawing.Point(150, 55);
            this.txtLocalSavePath.Name = "txtLocalSavePath";
            this.txtLocalSavePath.Size = new System.Drawing.Size(339, 20);
            this.txtLocalSavePath.TabIndex = 3;
            // 
            // lblLocalSavePath
            // 
            this.lblLocalSavePath.AutoSize = true;
            this.lblLocalSavePath.Location = new System.Drawing.Point(20, 58);
            this.lblLocalSavePath.Name = "lblLocalSavePath";
            this.lblLocalSavePath.Size = new System.Drawing.Size(60, 13);
            this.lblLocalSavePath.TabIndex = 2;
            this.lblLocalSavePath.Text = "Save Path:";
            // 
            // cmbSaveLocation
            // 
            this.cmbSaveLocation.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.cmbSaveLocation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbSaveLocation.FormattingEnabled = true;
            this.cmbSaveLocation.Location = new System.Drawing.Point(150, 28);
            this.cmbSaveLocation.Name = "cmbSaveLocation";
            this.cmbSaveLocation.Size = new System.Drawing.Size(397, 21);
            this.cmbSaveLocation.TabIndex = 1;
            this.cmbSaveLocation.SelectedIndexChanged += new System.EventHandler(this.cmbSaveLocation_SelectedIndexChanged);
            // 
            // lblSaveLocation
            // 
            this.lblSaveLocation.AutoSize = true;
            this.lblSaveLocation.Location = new System.Drawing.Point(20, 31);
            this.lblSaveLocation.Name = "lblSaveLocation";
            this.lblSaveLocation.Size = new System.Drawing.Size(78, 13);
            this.lblSaveLocation.TabIndex = 0;
            this.lblSaveLocation.Text = "Location Type:";
            // 
            // tabOverlay
            // 
            this.tabOverlay.Controls.Add(this.grpAdvancedOverlay);
            this.tabOverlay.Controls.Add(this.grpFontSettings);
            this.tabOverlay.Controls.Add(this.grpOverlayAppearance);
            this.tabOverlay.Location = new System.Drawing.Point(4, 22);
            this.tabOverlay.Name = "tabOverlay";
            this.tabOverlay.Padding = new System.Windows.Forms.Padding(12);
            this.tabOverlay.Size = new System.Drawing.Size(592, 481);
            this.tabOverlay.TabIndex = 2;
            this.tabOverlay.Text = "Overlay";
            this.tabOverlay.UseVisualStyleBackColor = true;
            // 
            // grpAdvancedOverlay
            // 
            this.grpAdvancedOverlay.Controls.Add(this.btnOpenControllerFolder);
            this.grpAdvancedOverlay.Controls.Add(this.numRendererDetectorTimeout);
            this.grpAdvancedOverlay.Controls.Add(this.chkDisableWarningBadAppId);
            this.grpAdvancedOverlay.Controls.Add(this.lblRendererDetectorTimeout);
            this.grpAdvancedOverlay.Controls.Add(this.numHookDelay);
            this.grpAdvancedOverlay.Controls.Add(this.lblHookDelay);
            this.grpAdvancedOverlay.Controls.Add(this.chkEnableExperimentalOverlay);
            this.grpAdvancedOverlay.Controls.Add(this.chkDisableWarningAny);
            this.grpAdvancedOverlay.Location = new System.Drawing.Point(12, 12);
            this.grpAdvancedOverlay.Name = "grpAdvancedOverlay";
            this.grpAdvancedOverlay.Padding = new System.Windows.Forms.Padding(12);
            this.grpAdvancedOverlay.Size = new System.Drawing.Size(568, 135);
            this.grpAdvancedOverlay.TabIndex = 0;
            this.grpAdvancedOverlay.TabStop = false;
            this.grpAdvancedOverlay.Text = "Advanced Overlay Settings";
            // 
            // btnOpenControllerFolder
            // 
            this.btnOpenControllerFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOpenControllerFolder.Location = new System.Drawing.Point(472, 88);
            this.btnOpenControllerFolder.Name = "btnOpenControllerFolder";
            this.btnOpenControllerFolder.Size = new System.Drawing.Size(75, 23);
            this.btnOpenControllerFolder.TabIndex = 5;
            this.btnOpenControllerFolder.Text = "Glyphs...";
            this.btnOpenControllerFolder.UseVisualStyleBackColor = true;
            this.btnOpenControllerFolder.Click += new System.EventHandler(this.OnOpenControllerFolder_Click);
            // 
            // numRendererDetectorTimeout
            // 
            this.numRendererDetectorTimeout.Location = new System.Drawing.Point(180, 58);
            this.numRendererDetectorTimeout.Maximum = new decimal(new int[] {
            60,
            0,
            0,
            0});
            this.numRendererDetectorTimeout.Minimum = new decimal(new int[] {
            5,
            0,
            0,
            0});
            this.numRendererDetectorTimeout.Name = "numRendererDetectorTimeout";
            this.numRendererDetectorTimeout.Size = new System.Drawing.Size(60, 20);
            this.numRendererDetectorTimeout.TabIndex = 2;
            this.numRendererDetectorTimeout.Value = new decimal(new int[] {
            15,
            0,
            0,
            0});
            // 
            // chkDisableWarningBadAppId
            // 
            this.chkDisableWarningBadAppId.AutoSize = true;
            this.chkDisableWarningBadAppId.Location = new System.Drawing.Point(280, 30);
            this.chkDisableWarningBadAppId.Name = "chkDisableWarningBadAppId";
            this.chkDisableWarningBadAppId.Size = new System.Drawing.Size(169, 17);
            this.chkDisableWarningBadAppId.TabIndex = 1;
            this.chkDisableWarningBadAppId.Text = "Disable \"Bad App ID\" warning";
            this.chkDisableWarningBadAppId.UseVisualStyleBackColor = true;
            // 
            // lblRendererDetectorTimeout
            // 
            this.lblRendererDetectorTimeout.AutoSize = true;
            this.lblRendererDetectorTimeout.Location = new System.Drawing.Point(20, 60);
            this.lblRendererDetectorTimeout.Name = "lblRendererDetectorTimeout";
            this.lblRendererDetectorTimeout.Size = new System.Drawing.Size(139, 13);
            this.lblRendererDetectorTimeout.TabIndex = 3;
            this.lblRendererDetectorTimeout.Text = "Renderer Detector Timeout:";
            // 
            // numHookDelay
            // 
            this.numHookDelay.Location = new System.Drawing.Point(180, 88);
            this.numHookDelay.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.numHookDelay.Name = "numHookDelay";
            this.numHookDelay.Size = new System.Drawing.Size(60, 20);
            this.numHookDelay.TabIndex = 4;
            // 
            // lblHookDelay
            // 
            this.lblHookDelay.AutoSize = true;
            this.lblHookDelay.Location = new System.Drawing.Point(20, 90);
            this.lblHookDelay.Name = "lblHookDelay";
            this.lblHookDelay.Size = new System.Drawing.Size(92, 13);
            this.lblHookDelay.TabIndex = 1;
            this.lblHookDelay.Text = "Hook Delay (sec):";
            // 
            // chkEnableExperimentalOverlay
            // 
            this.chkEnableExperimentalOverlay.AutoSize = true;
            this.chkEnableExperimentalOverlay.Location = new System.Drawing.Point(20, 30);
            this.chkEnableExperimentalOverlay.Name = "chkEnableExperimentalOverlay";
            this.chkEnableExperimentalOverlay.Size = new System.Drawing.Size(161, 17);
            this.chkEnableExperimentalOverlay.TabIndex = 0;
            this.chkEnableExperimentalOverlay.Tag = "WarningColor";
            this.chkEnableExperimentalOverlay.Text = "Enable Experimental Overlay";
            this.chkEnableExperimentalOverlay.UseVisualStyleBackColor = true;
            // 
            // chkDisableWarningAny
            // 
            this.chkDisableWarningAny.AutoSize = true;
            this.chkDisableWarningAny.Location = new System.Drawing.Point(280, 60);
            this.chkDisableWarningAny.Name = "chkDisableWarningAny";
            this.chkDisableWarningAny.Size = new System.Drawing.Size(119, 17);
            this.chkDisableWarningAny.TabIndex = 3;
            this.chkDisableWarningAny.Text = "Disable all warnings";
            this.chkDisableWarningAny.UseVisualStyleBackColor = true;
            // 
            // grpFontSettings
            // 
            this.grpFontSettings.Controls.Add(this.btnOpenFontsFolder);
            this.grpFontSettings.Controls.Add(this.btnBrowseFont);
            this.grpFontSettings.Controls.Add(this.cmbFontOverride);
            this.grpFontSettings.Controls.Add(this.lblFontOverride);
            this.grpFontSettings.Controls.Add(this.numFontSize);
            this.grpFontSettings.Controls.Add(this.lblFontSize);
            this.grpFontSettings.Controls.Add(this.numFontSpacingY);
            this.grpFontSettings.Controls.Add(this.numFontSpacingX);
            this.grpFontSettings.Controls.Add(this.lblFontSpacing);
            this.grpFontSettings.Location = new System.Drawing.Point(12, 301);
            this.grpFontSettings.Name = "grpFontSettings";
            this.grpFontSettings.Padding = new System.Windows.Forms.Padding(12);
            this.grpFontSettings.Size = new System.Drawing.Size(568, 139);
            this.grpFontSettings.TabIndex = 2;
            this.grpFontSettings.TabStop = false;
            this.grpFontSettings.Text = "Font Settings";
            // 
            // btnOpenFontsFolder
            // 
            this.btnOpenFontsFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOpenFontsFolder.Location = new System.Drawing.Point(434, 85);
            this.btnOpenFontsFolder.Name = "btnOpenFontsFolder";
            this.btnOpenFontsFolder.Size = new System.Drawing.Size(75, 23);
            this.btnOpenFontsFolder.TabIndex = 6;
            this.btnOpenFontsFolder.Text = "Open Folder";
            this.btnOpenFontsFolder.UseVisualStyleBackColor = true;
            this.btnOpenFontsFolder.Click += new System.EventHandler(this.OnOpenFontsFolder_Click);
            // 
            // btnBrowseFont
            // 
            this.btnBrowseFont.Location = new System.Drawing.Point(335, 26);
            this.btnBrowseFont.Name = "btnBrowseFont";
            this.btnBrowseFont.Size = new System.Drawing.Size(23, 23);
            this.btnBrowseFont.TabIndex = 2;
            this.btnBrowseFont.Text = "🔍";
            this.btnBrowseFont.UseVisualStyleBackColor = true;
            this.btnBrowseFont.Click += new System.EventHandler(this.OnBrowseFont_Click);
            // 
            // cmbFontOverride
            // 
            this.cmbFontOverride.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbFontOverride.FormattingEnabled = true;
            this.cmbFontOverride.Location = new System.Drawing.Point(110, 27);
            this.cmbFontOverride.Name = "cmbFontOverride";
            this.cmbFontOverride.Size = new System.Drawing.Size(219, 21);
            this.cmbFontOverride.TabIndex = 1;
            // 
            // lblFontOverride
            // 
            this.lblFontOverride.AutoSize = true;
            this.lblFontOverride.Location = new System.Drawing.Point(20, 30);
            this.lblFontOverride.Name = "lblFontOverride";
            this.lblFontOverride.Size = new System.Drawing.Size(31, 13);
            this.lblFontOverride.TabIndex = 7;
            this.lblFontOverride.Text = "Font:";
            // 
            // numFontSize
            // 
            this.numFontSize.DecimalPlaces = 1;
            this.numFontSize.Location = new System.Drawing.Point(180, 58);
            this.numFontSize.Maximum = new decimal(new int[] {
            72,
            0,
            0,
            0});
            this.numFontSize.Minimum = new decimal(new int[] {
            8,
            0,
            0,
            0});
            this.numFontSize.Name = "numFontSize";
            this.numFontSize.Size = new System.Drawing.Size(60, 20);
            this.numFontSize.TabIndex = 3;
            this.numFontSize.Value = new decimal(new int[] {
            20,
            0,
            0,
            0});
            // 
            // lblFontSize
            // 
            this.lblFontSize.AutoSize = true;
            this.lblFontSize.Location = new System.Drawing.Point(20, 60);
            this.lblFontSize.Name = "lblFontSize";
            this.lblFontSize.Size = new System.Drawing.Size(54, 13);
            this.lblFontSize.TabIndex = 3;
            this.lblFontSize.Text = "Font Size:";
            // 
            // numFontSpacingY
            // 
            this.numFontSpacingY.DecimalPlaces = 1;
            this.numFontSpacingY.Location = new System.Drawing.Point(250, 88);
            this.numFontSpacingY.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.numFontSpacingY.Minimum = new decimal(new int[] {
            -5,
            0,
            0,
            -2147483648});
            this.numFontSpacingY.Name = "numFontSpacingY";
            this.numFontSpacingY.Size = new System.Drawing.Size(60, 20);
            this.numFontSpacingY.TabIndex = 5;
            // 
            // numFontSpacingX
            // 
            this.numFontSpacingX.DecimalPlaces = 1;
            this.numFontSpacingX.Location = new System.Drawing.Point(180, 88);
            this.numFontSpacingX.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.numFontSpacingX.Minimum = new decimal(new int[] {
            -5,
            0,
            0,
            -2147483648});
            this.numFontSpacingX.Name = "numFontSpacingX";
            this.numFontSpacingX.Size = new System.Drawing.Size(60, 20);
            this.numFontSpacingX.TabIndex = 4;
            this.numFontSpacingX.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // lblFontSpacing
            // 
            this.lblFontSpacing.AutoSize = true;
            this.lblFontSpacing.Location = new System.Drawing.Point(20, 90);
            this.lblFontSpacing.Name = "lblFontSpacing";
            this.lblFontSpacing.Size = new System.Drawing.Size(101, 13);
            this.lblFontSpacing.TabIndex = 0;
            this.lblFontSpacing.Text = "Font Spacing (X/Y):";
            // 
            // grpOverlayAppearance
            // 
            this.grpOverlayAppearance.Controls.Add(this.numNotificationMarginY);
            this.grpOverlayAppearance.Controls.Add(this.numNotificationMarginX);
            this.grpOverlayAppearance.Controls.Add(this.lblNotificationMargin);
            this.grpOverlayAppearance.Controls.Add(this.numNotificationRounding);
            this.grpOverlayAppearance.Controls.Add(this.lblNotificationRounding);
            this.grpOverlayAppearance.Controls.Add(this.btnColorBackground);
            this.grpOverlayAppearance.Controls.Add(this.lblBackground);
            this.grpOverlayAppearance.Controls.Add(this.btnResetColorBackground);
            this.grpOverlayAppearance.Controls.Add(this.btnColorElements);
            this.grpOverlayAppearance.Controls.Add(this.lblElements);
            this.grpOverlayAppearance.Controls.Add(this.btnResetColorElements);
            this.grpOverlayAppearance.Controls.Add(this.btnColorHoveredElements);
            this.grpOverlayAppearance.Controls.Add(this.lblHoveredElements);
            this.grpOverlayAppearance.Controls.Add(this.btnResetColorHoveredElements);
            this.grpOverlayAppearance.Controls.Add(this.btnColorActiveElements);
            this.grpOverlayAppearance.Controls.Add(this.lblActiveElements);
            this.grpOverlayAppearance.Controls.Add(this.btnResetColorActiveElements);
            this.grpOverlayAppearance.Location = new System.Drawing.Point(12, 161);
            this.grpOverlayAppearance.Name = "grpOverlayAppearance";
            this.grpOverlayAppearance.Padding = new System.Windows.Forms.Padding(12);
            this.grpOverlayAppearance.Size = new System.Drawing.Size(568, 134);
            this.grpOverlayAppearance.TabIndex = 1;
            this.grpOverlayAppearance.TabStop = false;
            this.grpOverlayAppearance.Text = "Overlay Appearance";
            // 
            // numNotificationMarginY
            // 
            this.numNotificationMarginY.DecimalPlaces = 1;
            this.numNotificationMarginY.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.numNotificationMarginY.Location = new System.Drawing.Point(180, 88);
            this.numNotificationMarginY.Maximum = new decimal(new int[] {
            50,
            0,
            0,
            0});
            this.numNotificationMarginY.Name = "numNotificationMarginY";
            this.numNotificationMarginY.Size = new System.Drawing.Size(60, 20);
            this.numNotificationMarginY.TabIndex = 9;
            this.numNotificationMarginY.Value = new decimal(new int[] {
            5,
            0,
            0,
            0});
            // 
            // numNotificationMarginX
            // 
            this.numNotificationMarginX.DecimalPlaces = 1;
            this.numNotificationMarginX.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.numNotificationMarginX.Location = new System.Drawing.Point(110, 88);
            this.numNotificationMarginX.Maximum = new decimal(new int[] {
            50,
            0,
            0,
            0});
            this.numNotificationMarginX.Name = "numNotificationMarginX";
            this.numNotificationMarginX.Size = new System.Drawing.Size(60, 20);
            this.numNotificationMarginX.TabIndex = 8;
            this.numNotificationMarginX.Value = new decimal(new int[] {
            5,
            0,
            0,
            0});
            // 
            // lblNotificationMargin
            // 
            this.lblNotificationMargin.AutoSize = true;
            this.lblNotificationMargin.Location = new System.Drawing.Point(20, 90);
            this.lblNotificationMargin.Name = "lblNotificationMargin";
            this.lblNotificationMargin.Size = new System.Drawing.Size(70, 13);
            this.lblNotificationMargin.TabIndex = 12;
            this.lblNotificationMargin.Text = "Margin (X/Y):";
            // 
            // numNotificationRounding
            // 
            this.numNotificationRounding.DecimalPlaces = 1;
            this.numNotificationRounding.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.numNotificationRounding.Location = new System.Drawing.Point(180, 58);
            this.numNotificationRounding.Maximum = new decimal(new int[] {
            50,
            0,
            0,
            0});
            this.numNotificationRounding.Name = "numNotificationRounding";
            this.numNotificationRounding.Size = new System.Drawing.Size(60, 20);
            this.numNotificationRounding.TabIndex = 5;
            this.numNotificationRounding.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            // 
            // lblNotificationRounding
            // 
            this.lblNotificationRounding.AutoSize = true;
            this.lblNotificationRounding.Location = new System.Drawing.Point(20, 60);
            this.lblNotificationRounding.Name = "lblNotificationRounding";
            this.lblNotificationRounding.Size = new System.Drawing.Size(95, 13);
            this.lblNotificationRounding.TabIndex = 10;
            this.lblNotificationRounding.Text = "Corners Rounding:";
            // 
            // btnColorBackground
            // 
            this.btnColorBackground.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(31)))), ((int)(((byte)(28)))), ((int)(((byte)(28)))), ((int)(((byte)(140)))));
            this.btnColorBackground.Location = new System.Drawing.Point(180, 28);
            this.btnColorBackground.Name = "btnColorBackground";
            this.btnColorBackground.Size = new System.Drawing.Size(60, 23);
            this.btnColorBackground.TabIndex = 1;
            this.btnColorBackground.UseVisualStyleBackColor = false;
            this.btnColorBackground.Click += new System.EventHandler(this.OnColorBackground_Click);
            // 
            // lblBackground
            // 
            this.lblBackground.AutoSize = true;
            this.lblBackground.Location = new System.Drawing.Point(20, 30);
            this.lblBackground.Name = "lblBackground";
            this.lblBackground.Size = new System.Drawing.Size(68, 13);
            this.lblBackground.TabIndex = 2;
            this.lblBackground.Text = "Background:";
            // 
            // btnResetColorBackground
            // 
            this.btnResetColorBackground.Location = new System.Drawing.Point(246, 28);
            this.btnResetColorBackground.Name = "btnResetColorBackground";
            this.btnResetColorBackground.Size = new System.Drawing.Size(23, 23);
            this.btnResetColorBackground.TabIndex = 2;
            this.btnResetColorBackground.Text = "❌";
            this.btnResetColorBackground.UseVisualStyleBackColor = true;
            this.btnResetColorBackground.Click += new System.EventHandler(this.OnResetColorBackground_Click);
            // 
            // btnColorElements
            // 
            this.btnColorElements.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(77)))), ((int)(((byte)(82)))), ((int)(((byte)(102)))));
            this.btnColorElements.Location = new System.Drawing.Point(420, 28);
            this.btnColorElements.Name = "btnColorElements";
            this.btnColorElements.Size = new System.Drawing.Size(60, 23);
            this.btnColorElements.TabIndex = 3;
            this.btnColorElements.UseVisualStyleBackColor = false;
            this.btnColorElements.Click += new System.EventHandler(this.OnColorElement_Click);
            // 
            // lblElements
            // 
            this.lblElements.AutoSize = true;
            this.lblElements.Location = new System.Drawing.Point(280, 30);
            this.lblElements.Name = "lblElements";
            this.lblElements.Size = new System.Drawing.Size(75, 13);
            this.lblElements.TabIndex = 4;
            this.lblElements.Text = "Element Color:";
            this.lblElements.Click += new System.EventHandler(this.OnElement_Click);
            // 
            // btnResetColorElements
            // 
            this.btnResetColorElements.Location = new System.Drawing.Point(486, 28);
            this.btnResetColorElements.Name = "btnResetColorElements";
            this.btnResetColorElements.Size = new System.Drawing.Size(23, 23);
            this.btnResetColorElements.TabIndex = 4;
            this.btnResetColorElements.Text = "❌";
            this.btnResetColorElements.UseVisualStyleBackColor = true;
            this.btnResetColorElements.Click += new System.EventHandler(this.OnResetColorElements_Click);
            // 
            // btnColorHoveredElements
            // 
            this.btnColorHoveredElements.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(71)))), ((int)(((byte)(100)))), ((int)(((byte)(154)))));
            this.btnColorHoveredElements.Location = new System.Drawing.Point(420, 58);
            this.btnColorHoveredElements.Name = "btnColorHoveredElements";
            this.btnColorHoveredElements.Size = new System.Drawing.Size(60, 23);
            this.btnColorHoveredElements.TabIndex = 6;
            this.btnColorHoveredElements.UseVisualStyleBackColor = false;
            this.btnColorHoveredElements.Click += new System.EventHandler(this.OnColorElementHovered_Click);
            // 
            // lblHoveredElements
            // 
            this.lblHoveredElements.AutoSize = true;
            this.lblHoveredElements.Location = new System.Drawing.Point(280, 60);
            this.lblHoveredElements.Name = "lblHoveredElements";
            this.lblHoveredElements.Size = new System.Drawing.Size(119, 13);
            this.lblHoveredElements.TabIndex = 6;
            this.lblHoveredElements.Text = "Hovered Element Color:";
            // 
            // btnResetColorHoveredElements
            // 
            this.btnResetColorHoveredElements.Location = new System.Drawing.Point(486, 58);
            this.btnResetColorHoveredElements.Name = "btnResetColorHoveredElements";
            this.btnResetColorHoveredElements.Size = new System.Drawing.Size(23, 23);
            this.btnResetColorHoveredElements.TabIndex = 7;
            this.btnResetColorHoveredElements.Text = "❌";
            this.btnResetColorHoveredElements.UseVisualStyleBackColor = true;
            this.btnResetColorHoveredElements.Click += new System.EventHandler(this.OnResetColorHoveredElements_Click);
            // 
            // btnColorActiveElements
            // 
            this.btnColorActiveElements.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(77)))), ((int)(((byte)(82)))), ((int)(((byte)(102)))));
            this.btnColorActiveElements.Location = new System.Drawing.Point(420, 88);
            this.btnColorActiveElements.Name = "btnColorActiveElements";
            this.btnColorActiveElements.Size = new System.Drawing.Size(60, 23);
            this.btnColorActiveElements.TabIndex = 10;
            this.btnColorActiveElements.UseVisualStyleBackColor = false;
            this.btnColorActiveElements.Click += new System.EventHandler(this.OnColorElementActive_Click);
            // 
            // lblActiveElements
            // 
            this.lblActiveElements.AutoSize = true;
            this.lblActiveElements.Location = new System.Drawing.Point(280, 90);
            this.lblActiveElements.Name = "lblActiveElements";
            this.lblActiveElements.Size = new System.Drawing.Size(108, 13);
            this.lblActiveElements.TabIndex = 8;
            this.lblActiveElements.Text = "Active Element Color:";
            // 
            // btnResetColorActiveElements
            // 
            this.btnResetColorActiveElements.Location = new System.Drawing.Point(486, 88);
            this.btnResetColorActiveElements.Name = "btnResetColorActiveElements";
            this.btnResetColorActiveElements.Size = new System.Drawing.Size(23, 23);
            this.btnResetColorActiveElements.TabIndex = 11;
            this.btnResetColorActiveElements.Text = "❌";
            this.btnResetColorActiveElements.UseVisualStyleBackColor = true;
            this.btnResetColorActiveElements.Click += new System.EventHandler(this.OnResetColorActiveElements_Click);
            // 
            // tabNotifications
            // 
            this.tabNotifications.Controls.Add(this.grpSoundSettings);
            this.tabNotifications.Controls.Add(this.grpNotificationSettings);
            this.tabNotifications.Controls.Add(this.grpAchievements);
            this.tabNotifications.Location = new System.Drawing.Point(4, 22);
            this.tabNotifications.Name = "tabNotifications";
            this.tabNotifications.Padding = new System.Windows.Forms.Padding(12);
            this.tabNotifications.Size = new System.Drawing.Size(592, 493);
            this.tabNotifications.TabIndex = 3;
            this.tabNotifications.Text = "Notifications";
            this.tabNotifications.UseVisualStyleBackColor = true;
            // 
            // grpSoundSettings
            // 
            this.grpSoundSettings.Controls.Add(this.btnOpenSoundsFolder);
            this.grpSoundSettings.Controls.Add(this.btnSound2Browse);
            this.grpSoundSettings.Controls.Add(this.cmbSound2File);
            this.grpSoundSettings.Controls.Add(this.btnSound1Browse);
            this.grpSoundSettings.Controls.Add(this.cmbSound1File);
            this.grpSoundSettings.Controls.Add(this.btnSound2Default);
            this.grpSoundSettings.Controls.Add(this.btnSound2PlayStop);
            this.grpSoundSettings.Controls.Add(this.lblSound2);
            this.grpSoundSettings.Controls.Add(this.btnSound1Default);
            this.grpSoundSettings.Controls.Add(this.btnSound1PlayStop);
            this.grpSoundSettings.Controls.Add(this.lblSound1);
            this.grpSoundSettings.Location = new System.Drawing.Point(12, 340);
            this.grpSoundSettings.Name = "grpSoundSettings";
            this.grpSoundSettings.Padding = new System.Windows.Forms.Padding(12);
            this.grpSoundSettings.Size = new System.Drawing.Size(568, 138);
            this.grpSoundSettings.TabIndex = 2;
            this.grpSoundSettings.TabStop = false;
            this.grpSoundSettings.Text = "Sound Settings";
            // 
            // btnOpenSoundsFolder
            // 
            this.btnOpenSoundsFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOpenSoundsFolder.Location = new System.Drawing.Point(465, 92);
            this.btnOpenSoundsFolder.Name = "btnOpenSoundsFolder";
            this.btnOpenSoundsFolder.Size = new System.Drawing.Size(75, 23);
            this.btnOpenSoundsFolder.TabIndex = 11;
            this.btnOpenSoundsFolder.Text = "Open Folder";
            this.btnOpenSoundsFolder.UseVisualStyleBackColor = true;
            this.btnOpenSoundsFolder.Click += new System.EventHandler(this.OnOpenSoundsFolder_Click);
            // 
            // btnSound2Browse
            // 
            this.btnSound2Browse.Location = new System.Drawing.Point(289, 93);
            this.btnSound2Browse.Name = "btnSound2Browse";
            this.btnSound2Browse.Size = new System.Drawing.Size(23, 23);
            this.btnSound2Browse.TabIndex = 7;
            this.btnSound2Browse.Text = "🔍";
            this.btnSound2Browse.UseVisualStyleBackColor = true;
            this.btnSound2Browse.Click += new System.EventHandler(this.OnSound2Browse_Click);
            // 
            // cmbSound2File
            // 
            this.cmbSound2File.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbSound2File.FormattingEnabled = true;
            this.cmbSound2File.Location = new System.Drawing.Point(23, 94);
            this.cmbSound2File.Name = "cmbSound2File";
            this.cmbSound2File.Size = new System.Drawing.Size(260, 21);
            this.cmbSound2File.TabIndex = 6;
            // 
            // btnSound1Browse
            // 
            this.btnSound1Browse.Location = new System.Drawing.Point(289, 44);
            this.btnSound1Browse.Name = "btnSound1Browse";
            this.btnSound1Browse.Size = new System.Drawing.Size(23, 23);
            this.btnSound1Browse.TabIndex = 2;
            this.btnSound1Browse.Text = "🔍";
            this.btnSound1Browse.UseVisualStyleBackColor = true;
            this.btnSound1Browse.Click += new System.EventHandler(this.OnSound1Browse_Click);
            // 
            // cmbSound1File
            // 
            this.cmbSound1File.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbSound1File.FormattingEnabled = true;
            this.cmbSound1File.Location = new System.Drawing.Point(23, 45);
            this.cmbSound1File.Name = "cmbSound1File";
            this.cmbSound1File.Size = new System.Drawing.Size(260, 21);
            this.cmbSound1File.TabIndex = 1;
            // 
            // btnSound2Default
            // 
            this.btnSound2Default.Cursor = System.Windows.Forms.Cursors.Default;
            this.btnSound2Default.Location = new System.Drawing.Point(347, 93);
            this.btnSound2Default.Name = "btnSound2Default";
            this.btnSound2Default.Size = new System.Drawing.Size(23, 23);
            this.btnSound2Default.TabIndex = 9;
            this.btnSound2Default.Text = "♻️";
            this.btnSound2Default.UseVisualStyleBackColor = true;
            this.btnSound2Default.Click += new System.EventHandler(this.OnSound2Default_Click);
            // 
            // btnSound2PlayStop
            // 
            this.btnSound2PlayStop.Font = new System.Drawing.Font("Segoe UI Symbol", 9F);
            this.btnSound2PlayStop.Location = new System.Drawing.Point(318, 93);
            this.btnSound2PlayStop.Name = "btnSound2PlayStop";
            this.btnSound2PlayStop.Size = new System.Drawing.Size(23, 23);
            this.btnSound2PlayStop.TabIndex = 8;
            this.btnSound2PlayStop.Text = "▶";
            this.btnSound2PlayStop.UseCompatibleTextRendering = true;
            this.btnSound2PlayStop.UseVisualStyleBackColor = true;
            this.btnSound2PlayStop.Click += new System.EventHandler(this.OnSound2PlayStop_Click);
            // 
            // lblSound2
            // 
            this.lblSound2.AutoSize = true;
            this.lblSound2.Location = new System.Drawing.Point(23, 78);
            this.lblSound2.Name = "lblSound2";
            this.lblSound2.Size = new System.Drawing.Size(93, 13);
            this.lblSound2.TabIndex = 5;
            this.lblSound2.Text = "Friend notification:";
            // 
            // btnSound1Default
            // 
            this.btnSound1Default.Location = new System.Drawing.Point(347, 44);
            this.btnSound1Default.Name = "btnSound1Default";
            this.btnSound1Default.Size = new System.Drawing.Size(23, 23);
            this.btnSound1Default.TabIndex = 5;
            this.btnSound1Default.Text = "♻️";
            this.btnSound1Default.UseVisualStyleBackColor = true;
            this.btnSound1Default.Click += new System.EventHandler(this.OnSound1Default_Click);
            // 
            // btnSound1PlayStop
            // 
            this.btnSound1PlayStop.Font = new System.Drawing.Font("Segoe UI Symbol", 9F);
            this.btnSound1PlayStop.Location = new System.Drawing.Point(318, 44);
            this.btnSound1PlayStop.Name = "btnSound1PlayStop";
            this.btnSound1PlayStop.Size = new System.Drawing.Size(23, 23);
            this.btnSound1PlayStop.TabIndex = 3;
            this.btnSound1PlayStop.Text = "▶";
            this.btnSound1PlayStop.UseCompatibleTextRendering = true;
            this.btnSound1PlayStop.UseVisualStyleBackColor = true;
            this.btnSound1PlayStop.Click += new System.EventHandler(this.OnSound1PlayStop_Click);
            // 
            // lblSound1
            // 
            this.lblSound1.AutoSize = true;
            this.lblSound1.Location = new System.Drawing.Point(23, 29);
            this.lblSound1.Name = "lblSound1";
            this.lblSound1.Size = new System.Drawing.Size(126, 13);
            this.lblSound1.TabIndex = 0;
            this.lblSound1.Text = "Achievement notification:";
            // 
            // grpNotificationSettings
            // 
            this.grpNotificationSettings.Controls.Add(this.btnResetNotificationColor);
            this.grpNotificationSettings.Controls.Add(this.chkDisableFriendNotification);
            this.grpNotificationSettings.Controls.Add(this.lblNotificationDurationInvitation);
            this.grpNotificationSettings.Controls.Add(this.numNotificationDurationInvitation);
            this.grpNotificationSettings.Controls.Add(this.lblNotificationDurationChat);
            this.grpNotificationSettings.Controls.Add(this.numNotificationDurationChat);
            this.grpNotificationSettings.Controls.Add(this.lblNotificationAnimation);
            this.grpNotificationSettings.Controls.Add(this.numNotificationAnimation);
            this.grpNotificationSettings.Controls.Add(this.lblNotification);
            this.grpNotificationSettings.Controls.Add(this.btnColorNotification);
            this.grpNotificationSettings.Controls.Add(this.lblPosInvitation);
            this.grpNotificationSettings.Controls.Add(this.cmbPosInvitation);
            this.grpNotificationSettings.Controls.Add(this.lblPosChatMsg);
            this.grpNotificationSettings.Controls.Add(this.cmbPosChatMsg);
            this.grpNotificationSettings.Location = new System.Drawing.Point(12, 12);
            this.grpNotificationSettings.Name = "grpNotificationSettings";
            this.grpNotificationSettings.Padding = new System.Windows.Forms.Padding(12);
            this.grpNotificationSettings.Size = new System.Drawing.Size(568, 156);
            this.grpNotificationSettings.TabIndex = 0;
            this.grpNotificationSettings.TabStop = false;
            this.grpNotificationSettings.Text = "Notifications";
            // 
            // btnResetNotificationColor
            // 
            this.btnResetNotificationColor.Location = new System.Drawing.Point(486, 23);
            this.btnResetNotificationColor.Name = "btnResetNotificationColor";
            this.btnResetNotificationColor.Size = new System.Drawing.Size(23, 23);
            this.btnResetNotificationColor.TabIndex = 3;
            this.btnResetNotificationColor.Text = "❌";
            this.btnResetNotificationColor.UseVisualStyleBackColor = true;
            this.btnResetNotificationColor.Click += new System.EventHandler(this.OnResetColorNotification_Click);
            // 
            // chkDisableFriendNotification
            // 
            this.chkDisableFriendNotification.AutoSize = true;
            this.chkDisableFriendNotification.Location = new System.Drawing.Point(280, 55);
            this.chkDisableFriendNotification.Name = "chkDisableFriendNotification";
            this.chkDisableFriendNotification.Size = new System.Drawing.Size(154, 17);
            this.chkDisableFriendNotification.TabIndex = 5;
            this.chkDisableFriendNotification.Text = "Disable Friend Notifications";
            this.chkDisableFriendNotification.UseVisualStyleBackColor = true;
            // 
            // lblNotificationDurationInvitation
            // 
            this.lblNotificationDurationInvitation.AutoSize = true;
            this.lblNotificationDurationInvitation.Location = new System.Drawing.Point(20, 55);
            this.lblNotificationDurationInvitation.Name = "lblNotificationDurationInvitation";
            this.lblNotificationDurationInvitation.Size = new System.Drawing.Size(110, 13);
            this.lblNotificationDurationInvitation.TabIndex = 4;
            this.lblNotificationDurationInvitation.Text = "Invitation Duration (s):";
            // 
            // numNotificationDurationInvitation
            // 
            this.numNotificationDurationInvitation.DecimalPlaces = 1;
            this.numNotificationDurationInvitation.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.numNotificationDurationInvitation.Location = new System.Drawing.Point(180, 53);
            this.numNotificationDurationInvitation.Maximum = new decimal(new int[] {
            60,
            0,
            0,
            0});
            this.numNotificationDurationInvitation.Name = "numNotificationDurationInvitation";
            this.numNotificationDurationInvitation.Size = new System.Drawing.Size(60, 20);
            this.numNotificationDurationInvitation.TabIndex = 4;
            this.numNotificationDurationInvitation.Value = new decimal(new int[] {
            8,
            0,
            0,
            0});
            // 
            // lblNotificationDurationChat
            // 
            this.lblNotificationDurationChat.AutoSize = true;
            this.lblNotificationDurationChat.Location = new System.Drawing.Point(20, 85);
            this.lblNotificationDurationChat.Name = "lblNotificationDurationChat";
            this.lblNotificationDurationChat.Size = new System.Drawing.Size(89, 13);
            this.lblNotificationDurationChat.TabIndex = 6;
            this.lblNotificationDurationChat.Text = "Chat Duration (s):";
            // 
            // numNotificationDurationChat
            // 
            this.numNotificationDurationChat.DecimalPlaces = 1;
            this.numNotificationDurationChat.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.numNotificationDurationChat.Location = new System.Drawing.Point(180, 83);
            this.numNotificationDurationChat.Maximum = new decimal(new int[] {
            60,
            0,
            0,
            0});
            this.numNotificationDurationChat.Name = "numNotificationDurationChat";
            this.numNotificationDurationChat.Size = new System.Drawing.Size(60, 20);
            this.numNotificationDurationChat.TabIndex = 6;
            this.numNotificationDurationChat.Value = new decimal(new int[] {
            4,
            0,
            0,
            0});
            // 
            // lblNotificationAnimation
            // 
            this.lblNotificationAnimation.AutoSize = true;
            this.lblNotificationAnimation.Location = new System.Drawing.Point(20, 25);
            this.lblNotificationAnimation.Name = "lblNotificationAnimation";
            this.lblNotificationAnimation.Size = new System.Drawing.Size(121, 13);
            this.lblNotificationAnimation.TabIndex = 8;
            this.lblNotificationAnimation.Text = "Animation Duration (ms):";
            // 
            // numNotificationAnimation
            // 
            this.numNotificationAnimation.DecimalPlaces = 2;
            this.numNotificationAnimation.Increment = new decimal(new int[] {
            1,
            0,
            0,
            131072});
            this.numNotificationAnimation.Location = new System.Drawing.Point(180, 23);
            this.numNotificationAnimation.Maximum = new decimal(new int[] {
            5,
            0,
            0,
            0});
            this.numNotificationAnimation.Name = "numNotificationAnimation";
            this.numNotificationAnimation.Size = new System.Drawing.Size(60, 20);
            this.numNotificationAnimation.TabIndex = 1;
            this.numNotificationAnimation.Value = new decimal(new int[] {
            35,
            0,
            0,
            131072});
            // 
            // lblNotification
            // 
            this.lblNotification.AutoSize = true;
            this.lblNotification.Location = new System.Drawing.Point(280, 25);
            this.lblNotification.Name = "lblNotification";
            this.lblNotification.Size = new System.Drawing.Size(90, 13);
            this.lblNotification.TabIndex = 0;
            this.lblNotification.Text = "Notification Color:";
            // 
            // btnColorNotification
            // 
            this.btnColorNotification.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(31)))), ((int)(((byte)(36)))), ((int)(((byte)(54)))));
            this.btnColorNotification.Location = new System.Drawing.Point(420, 23);
            this.btnColorNotification.Name = "btnColorNotification";
            this.btnColorNotification.Size = new System.Drawing.Size(60, 23);
            this.btnColorNotification.TabIndex = 2;
            this.btnColorNotification.UseVisualStyleBackColor = false;
            this.btnColorNotification.Click += new System.EventHandler(this.OnColorNotification_Click);
            // 
            // lblPosInvitation
            // 
            this.lblPosInvitation.AutoSize = true;
            this.lblPosInvitation.Location = new System.Drawing.Point(20, 116);
            this.lblPosInvitation.Name = "lblPosInvitation";
            this.lblPosInvitation.Size = new System.Drawing.Size(93, 13);
            this.lblPosInvitation.TabIndex = 7;
            this.lblPosInvitation.Text = "Invitation Position:";
            // 
            // cmbPosInvitation
            // 
            this.cmbPosInvitation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbPosInvitation.FormattingEnabled = true;
            this.cmbPosInvitation.Items.AddRange(new object[] {
            "top_center",
            "top_left",
            "top_right",
            "bot_center",
            "bot_left",
            "bot_right"});
            this.cmbPosInvitation.Location = new System.Drawing.Point(140, 113);
            this.cmbPosInvitation.Name = "cmbPosInvitation";
            this.cmbPosInvitation.Size = new System.Drawing.Size(120, 21);
            this.cmbPosInvitation.TabIndex = 8;
            // 
            // lblPosChatMsg
            // 
            this.lblPosChatMsg.AutoSize = true;
            this.lblPosChatMsg.Location = new System.Drawing.Point(280, 116);
            this.lblPosChatMsg.Name = "lblPosChatMsg";
            this.lblPosChatMsg.Size = new System.Drawing.Size(118, 13);
            this.lblPosChatMsg.TabIndex = 9;
            this.lblPosChatMsg.Text = "Chat Message Position:";
            // 
            // cmbPosChatMsg
            // 
            this.cmbPosChatMsg.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbPosChatMsg.FormattingEnabled = true;
            this.cmbPosChatMsg.Items.AddRange(new object[] {
            "top_center",
            "top_left",
            "top_right",
            "bot_center",
            "bot_left",
            "bot_right"});
            this.cmbPosChatMsg.Location = new System.Drawing.Point(420, 113);
            this.cmbPosChatMsg.Name = "cmbPosChatMsg";
            this.cmbPosChatMsg.Size = new System.Drawing.Size(120, 21);
            this.cmbPosChatMsg.TabIndex = 10;
            // 
            // grpAchievements
            // 
            this.grpAchievements.Controls.Add(this.chkUploadAchievementsToGPU);
            this.grpAchievements.Controls.Add(this.chkDisableAchievementNotification);
            this.grpAchievements.Controls.Add(this.chkDisableAchievementProgress);
            this.grpAchievements.Controls.Add(this.lblNotificationDurationAchievement);
            this.grpAchievements.Controls.Add(this.numNotificationDurationAchievement);
            this.grpAchievements.Controls.Add(this.lblNotificationDurationProgress);
            this.grpAchievements.Controls.Add(this.numNotificationDurationProgress);
            this.grpAchievements.Controls.Add(this.lblAchievementDateTimeFormat);
            this.grpAchievements.Controls.Add(this.cmbAchievementDateTimeFormat);
            this.grpAchievements.Controls.Add(this.lblPosAchievement);
            this.grpAchievements.Controls.Add(this.cmbPosAchievement);
            this.grpAchievements.Controls.Add(this.lblIconSize);
            this.grpAchievements.Controls.Add(this.numIconSize);
            this.grpAchievements.Location = new System.Drawing.Point(12, 174);
            this.grpAchievements.Name = "grpAchievements";
            this.grpAchievements.Padding = new System.Windows.Forms.Padding(12);
            this.grpAchievements.Size = new System.Drawing.Size(568, 160);
            this.grpAchievements.TabIndex = 1;
            this.grpAchievements.TabStop = false;
            this.grpAchievements.Text = "Achievements";
            // 
            // chkUploadAchievementsToGPU
            // 
            this.chkUploadAchievementsToGPU.AutoSize = true;
            this.chkUploadAchievementsToGPU.Checked = true;
            this.chkUploadAchievementsToGPU.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkUploadAchievementsToGPU.Location = new System.Drawing.Point(280, 30);
            this.chkUploadAchievementsToGPU.Name = "chkUploadAchievementsToGPU";
            this.chkUploadAchievementsToGPU.Size = new System.Drawing.Size(192, 17);
            this.chkUploadAchievementsToGPU.TabIndex = 2;
            this.chkUploadAchievementsToGPU.Text = "Upload Achievement Icons to GPU";
            this.chkUploadAchievementsToGPU.UseVisualStyleBackColor = true;
            // 
            // chkDisableAchievementNotification
            // 
            this.chkDisableAchievementNotification.AutoSize = true;
            this.chkDisableAchievementNotification.Location = new System.Drawing.Point(280, 60);
            this.chkDisableAchievementNotification.Name = "chkDisableAchievementNotification";
            this.chkDisableAchievementNotification.Size = new System.Drawing.Size(187, 17);
            this.chkDisableAchievementNotification.TabIndex = 4;
            this.chkDisableAchievementNotification.Text = "Disable Achievement Notifications";
            this.chkDisableAchievementNotification.UseVisualStyleBackColor = true;
            // 
            // chkDisableAchievementProgress
            // 
            this.chkDisableAchievementProgress.AutoSize = true;
            this.chkDisableAchievementProgress.Location = new System.Drawing.Point(280, 90);
            this.chkDisableAchievementProgress.Name = "chkDisableAchievementProgress";
            this.chkDisableAchievementProgress.Size = new System.Drawing.Size(170, 17);
            this.chkDisableAchievementProgress.TabIndex = 6;
            this.chkDisableAchievementProgress.Text = "Disable Achievement Progress";
            this.chkDisableAchievementProgress.UseVisualStyleBackColor = true;
            // 
            // lblNotificationDurationAchievement
            // 
            this.lblNotificationDurationAchievement.AutoSize = true;
            this.lblNotificationDurationAchievement.Location = new System.Drawing.Point(20, 60);
            this.lblNotificationDurationAchievement.Name = "lblNotificationDurationAchievement";
            this.lblNotificationDurationAchievement.Size = new System.Drawing.Size(129, 13);
            this.lblNotificationDurationAchievement.TabIndex = 2;
            this.lblNotificationDurationAchievement.Text = "Achievement Duration (s):";
            // 
            // numNotificationDurationAchievement
            // 
            this.numNotificationDurationAchievement.DecimalPlaces = 1;
            this.numNotificationDurationAchievement.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.numNotificationDurationAchievement.Location = new System.Drawing.Point(180, 58);
            this.numNotificationDurationAchievement.Maximum = new decimal(new int[] {
            60,
            0,
            0,
            0});
            this.numNotificationDurationAchievement.Name = "numNotificationDurationAchievement";
            this.numNotificationDurationAchievement.Size = new System.Drawing.Size(60, 20);
            this.numNotificationDurationAchievement.TabIndex = 3;
            this.numNotificationDurationAchievement.Value = new decimal(new int[] {
            7,
            0,
            0,
            0});
            // 
            // lblNotificationDurationProgress
            // 
            this.lblNotificationDurationProgress.AutoSize = true;
            this.lblNotificationDurationProgress.Location = new System.Drawing.Point(20, 90);
            this.lblNotificationDurationProgress.Name = "lblNotificationDurationProgress";
            this.lblNotificationDurationProgress.Size = new System.Drawing.Size(108, 13);
            this.lblNotificationDurationProgress.TabIndex = 0;
            this.lblNotificationDurationProgress.Text = "Progress Duration (s):";
            // 
            // numNotificationDurationProgress
            // 
            this.numNotificationDurationProgress.DecimalPlaces = 1;
            this.numNotificationDurationProgress.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.numNotificationDurationProgress.Location = new System.Drawing.Point(180, 88);
            this.numNotificationDurationProgress.Maximum = new decimal(new int[] {
            60,
            0,
            0,
            0});
            this.numNotificationDurationProgress.Name = "numNotificationDurationProgress";
            this.numNotificationDurationProgress.Size = new System.Drawing.Size(60, 20);
            this.numNotificationDurationProgress.TabIndex = 5;
            this.numNotificationDurationProgress.Value = new decimal(new int[] {
            6,
            0,
            0,
            0});
            // 
            // lblAchievementDateTimeFormat
            // 
            this.lblAchievementDateTimeFormat.AutoSize = true;
            this.lblAchievementDateTimeFormat.Location = new System.Drawing.Point(277, 120);
            this.lblAchievementDateTimeFormat.Name = "lblAchievementDateTimeFormat";
            this.lblAchievementDateTimeFormat.Size = new System.Drawing.Size(96, 13);
            this.lblAchievementDateTimeFormat.TabIndex = 15;
            this.lblAchievementDateTimeFormat.Text = "Date/Time Format:";
            // 
            // cmbAchievementDateTimeFormat
            // 
            this.cmbAchievementDateTimeFormat.FormattingEnabled = true;
            this.cmbAchievementDateTimeFormat.Items.AddRange(new object[] {
            "%Y/%m/%d - %H:%M:%S",
            "%Y-%m-%d %H:%M:%S",
            "%m/%d/%Y %I:%M:%S %p",
            "%d/%m/%Y %H:%M",
            "%B %d, %Y at %I:%M %p",
            "%A, %B %d, %Y",
            "%Y-%m-%d",
            "%m/%d/%Y",
            "%d/%m/%Y",
            "%H:%M:%S",
            "%I:%M:%S %p"});
            this.cmbAchievementDateTimeFormat.Location = new System.Drawing.Point(396, 117);
            this.cmbAchievementDateTimeFormat.MaxLength = 79;
            this.cmbAchievementDateTimeFormat.Name = "cmbAchievementDateTimeFormat";
            this.cmbAchievementDateTimeFormat.Size = new System.Drawing.Size(144, 21);
            this.cmbAchievementDateTimeFormat.TabIndex = 8;
            this.cmbAchievementDateTimeFormat.Text = "%Y/%m/%d - %H:%M:%S";
            // 
            // lblPosAchievement
            // 
            this.lblPosAchievement.AutoSize = true;
            this.lblPosAchievement.Location = new System.Drawing.Point(20, 30);
            this.lblPosAchievement.Name = "lblPosAchievement";
            this.lblPosAchievement.Size = new System.Drawing.Size(112, 13);
            this.lblPosAchievement.TabIndex = 0;
            this.lblPosAchievement.Text = "Achievement Position:";
            // 
            // cmbPosAchievement
            // 
            this.cmbPosAchievement.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbPosAchievement.FormattingEnabled = true;
            this.cmbPosAchievement.Items.AddRange(new object[] {
            "top_center",
            "top_left",
            "top_right",
            "bot_center",
            "bot_left",
            "bot_right"});
            this.cmbPosAchievement.Location = new System.Drawing.Point(180, 28);
            this.cmbPosAchievement.Name = "cmbPosAchievement";
            this.cmbPosAchievement.Size = new System.Drawing.Size(70, 21);
            this.cmbPosAchievement.TabIndex = 1;
            // 
            // lblIconSize
            // 
            this.lblIconSize.AutoSize = true;
            this.lblIconSize.Location = new System.Drawing.Point(20, 120);
            this.lblIconSize.Name = "lblIconSize";
            this.lblIconSize.Size = new System.Drawing.Size(54, 13);
            this.lblIconSize.TabIndex = 17;
            this.lblIconSize.Text = "Icon Size:";
            // 
            // numIconSize
            // 
            this.numIconSize.DecimalPlaces = 1;
            this.numIconSize.Location = new System.Drawing.Point(180, 118);
            this.numIconSize.Maximum = new decimal(new int[] {
            200,
            0,
            0,
            0});
            this.numIconSize.Minimum = new decimal(new int[] {
            16,
            0,
            0,
            0});
            this.numIconSize.Name = "numIconSize";
            this.numIconSize.Size = new System.Drawing.Size(60, 20);
            this.numIconSize.TabIndex = 7;
            this.numIconSize.Value = new decimal(new int[] {
            64,
            0,
            0,
            0});
            // 
            // tabMetrics
            // 
            this.tabMetrics.Controls.Add(this.grpFPSDisplay);
            this.tabMetrics.Location = new System.Drawing.Point(4, 22);
            this.tabMetrics.Name = "tabMetrics";
            this.tabMetrics.Padding = new System.Windows.Forms.Padding(12);
            this.tabMetrics.Size = new System.Drawing.Size(592, 533);
            this.tabMetrics.TabIndex = 4;
            this.tabMetrics.Text = "Metrics";
            this.tabMetrics.UseVisualStyleBackColor = true;
            // 
            // grpFPSDisplay
            // 
            this.grpFPSDisplay.Controls.Add(this.chkAlwaysShowPlaytime);
            this.grpFPSDisplay.Controls.Add(this.chkAlwaysShowFrametime);
            this.grpFPSDisplay.Controls.Add(this.chkAlwaysShowFPS);
            this.grpFPSDisplay.Controls.Add(this.chkAlwaysShowUserInfo);
            this.grpFPSDisplay.Controls.Add(this.numFpsAveragingWindow);
            this.grpFPSDisplay.Controls.Add(this.lblFpsAveragingWindow);
            this.grpFPSDisplay.Controls.Add(this.btnColorStatsText);
            this.grpFPSDisplay.Controls.Add(this.btnResetColorStatsText);
            this.grpFPSDisplay.Controls.Add(this.lblStatsText);
            this.grpFPSDisplay.Controls.Add(this.lblStatsBackground);
            this.grpFPSDisplay.Controls.Add(this.btnColorStatsBackground);
            this.grpFPSDisplay.Controls.Add(this.btnResetColorStatsBackground);
            this.grpFPSDisplay.Controls.Add(this.lblStatsPosition);
            this.grpFPSDisplay.Controls.Add(this.numStatsPosX);
            this.grpFPSDisplay.Controls.Add(this.numStatsPosY);
            this.grpFPSDisplay.Location = new System.Drawing.Point(12, 12);
            this.grpFPSDisplay.Name = "grpFPSDisplay";
            this.grpFPSDisplay.Padding = new System.Windows.Forms.Padding(12);
            this.grpFPSDisplay.Size = new System.Drawing.Size(568, 185);
            this.grpFPSDisplay.TabIndex = 0;
            this.grpFPSDisplay.TabStop = false;
            this.grpFPSDisplay.Text = "Metrics Display";
            // 
            // chkAlwaysShowPlaytime
            // 
            this.chkAlwaysShowPlaytime.AutoSize = true;
            this.chkAlwaysShowPlaytime.Location = new System.Drawing.Point(20, 90);
            this.chkAlwaysShowPlaytime.Name = "chkAlwaysShowPlaytime";
            this.chkAlwaysShowPlaytime.Size = new System.Drawing.Size(131, 17);
            this.chkAlwaysShowPlaytime.TabIndex = 8;
            this.chkAlwaysShowPlaytime.Text = "Always Show Playtime";
            this.chkAlwaysShowPlaytime.UseVisualStyleBackColor = true;
            // 
            // chkAlwaysShowFrametime
            // 
            this.chkAlwaysShowFrametime.AutoSize = true;
            this.chkAlwaysShowFrametime.Location = new System.Drawing.Point(20, 60);
            this.chkAlwaysShowFrametime.Name = "chkAlwaysShowFrametime";
            this.chkAlwaysShowFrametime.Size = new System.Drawing.Size(140, 17);
            this.chkAlwaysShowFrametime.TabIndex = 4;
            this.chkAlwaysShowFrametime.Text = "Always Show Frametime";
            this.chkAlwaysShowFrametime.UseVisualStyleBackColor = true;
            // 
            // chkAlwaysShowFPS
            // 
            this.chkAlwaysShowFPS.AutoSize = true;
            this.chkAlwaysShowFPS.Location = new System.Drawing.Point(20, 30);
            this.chkAlwaysShowFPS.Name = "chkAlwaysShowFPS";
            this.chkAlwaysShowFPS.Size = new System.Drawing.Size(112, 17);
            this.chkAlwaysShowFPS.TabIndex = 1;
            this.chkAlwaysShowFPS.Text = "Always Show FPS";
            this.chkAlwaysShowFPS.UseVisualStyleBackColor = true;
            // 
            // chkAlwaysShowUserInfo
            // 
            this.chkAlwaysShowUserInfo.AutoSize = true;
            this.chkAlwaysShowUserInfo.Location = new System.Drawing.Point(20, 120);
            this.chkAlwaysShowUserInfo.Name = "chkAlwaysShowUserInfo";
            this.chkAlwaysShowUserInfo.Size = new System.Drawing.Size(135, 17);
            this.chkAlwaysShowUserInfo.TabIndex = 12;
            this.chkAlwaysShowUserInfo.Text = "Always Show User Info";
            this.chkAlwaysShowUserInfo.UseVisualStyleBackColor = true;
            // 
            // numFpsAveragingWindow
            // 
            this.numFpsAveragingWindow.Location = new System.Drawing.Point(420, 28);
            this.numFpsAveragingWindow.Maximum = new decimal(new int[] {
            60,
            0,
            0,
            0});
            this.numFpsAveragingWindow.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numFpsAveragingWindow.Name = "numFpsAveragingWindow";
            this.numFpsAveragingWindow.Size = new System.Drawing.Size(60, 20);
            this.numFpsAveragingWindow.TabIndex = 3;
            this.numFpsAveragingWindow.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            // 
            // lblFpsAveragingWindow
            // 
            this.lblFpsAveragingWindow.AutoSize = true;
            this.lblFpsAveragingWindow.Location = new System.Drawing.Point(280, 30);
            this.lblFpsAveragingWindow.Name = "lblFpsAveragingWindow";
            this.lblFpsAveragingWindow.Size = new System.Drawing.Size(163, 13);
            this.lblFpsAveragingWindow.TabIndex = 2;
            this.lblFpsAveragingWindow.Text = "FPS Averaging Window (frames):";
            // 
            // btnColorStatsText
            // 
            this.btnColorStatsText.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(204)))), ((int)(((byte)(179)))), ((int)(((byte)(0)))));
            this.btnColorStatsText.Location = new System.Drawing.Point(420, 88);
            this.btnColorStatsText.Name = "btnColorStatsText";
            this.btnColorStatsText.Size = new System.Drawing.Size(75, 23);
            this.btnColorStatsText.TabIndex = 10;
            this.btnColorStatsText.UseVisualStyleBackColor = false;
            this.btnColorStatsText.Click += new System.EventHandler(this.OnColorStatsText_Click);
            // 
            // btnResetColorStatsText
            // 
            this.btnResetColorStatsText.Location = new System.Drawing.Point(501, 88);
            this.btnResetColorStatsText.Name = "btnResetColorStatsText";
            this.btnResetColorStatsText.Size = new System.Drawing.Size(23, 23);
            this.btnResetColorStatsText.TabIndex = 11;
            this.btnResetColorStatsText.Text = "❌";
            this.btnResetColorStatsText.UseVisualStyleBackColor = true;
            this.btnResetColorStatsText.Click += new System.EventHandler(this.OnResetColorStatsText_Click);
            // 
            // lblStatsText
            // 
            this.lblStatsText.AutoSize = true;
            this.lblStatsText.Location = new System.Drawing.Point(280, 90);
            this.lblStatsText.Name = "lblStatsText";
            this.lblStatsText.Size = new System.Drawing.Size(58, 13);
            this.lblStatsText.TabIndex = 9;
            this.lblStatsText.Text = "Text Color:";
            // 
            // lblStatsBackground
            // 
            this.lblStatsBackground.AutoSize = true;
            this.lblStatsBackground.Location = new System.Drawing.Point(280, 60);
            this.lblStatsBackground.Name = "lblStatsBackground";
            this.lblStatsBackground.Size = new System.Drawing.Size(95, 13);
            this.lblStatsBackground.TabIndex = 5;
            this.lblStatsBackground.Text = "Background Color:";
            // 
            // btnColorStatsBackground
            // 
            this.btnColorStatsBackground.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(153)))));
            this.btnColorStatsBackground.Location = new System.Drawing.Point(420, 58);
            this.btnColorStatsBackground.Name = "btnColorStatsBackground";
            this.btnColorStatsBackground.Size = new System.Drawing.Size(75, 23);
            this.btnColorStatsBackground.TabIndex = 6;
            this.btnColorStatsBackground.UseVisualStyleBackColor = false;
            this.btnColorStatsBackground.Click += new System.EventHandler(this.OnColorStatsBackground_Click);
            // 
            // btnResetColorStatsBackground
            // 
            this.btnResetColorStatsBackground.Location = new System.Drawing.Point(501, 58);
            this.btnResetColorStatsBackground.Name = "btnResetColorStatsBackground";
            this.btnResetColorStatsBackground.Size = new System.Drawing.Size(23, 23);
            this.btnResetColorStatsBackground.TabIndex = 7;
            this.btnResetColorStatsBackground.Text = "❌";
            this.btnResetColorStatsBackground.UseVisualStyleBackColor = true;
            this.btnResetColorStatsBackground.Click += new System.EventHandler(this.OnResetColorStatsBackground_Click);
            // 
            // lblStatsPosition
            // 
            this.lblStatsPosition.AutoSize = true;
            this.lblStatsPosition.Location = new System.Drawing.Point(20, 152);
            this.lblStatsPosition.Name = "lblStatsPosition";
            this.lblStatsPosition.Size = new System.Drawing.Size(103, 13);
            this.lblStatsPosition.TabIndex = 13;
            this.lblStatsPosition.Text = "Stats Position (X, Y):";
            // 
            // numStatsPosX
            // 
            this.numStatsPosX.DecimalPlaces = 2;
            this.numStatsPosX.Increment = new decimal(new int[] {
            5,
            0,
            0,
            131072});
            this.numStatsPosX.Location = new System.Drawing.Point(140, 150);
            this.numStatsPosX.Maximum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numStatsPosX.Name = "numStatsPosX";
            this.numStatsPosX.Size = new System.Drawing.Size(60, 20);
            this.numStatsPosX.TabIndex = 14;
            // 
            // numStatsPosY
            // 
            this.numStatsPosY.DecimalPlaces = 2;
            this.numStatsPosY.Increment = new decimal(new int[] {
            5,
            0,
            0,
            131072});
            this.numStatsPosY.Location = new System.Drawing.Point(210, 150);
            this.numStatsPosY.Maximum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numStatsPosY.Name = "numStatsPosY";
            this.numStatsPosY.Size = new System.Drawing.Size(60, 20);
            this.numStatsPosY.TabIndex = 15;
            // 
            // tabEmulator
            // 
            this.tabEmulator.AutoScroll = true;
            this.tabEmulator.Controls.Add(this.grpEmulatorWorkarounds);
            this.tabEmulator.Controls.Add(this.grpEmulatorStats);
            this.tabEmulator.Controls.Add(this.grpEmulatorSession);
            this.tabEmulator.Location = new System.Drawing.Point(4, 22);
            this.tabEmulator.Name = "tabEmulator";
            this.tabEmulator.Padding = new System.Windows.Forms.Padding(12);
            this.tabEmulator.Size = new System.Drawing.Size(592, 481);
            this.tabEmulator.TabIndex = 5;
            this.tabEmulator.Text = "Emulator";
            this.tabEmulator.UseVisualStyleBackColor = true;
            // 
            // grpEmulatorWorkarounds
            // 
            this.grpEmulatorWorkarounds.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpEmulatorWorkarounds.Controls.Add(this.chkUse32BitInventoryItemIds);
            this.grpEmulatorWorkarounds.Controls.Add(this.chkFreeWeekend);
            this.grpEmulatorWorkarounds.Controls.Add(this.chkEnableSteamPreownedIds);
            this.grpEmulatorWorkarounds.Controls.Add(this.chkDisableSteamoverlaygameidEnvVar);
            this.grpEmulatorWorkarounds.Controls.Add(this.chkForceSteamhttpSuccess);
            this.grpEmulatorWorkarounds.Controls.Add(this.chkAchievementsBypass);
            this.grpEmulatorWorkarounds.Location = new System.Drawing.Point(12, 324);
            this.grpEmulatorWorkarounds.Name = "grpEmulatorWorkarounds";
            this.grpEmulatorWorkarounds.Size = new System.Drawing.Size(568, 123);
            this.grpEmulatorWorkarounds.TabIndex = 2;
            this.grpEmulatorWorkarounds.TabStop = false;
            this.grpEmulatorWorkarounds.Text = "Workarounds (main::misc)";
            // 
            // chkUse32BitInventoryItemIds
            // 
            this.chkUse32BitInventoryItemIds.AutoSize = true;
            this.chkUse32BitInventoryItemIds.Location = new System.Drawing.Point(280, 72);
            this.chkUse32BitInventoryItemIds.Name = "chkUse32BitInventoryItemIds";
            this.chkUse32BitInventoryItemIds.Size = new System.Drawing.Size(161, 17);
            this.chkUse32BitInventoryItemIds.TabIndex = 5;
            this.chkUse32BitInventoryItemIds.Text = "Use 32-bit inventory item IDs";
            this.chkUse32BitInventoryItemIds.UseVisualStyleBackColor = true;
            // 
            // chkFreeWeekend
            // 
            this.chkFreeWeekend.AutoSize = true;
            this.chkFreeWeekend.Location = new System.Drawing.Point(16, 72);
            this.chkFreeWeekend.Name = "chkFreeWeekend";
            this.chkFreeWeekend.Size = new System.Drawing.Size(134, 17);
            this.chkFreeWeekend.TabIndex = 4;
            this.chkFreeWeekend.Text = "Simulate free weekend";
            this.chkFreeWeekend.UseVisualStyleBackColor = true;
            // 
            // chkEnableSteamPreownedIds
            // 
            this.chkEnableSteamPreownedIds.AutoSize = true;
            this.chkEnableSteamPreownedIds.Location = new System.Drawing.Point(280, 48);
            this.chkEnableSteamPreownedIds.Name = "chkEnableSteamPreownedIds";
            this.chkEnableSteamPreownedIds.Size = new System.Drawing.Size(161, 17);
            this.chkEnableSteamPreownedIds.TabIndex = 3;
            this.chkEnableSteamPreownedIds.Text = "Enable Steam preowned IDs";
            this.chkEnableSteamPreownedIds.UseVisualStyleBackColor = true;
            // 
            // chkDisableSteamoverlaygameidEnvVar
            // 
            this.chkDisableSteamoverlaygameidEnvVar.AutoSize = true;
            this.chkDisableSteamoverlaygameidEnvVar.Location = new System.Drawing.Point(16, 48);
            this.chkDisableSteamoverlaygameidEnvVar.Name = "chkDisableSteamoverlaygameidEnvVar";
            this.chkDisableSteamoverlaygameidEnvVar.Size = new System.Drawing.Size(206, 17);
            this.chkDisableSteamoverlaygameidEnvVar.TabIndex = 2;
            this.chkDisableSteamoverlaygameidEnvVar.Text = "Disable SteamOverlayGameId env var";
            this.chkDisableSteamoverlaygameidEnvVar.UseVisualStyleBackColor = true;
            // 
            // chkForceSteamhttpSuccess
            // 
            this.chkForceSteamhttpSuccess.AutoSize = true;
            this.chkForceSteamhttpSuccess.Location = new System.Drawing.Point(280, 24);
            this.chkForceSteamhttpSuccess.Name = "chkForceSteamhttpSuccess";
            this.chkForceSteamhttpSuccess.Size = new System.Drawing.Size(160, 17);
            this.chkForceSteamhttpSuccess.TabIndex = 1;
            this.chkForceSteamhttpSuccess.Text = "Force Steam HTTP success";
            this.chkForceSteamhttpSuccess.UseVisualStyleBackColor = true;
            // 
            // chkAchievementsBypass
            // 
            this.chkAchievementsBypass.AutoSize = true;
            this.chkAchievementsBypass.Location = new System.Drawing.Point(16, 24);
            this.chkAchievementsBypass.Name = "chkAchievementsBypass";
            this.chkAchievementsBypass.Size = new System.Drawing.Size(129, 17);
            this.chkAchievementsBypass.TabIndex = 0;
            this.chkAchievementsBypass.Text = "Achievements bypass";
            this.chkAchievementsBypass.UseVisualStyleBackColor = true;
            // 
            // grpEmulatorStats
            // 
            this.grpEmulatorStats.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpEmulatorStats.Controls.Add(this.btnBrowseSteamGameStatsReportsDir);
            this.grpEmulatorStats.Controls.Add(this.txtSteamGameStatsReportsDir);
            this.grpEmulatorStats.Controls.Add(this.lblStatsReportsFolder);
            this.grpEmulatorStats.Controls.Add(this.numIconsPerIteration);
            this.grpEmulatorStats.Controls.Add(this.lblIconsPerIteration);
            this.grpEmulatorStats.Controls.Add(this.chkRecordPlaytime);
            this.grpEmulatorStats.Controls.Add(this.chkSaveOnlyHigherStatAchievementProgress);
            this.grpEmulatorStats.Controls.Add(this.chkStatAchievementProgressFunctionality);
            this.grpEmulatorStats.Controls.Add(this.chkAllowUnknownStats);
            this.grpEmulatorStats.Controls.Add(this.chkDisableLeaderboardsCreateUnknown);
            this.grpEmulatorStats.Location = new System.Drawing.Point(12, 118);
            this.grpEmulatorStats.Name = "grpEmulatorStats";
            this.grpEmulatorStats.Size = new System.Drawing.Size(568, 200);
            this.grpEmulatorStats.TabIndex = 1;
            this.grpEmulatorStats.TabStop = false;
            this.grpEmulatorStats.Text = "Stats (main::stats)";
            // 
            // btnBrowseSteamGameStatsReportsDir
            // 
            this.btnBrowseSteamGameStatsReportsDir.Location = new System.Drawing.Point(476, 143);
            this.btnBrowseSteamGameStatsReportsDir.Name = "btnBrowseSteamGameStatsReportsDir";
            this.btnBrowseSteamGameStatsReportsDir.Size = new System.Drawing.Size(75, 23);
            this.btnBrowseSteamGameStatsReportsDir.TabIndex = 9;
            this.btnBrowseSteamGameStatsReportsDir.Text = "Browse";
            this.btnBrowseSteamGameStatsReportsDir.UseVisualStyleBackColor = true;
            this.btnBrowseSteamGameStatsReportsDir.Click += new System.EventHandler(this.OnBrowseEmulatorSteamGameStatsReportsDir_Click);
            // 
            // txtSteamGameStatsReportsDir
            // 
            this.txtSteamGameStatsReportsDir.Location = new System.Drawing.Point(340, 145);
            this.txtSteamGameStatsReportsDir.Name = "txtSteamGameStatsReportsDir";
            this.txtSteamGameStatsReportsDir.Size = new System.Drawing.Size(130, 20);
            this.txtSteamGameStatsReportsDir.TabIndex = 8;
            // 
            // lblStatsReportsFolder
            // 
            this.lblStatsReportsFolder.AutoSize = true;
            this.lblStatsReportsFolder.Location = new System.Drawing.Point(220, 148);
            this.lblStatsReportsFolder.Name = "lblStatsReportsFolder";
            this.lblStatsReportsFolder.Size = new System.Drawing.Size(98, 13);
            this.lblStatsReportsFolder.TabIndex = 7;
            this.lblStatsReportsFolder.Text = "Stats reports folder:";
            // 
            // numIconsPerIteration
            // 
            this.numIconsPerIteration.Location = new System.Drawing.Point(130, 145);
            this.numIconsPerIteration.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.numIconsPerIteration.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numIconsPerIteration.Name = "numIconsPerIteration";
            this.numIconsPerIteration.Size = new System.Drawing.Size(60, 20);
            this.numIconsPerIteration.TabIndex = 6;
            this.numIconsPerIteration.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            // 
            // lblIconsPerIteration
            // 
            this.lblIconsPerIteration.AutoSize = true;
            this.lblIconsPerIteration.Location = new System.Drawing.Point(16, 148);
            this.lblIconsPerIteration.Name = "lblIconsPerIteration";
            this.lblIconsPerIteration.Size = new System.Drawing.Size(94, 13);
            this.lblIconsPerIteration.TabIndex = 5;
            this.lblIconsPerIteration.Text = "Icons per iteration:";
            // 
            // chkRecordPlaytime
            // 
            this.chkRecordPlaytime.AutoSize = true;
            this.chkRecordPlaytime.Location = new System.Drawing.Point(16, 120);
            this.chkRecordPlaytime.Name = "chkRecordPlaytime";
            this.chkRecordPlaytime.Size = new System.Drawing.Size(102, 17);
            this.chkRecordPlaytime.TabIndex = 4;
            this.chkRecordPlaytime.Text = "Record playtime";
            this.chkRecordPlaytime.UseVisualStyleBackColor = true;
            // 
            // chkSaveOnlyHigherStatAchievementProgress
            // 
            this.chkSaveOnlyHigherStatAchievementProgress.AutoSize = true;
            this.chkSaveOnlyHigherStatAchievementProgress.Location = new System.Drawing.Point(16, 96);
            this.chkSaveOnlyHigherStatAchievementProgress.Name = "chkSaveOnlyHigherStatAchievementProgress";
            this.chkSaveOnlyHigherStatAchievementProgress.Size = new System.Drawing.Size(168, 17);
            this.chkSaveOnlyHigherStatAchievementProgress.TabIndex = 3;
            this.chkSaveOnlyHigherStatAchievementProgress.Text = "Save only higher stat progress";
            this.chkSaveOnlyHigherStatAchievementProgress.UseVisualStyleBackColor = true;
            // 
            // chkStatAchievementProgressFunctionality
            // 
            this.chkStatAchievementProgressFunctionality.AutoSize = true;
            this.chkStatAchievementProgressFunctionality.Location = new System.Drawing.Point(16, 72);
            this.chkStatAchievementProgressFunctionality.Name = "chkStatAchievementProgressFunctionality";
            this.chkStatAchievementProgressFunctionality.Size = new System.Drawing.Size(152, 17);
            this.chkStatAchievementProgressFunctionality.TabIndex = 2;
            this.chkStatAchievementProgressFunctionality.Text = "Stat achievement progress";
            this.chkStatAchievementProgressFunctionality.UseVisualStyleBackColor = true;
            // 
            // chkAllowUnknownStats
            // 
            this.chkAllowUnknownStats.AutoSize = true;
            this.chkAllowUnknownStats.Location = new System.Drawing.Point(16, 48);
            this.chkAllowUnknownStats.Name = "chkAllowUnknownStats";
            this.chkAllowUnknownStats.Size = new System.Drawing.Size(123, 17);
            this.chkAllowUnknownStats.TabIndex = 1;
            this.chkAllowUnknownStats.Text = "Allow unknown stats";
            this.chkAllowUnknownStats.UseVisualStyleBackColor = true;
            // 
            // chkDisableLeaderboardsCreateUnknown
            // 
            this.chkDisableLeaderboardsCreateUnknown.AutoSize = true;
            this.chkDisableLeaderboardsCreateUnknown.Location = new System.Drawing.Point(16, 24);
            this.chkDisableLeaderboardsCreateUnknown.Name = "chkDisableLeaderboardsCreateUnknown";
            this.chkDisableLeaderboardsCreateUnknown.Size = new System.Drawing.Size(205, 17);
            this.chkDisableLeaderboardsCreateUnknown.TabIndex = 0;
            this.chkDisableLeaderboardsCreateUnknown.Text = "Disable leaderboards create unknown";
            this.chkDisableLeaderboardsCreateUnknown.UseVisualStyleBackColor = true;
            // 
            // grpEmulatorSession
            // 
            this.grpEmulatorSession.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpEmulatorSession.Controls.Add(this.chkSteamDeck);
            this.grpEmulatorSession.Controls.Add(this.chkEnableVoiceChat);
            this.grpEmulatorSession.Controls.Add(this.chkEnableAccountAvatar);
            this.grpEmulatorSession.Controls.Add(this.chkGameCoordinatorToken);
            this.grpEmulatorSession.Controls.Add(this.chkModernAuthTicket);
            this.grpEmulatorSession.Location = new System.Drawing.Point(12, 12);
            this.grpEmulatorSession.Name = "grpEmulatorSession";
            this.grpEmulatorSession.Size = new System.Drawing.Size(568, 100);
            this.grpEmulatorSession.TabIndex = 0;
            this.grpEmulatorSession.TabStop = false;
            this.grpEmulatorSession.Text = "Session and auth (main::general)";
            // 
            // chkSteamDeck
            // 
            this.chkSteamDeck.AutoSize = true;
            this.chkSteamDeck.Location = new System.Drawing.Point(280, 48);
            this.chkSteamDeck.Name = "chkSteamDeck";
            this.chkSteamDeck.Size = new System.Drawing.Size(85, 17);
            this.chkSteamDeck.TabIndex = 4;
            this.chkSteamDeck.Text = "Steam Deck";
            this.chkSteamDeck.UseVisualStyleBackColor = true;
            // 
            // chkEnableVoiceChat
            // 
            this.chkEnableVoiceChat.AutoSize = true;
            this.chkEnableVoiceChat.Location = new System.Drawing.Point(280, 24);
            this.chkEnableVoiceChat.Name = "chkEnableVoiceChat";
            this.chkEnableVoiceChat.Size = new System.Drawing.Size(112, 17);
            this.chkEnableVoiceChat.TabIndex = 3;
            this.chkEnableVoiceChat.Text = "Enable voice chat";
            this.chkEnableVoiceChat.UseVisualStyleBackColor = true;
            // 
            // chkEnableAccountAvatar
            // 
            this.chkEnableAccountAvatar.AutoSize = true;
            this.chkEnableAccountAvatar.Location = new System.Drawing.Point(16, 72);
            this.chkEnableAccountAvatar.Name = "chkEnableAccountAvatar";
            this.chkEnableAccountAvatar.Size = new System.Drawing.Size(134, 17);
            this.chkEnableAccountAvatar.TabIndex = 2;
            this.chkEnableAccountAvatar.Text = "Enable account avatar";
            this.chkEnableAccountAvatar.UseVisualStyleBackColor = true;
            // 
            // chkGameCoordinatorToken
            // 
            this.chkGameCoordinatorToken.AutoSize = true;
            this.chkGameCoordinatorToken.Location = new System.Drawing.Point(16, 48);
            this.chkGameCoordinatorToken.Name = "chkGameCoordinatorToken";
            this.chkGameCoordinatorToken.Size = new System.Drawing.Size(141, 17);
            this.chkGameCoordinatorToken.TabIndex = 1;
            this.chkGameCoordinatorToken.Text = "Game Coordinator token";
            this.chkGameCoordinatorToken.UseVisualStyleBackColor = true;
            // 
            // chkModernAuthTicket
            // 
            this.chkModernAuthTicket.AutoSize = true;
            this.chkModernAuthTicket.Location = new System.Drawing.Point(16, 24);
            this.chkModernAuthTicket.Name = "chkModernAuthTicket";
            this.chkModernAuthTicket.Size = new System.Drawing.Size(115, 17);
            this.chkModernAuthTicket.TabIndex = 0;
            this.chkModernAuthTicket.Text = "Modern auth ticket";
            this.chkModernAuthTicket.UseVisualStyleBackColor = true;
            // 
            // btnSave
            // 
            this.btnSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSave.Location = new System.Drawing.Point(432, 531);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(75, 23);
            this.btnSave.TabIndex = 1;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.OnSave_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(513, 531);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.OnCancel_Click);
            // 
            // SettingsForm
            // 
            this.AcceptButton = this.btnSave;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(600, 565);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.tabControl);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(600, 520);
            this.Name = "SettingsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Global Settings";
            this.Load += new System.EventHandler(this.SettingsForm_Load);
            this.tabControl.ResumeLayout(false);
            this.tabUserAccount.ResumeLayout(false);
            this.grpSteamWebApi.ResumeLayout(false);
            this.grpSteamWebApi.PerformLayout();
            this.grpAccountIdentity.ResumeLayout(false);
            this.grpAccountIdentity.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picAvatar)).EndInit();
            this.tabSaveManagement.ResumeLayout(false);
            this.grpSaveLocation.ResumeLayout(false);
            this.grpSaveLocation.PerformLayout();
            this.tabOverlay.ResumeLayout(false);
            this.grpAdvancedOverlay.ResumeLayout(false);
            this.grpAdvancedOverlay.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numRendererDetectorTimeout)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numHookDelay)).EndInit();
            this.grpFontSettings.ResumeLayout(false);
            this.grpFontSettings.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numFontSize)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numFontSpacingY)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numFontSpacingX)).EndInit();
            this.grpOverlayAppearance.ResumeLayout(false);
            this.grpOverlayAppearance.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numNotificationMarginY)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numNotificationMarginX)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numNotificationRounding)).EndInit();
            this.tabNotifications.ResumeLayout(false);
            this.grpSoundSettings.ResumeLayout(false);
            this.grpSoundSettings.PerformLayout();
            this.grpNotificationSettings.ResumeLayout(false);
            this.grpNotificationSettings.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numNotificationDurationInvitation)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numNotificationDurationChat)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numNotificationAnimation)).EndInit();
            this.grpAchievements.ResumeLayout(false);
            this.grpAchievements.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numNotificationDurationAchievement)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numNotificationDurationProgress)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numIconSize)).EndInit();
            this.tabMetrics.ResumeLayout(false);
            this.grpFPSDisplay.ResumeLayout(false);
            this.grpFPSDisplay.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numFpsAveragingWindow)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numStatsPosX)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numStatsPosY)).EndInit();
            this.tabEmulator.ResumeLayout(false);
            this.grpEmulatorWorkarounds.ResumeLayout(false);
            this.grpEmulatorWorkarounds.PerformLayout();
            this.grpEmulatorStats.ResumeLayout(false);
            this.grpEmulatorStats.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numIconsPerIteration)).EndInit();
            this.grpEmulatorSession.ResumeLayout(false);
            this.grpEmulatorSession.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabUserAccount;
        private System.Windows.Forms.TabPage tabNotifications;
        private System.Windows.Forms.TabPage tabOverlay;
        private System.Windows.Forms.TabPage tabMetrics;
        private System.Windows.Forms.TabPage tabEmulator;
        private System.Windows.Forms.GroupBox grpEmulatorSession;
        private System.Windows.Forms.CheckBox chkModernAuthTicket;
        private System.Windows.Forms.CheckBox chkGameCoordinatorToken;
        private System.Windows.Forms.CheckBox chkEnableAccountAvatar;
        private System.Windows.Forms.CheckBox chkEnableVoiceChat;
        private System.Windows.Forms.CheckBox chkSteamDeck;
        private System.Windows.Forms.GroupBox grpEmulatorStats;
        private System.Windows.Forms.CheckBox chkDisableLeaderboardsCreateUnknown;
        private System.Windows.Forms.CheckBox chkAllowUnknownStats;
        private System.Windows.Forms.CheckBox chkStatAchievementProgressFunctionality;
        private System.Windows.Forms.CheckBox chkSaveOnlyHigherStatAchievementProgress;
        private System.Windows.Forms.CheckBox chkRecordPlaytime;
        private System.Windows.Forms.Label lblIconsPerIteration;
        private System.Windows.Forms.NumericUpDown numIconsPerIteration;
        private System.Windows.Forms.Label lblStatsReportsFolder;
        private System.Windows.Forms.TextBox txtSteamGameStatsReportsDir;
        private System.Windows.Forms.Button btnBrowseSteamGameStatsReportsDir;
        private System.Windows.Forms.GroupBox grpEmulatorWorkarounds;
        private System.Windows.Forms.CheckBox chkAchievementsBypass;
        private System.Windows.Forms.CheckBox chkForceSteamhttpSuccess;
        private System.Windows.Forms.CheckBox chkDisableSteamoverlaygameidEnvVar;
        private System.Windows.Forms.CheckBox chkEnableSteamPreownedIds;
        private System.Windows.Forms.CheckBox chkFreeWeekend;
        private System.Windows.Forms.CheckBox chkUse32BitInventoryItemIds;

        private System.Windows.Forms.TabPage tabSaveManagement;
        private System.Windows.Forms.GroupBox grpAccountIdentity;
        private System.Windows.Forms.TextBox txtUsername;
        private System.Windows.Forms.Label lblUsername;
        private System.Windows.Forms.ComboBox txtSteamID;
        private System.Windows.Forms.Label lblSteamID;
        private System.Windows.Forms.Button btnRandomizeSteamID;
        private System.Windows.Forms.ComboBox cmbLanguage;
        private System.Windows.Forms.Label lblLanguage;
        private System.Windows.Forms.ComboBox cmbCountry;
        private System.Windows.Forms.Label lblCountry;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.ToolTip toolTip;
        private System.Windows.Forms.Button btnClearAvatar;
        private System.Windows.Forms.Button btnSetAvatar;
        private System.Windows.Forms.GroupBox grpSaveLocation;
        private System.Windows.Forms.TextBox txtSavesFolderName;
        private System.Windows.Forms.Label lblSavesFolderName;
        private System.Windows.Forms.Button btnOpenSaveFolder;
        private System.Windows.Forms.Button btnBrowseSavePath;
        private System.Windows.Forms.TextBox txtLocalSavePath;
        private System.Windows.Forms.Label lblLocalSavePath;
        private System.Windows.Forms.ComboBox cmbSaveLocation;
        private System.Windows.Forms.Label lblSaveLocation;
        private System.Windows.Forms.LinkLabel lnkSteamWebApiKey;
        private System.Windows.Forms.GroupBox grpNotificationSettings;
        private System.Windows.Forms.ComboBox cmbAchievementDateTimeFormat;
        private System.Windows.Forms.Label lblAchievementDateTimeFormat;
        private System.Windows.Forms.NumericUpDown numNotificationMarginY;
        private System.Windows.Forms.NumericUpDown numNotificationMarginX;
        private System.Windows.Forms.Label lblNotificationMargin;
        private System.Windows.Forms.NumericUpDown numNotificationRounding;
        private System.Windows.Forms.Label lblNotificationRounding;
        private System.Windows.Forms.NumericUpDown numNotificationAnimation;
        private System.Windows.Forms.Label lblNotificationAnimation;
        private System.Windows.Forms.NumericUpDown numNotificationDurationChat;
        private System.Windows.Forms.NumericUpDown numNotificationDurationInvitation;
        private System.Windows.Forms.NumericUpDown numNotificationDurationAchievement;
        private System.Windows.Forms.NumericUpDown numNotificationDurationProgress;
        private System.Windows.Forms.Label lblNotificationDurationChat;
        private System.Windows.Forms.Label lblNotificationDurationInvitation;
        private System.Windows.Forms.Label lblNotificationDurationAchievement;
        private System.Windows.Forms.Label lblNotificationDurationProgress;
        private System.Windows.Forms.GroupBox grpOverlayAppearance;
        private System.Windows.Forms.Button btnColorActiveElements;
        private System.Windows.Forms.Label lblActiveElements;
        private System.Windows.Forms.Button btnColorHoveredElements;
        private System.Windows.Forms.Label lblHoveredElements;
        private System.Windows.Forms.Button btnColorElements;
        private System.Windows.Forms.Label lblElements;
        private System.Windows.Forms.Button btnColorBackground;
        private System.Windows.Forms.Label lblBackground;
        private System.Windows.Forms.Button btnColorNotification;
        private System.Windows.Forms.Label lblNotification;
        private System.Windows.Forms.Button btnResetColorBackground;
        private System.Windows.Forms.Button btnResetColorElements;
        private System.Windows.Forms.Button btnResetColorHoveredElements;
        private System.Windows.Forms.Button btnResetColorActiveElements;
        private System.Windows.Forms.Button btnResetColorStatsBackground;
        private System.Windows.Forms.Button btnResetColorStatsText;
        private System.Windows.Forms.GroupBox grpAchievements;
        private System.Windows.Forms.NumericUpDown numStatsPosY;
        private System.Windows.Forms.NumericUpDown numStatsPosX;
        private System.Windows.Forms.Label lblStatsPosition;
        private System.Windows.Forms.ComboBox cmbPosChatMsg;
        private System.Windows.Forms.Label lblPosChatMsg;
        private System.Windows.Forms.ComboBox cmbPosInvitation;
        private System.Windows.Forms.Label lblPosInvitation;
        private System.Windows.Forms.ComboBox cmbPosAchievement;
        private System.Windows.Forms.Label lblPosAchievement;
        private System.Windows.Forms.Button btnOpenControllerFolder;
        private System.Windows.Forms.Button btnOpenFontsFolder;
        private System.Windows.Forms.Button btnOpenSoundsFolder;
        private System.Windows.Forms.PictureBox picAvatar;
        private System.Windows.Forms.GroupBox grpSteamWebApi;
        private System.Windows.Forms.Button btnRemoveApiKey;
        private System.Windows.Forms.TextBox txtSteamWebApiKey;
        private System.Windows.Forms.Label lblSteamWebApiKey;
        private System.Windows.Forms.CheckBox chkDisableWarningLocalSave;
        private System.Windows.Forms.NumericUpDown numIconSize;
        private System.Windows.Forms.Label lblIconSize;
        private System.Windows.Forms.CheckBox chkDisableAchievementProgress;
        private System.Windows.Forms.CheckBox chkDisableAchievementNotification;
        private System.Windows.Forms.CheckBox chkDisableFriendNotification;
        private System.Windows.Forms.CheckBox chkUploadAchievementsToGPU;
        private System.Windows.Forms.GroupBox grpFontSettings;
        private System.Windows.Forms.Button btnBrowseFont;
        private System.Windows.Forms.ComboBox cmbFontOverride;
        private System.Windows.Forms.Label lblFontOverride;
        private System.Windows.Forms.NumericUpDown numFontSize;
        private System.Windows.Forms.Label lblFontSize;
        private System.Windows.Forms.NumericUpDown numFontSpacingY;
        private System.Windows.Forms.NumericUpDown numFontSpacingX;
        private System.Windows.Forms.Label lblFontSpacing;
        private System.Windows.Forms.GroupBox grpFPSDisplay;
        private System.Windows.Forms.CheckBox chkAlwaysShowPlaytime;
        private System.Windows.Forms.CheckBox chkAlwaysShowFrametime;
        private System.Windows.Forms.CheckBox chkAlwaysShowFPS;
        private System.Windows.Forms.CheckBox chkAlwaysShowUserInfo;
        private System.Windows.Forms.NumericUpDown numFpsAveragingWindow;
        private System.Windows.Forms.Label lblFpsAveragingWindow;
        private System.Windows.Forms.Button btnColorStatsText;
        private System.Windows.Forms.Label lblStatsText;
        private System.Windows.Forms.Label lblStatsBackground;
        private System.Windows.Forms.Button btnColorStatsBackground;
        private System.Windows.Forms.GroupBox grpAdvancedOverlay;
        private System.Windows.Forms.NumericUpDown numRendererDetectorTimeout;
        private System.Windows.Forms.CheckBox chkDisableWarningBadAppId;
        private System.Windows.Forms.Label lblRendererDetectorTimeout;
        private System.Windows.Forms.NumericUpDown numHookDelay;
        private System.Windows.Forms.Label lblHookDelay;
        private System.Windows.Forms.CheckBox chkEnableExperimentalOverlay;
        private System.Windows.Forms.CheckBox chkDisableWarningAny;
        private System.Windows.Forms.GroupBox grpSoundSettings;
        private System.Windows.Forms.ComboBox cmbSound2File;
        private System.Windows.Forms.ComboBox cmbSound1File;
        private System.Windows.Forms.Button btnSound2Browse;
        private System.Windows.Forms.Button btnSound2Default;
        private System.Windows.Forms.Button btnSound2PlayStop;
        private System.Windows.Forms.Label lblSound2;
        private System.Windows.Forms.Button btnSound1Browse;
        private System.Windows.Forms.Button btnSound1Default;
        private System.Windows.Forms.Button btnSound1PlayStop;
        private System.Windows.Forms.Label lblSound1;
        private System.Windows.Forms.Label lblApiKeyValidation;
        private System.Windows.Forms.Button btnResetNotificationColor;
        private System.Windows.Forms.Button btnRemoveSteamIdProfile;
        private System.Windows.Forms.Label lblProfileHint;
        private System.Windows.Forms.Label lblApiKeyHint;
    }
}
