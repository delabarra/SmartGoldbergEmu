// SmartGoldbergEmu — see LICENSE in the repository root.
using System.Windows.Forms;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Forms
{
    partial class GameSettingsForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GameSettingsForm));
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabGameInfo = new System.Windows.Forms.TabPage();
            this.grpPaths = new System.Windows.Forms.GroupBox();
            this.lblHintGameFolder = new System.Windows.Forms.Label();
            this.btnBrowseWorkingDirectory = new System.Windows.Forms.Button();
            this.txtWorkingDirectory = new System.Windows.Forms.TextBox();
            this.lblWorkingDirectory = new System.Windows.Forms.Label();
            this.btnClearCustomIcon = new System.Windows.Forms.Button();
            this.txtLaunchParameters = new System.Windows.Forms.TextBox();
            this.lnkLauncherOptionsSteamDb = new System.Windows.Forms.LinkLabel();
            this.lblCustomIcon = new System.Windows.Forms.Label();
            this.btnBrowseCustomIcon = new System.Windows.Forms.Button();
            this.lblLaunchParameters = new System.Windows.Forms.Label();
            this.txtCustomIcon = new System.Windows.Forms.TextBox();
            this.btnBrowseGameExecutable = new System.Windows.Forms.Button();
            this.txtGameExecutable = new System.Windows.Forms.TextBox();
            this.lblGameExecutable = new System.Windows.Forms.Label();
            this.btnBrowseGameFolder = new System.Windows.Forms.Button();
            this.lnkSteamCmdLineOptionsValveWiki = new System.Windows.Forms.LinkLabel();
            this.txtGameFolder = new System.Windows.Forms.TextBox();
            this.lblGameFolder = new System.Windows.Forms.Label();
            this.txtBetaBranchName = new System.Windows.Forms.TextBox();
            this.lblBetaBranchName = new System.Windows.Forms.Label();
            this.chkBetaBranch = new System.Windows.Forms.CheckBox();
            this.grpSteamLaunchOptions = new System.Windows.Forms.GroupBox();
            this.lblSteamLaunchOptions = new System.Windows.Forms.Label();
            this.cmbSteamLaunchOptions = new System.Windows.Forms.ComboBox();
            this.btnRemoveUserLaunchOption = new System.Windows.Forms.Button();
            this.lblUserLaunchOptionName = new System.Windows.Forms.Label();
            this.txtUserLaunchOptionName = new System.Windows.Forms.TextBox();
            this.btnSaveUserLaunchOption = new System.Windows.Forms.Button();
            this.grpBasicInfo = new System.Windows.Forms.GroupBox();
            this.lblAppID = new System.Windows.Forms.Label();
            this.txtAppID = new System.Windows.Forms.TextBox();
            this.btnLookupAppID = new System.Windows.Forms.Button();
            this.lblGameName = new System.Windows.Forms.Label();
            this.txtGameName = new System.Windows.Forms.TextBox();
            this.btnLookupGameName = new System.Windows.Forms.Button();
            this.lblSteamAPIStatus = new System.Windows.Forms.Label();
            this.lblSteamAPIStatusX32Value = new System.Windows.Forms.Label();
            this.lblSteamAPIStatusX64Value = new System.Windows.Forms.Label();
            this.lblSteamApiHealth = new System.Windows.Forms.Label();
            this.lblSteamApiHealthValue = new System.Windows.Forms.Label();
            this.lblSteamApiHealthNote = new System.Windows.Forms.Label();
            this.lblPatchOpMessage = new System.Windows.Forms.Label();
            this.lblLaunchMode = new System.Windows.Forms.Label();
            this.rdoLaunchSteamClient = new System.Windows.Forms.RadioButton();
            this.rdoLaunchExperimentalMode = new System.Windows.Forms.RadioButton();
            this.rdoLaunchSteamDll = new System.Windows.Forms.RadioButton();
            this.rdoLaunchNoEmulation = new System.Windows.Forms.RadioButton();
            this.btnRestoreDlls = new System.Windows.Forms.Button();
            this.tabDLCContent = new System.Windows.Forms.TabPage();
            this.grpSubscribedGroups = new System.Windows.Forms.GroupBox();
            this.lblSubscribedGroups = new System.Windows.Forms.Label();
            this.lblSubscribedGroupsClans = new System.Windows.Forms.Label();
            this.lstSubscribedGroups = new System.Windows.Forms.ListBox();
            this.lstSubscribedGroupsClans = new System.Windows.Forms.ListBox();
            this.txtSubscribedGroupIdEntry = new System.Windows.Forms.TextBox();
            this.btnAddSubscribedGroup = new System.Windows.Forms.Button();
            this.btnRemoveSubscribedGroup = new System.Windows.Forms.Button();
            this.txtSubscribedGroupClanEntry = new System.Windows.Forms.TextBox();
            this.btnAddSubscribedGroupClan = new System.Windows.Forms.Button();
            this.btnRemoveSubscribedGroupClan = new System.Windows.Forms.Button();
            this.grpDLCManagement = new System.Windows.Forms.GroupBox();
            this.btnFindDLCs = new System.Windows.Forms.Button();
            this.txtDLCList = new System.Windows.Forms.TextBox();
            this.chkUnlockAllDLC = new System.Windows.Forms.CheckBox();
            this.tabOtherSettings = new System.Windows.Forms.TabPage();
            this.grpAuthenticationSettings = new System.Windows.Forms.GroupBox();
            this.txtForceIpCountry = new System.Windows.Forms.TextBox();
            this.lblForceIpCountry = new System.Windows.Forms.Label();
            this.txtClanTag = new System.Windows.Forms.TextBox();
            this.lblClanTag = new System.Windows.Forms.Label();
            this.txtForceSteamId = new System.Windows.Forms.TextBox();
            this.lblForceSteamId = new System.Windows.Forms.Label();
            this.cmbForceLanguage = new System.Windows.Forms.ComboBox();
            this.lblForceLanguage = new System.Windows.Forms.Label();
            this.txtForceAccountName = new System.Windows.Forms.TextBox();
            this.lblForceAccountName = new System.Windows.Forms.Label();
            this.grpAdvancedAuth = new System.Windows.Forms.GroupBox();
            this.numAltSteamIdCount = new System.Windows.Forms.NumericUpDown();
            this.lblAltSteamIdCount = new System.Windows.Forms.Label();
            this.txtAltSteamId = new System.Windows.Forms.TextBox();
            this.lblAltSteamId = new System.Windows.Forms.Label();
            this.txtUserTicket = new System.Windows.Forms.TextBox();
            this.lblUserTicket = new System.Windows.Forms.Label();
            this.tabStatsAchievements = new System.Windows.Forms.TabPage();
            this.grpAchievementsFile = new System.Windows.Forms.GroupBox();
            this.lstAchievementsPreview = new System.Windows.Forms.ListView();
            this.colAchievementName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colAchievementDescription = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.imgAchievementsPreview = new System.Windows.Forms.ImageList(this.components);
            this.lblAchievementsPreview = new System.Windows.Forms.Label();
            this.lblAchievementsFilter = new System.Windows.Forms.Label();
            this.txtAchievementsFilter = new System.Windows.Forms.TextBox();
            this.btnRefreshAchievements = new System.Windows.Forms.Button();
            this.grpCustomStats = new System.Windows.Forms.GroupBox();
            this.pnlStatsDisplayScroll = new System.Windows.Forms.Panel();
            this.lblCustomStatsDisplay = new System.Windows.Forms.Label();
            this.btnRefreshStats = new System.Windows.Forms.Button();
            this.grpStatsSettings = new System.Windows.Forms.GroupBox();
            this.btnBrowseSteamGameStatsReportsDir = new System.Windows.Forms.Button();
            this.txtSteamGameStatsReportsDir = new System.Windows.Forms.TextBox();
            this.lblSteamGameStatsReportsDir = new System.Windows.Forms.Label();
            this.chkStats = new System.Windows.Forms.CheckBox();
            this.chkRecordPlaytime = new System.Windows.Forms.CheckBox();
            this.chkDisableLeaderboardsCreateUnknown = new System.Windows.Forms.CheckBox();
            this.chkAllowUnknownStats = new System.Windows.Forms.CheckBox();
            this.grpOtherStatsSettings = new System.Windows.Forms.GroupBox();
            this.lblIconsPerIteration = new System.Windows.Forms.Label();
            this.numIconsPerIteration = new System.Windows.Forms.NumericUpDown();
            this.chkAchievementsBypass = new System.Windows.Forms.CheckBox();
            this.chkSaveOnlyHigherStatAchievementProgress = new System.Windows.Forms.CheckBox();
            this.chkStatAchievementProgressFunctionality = new System.Windows.Forms.CheckBox();
            this.tabServerMultiplayer = new System.Windows.Forms.TabPage();
            this.grpNetworkSettings = new System.Windows.Forms.GroupBox();
            this.numOldP2PPacketSharingMode = new System.Windows.Forms.NumericUpDown();
            this.lblOldP2PPacketSharingMode = new System.Windows.Forms.Label();
            this.chkOffline = new System.Windows.Forms.CheckBox();
            this.chkDisableNetworking = new System.Windows.Forms.CheckBox();
            this.chkShareLeaderboardsOverNetwork = new System.Windows.Forms.CheckBox();
            this.numForcePort = new System.Windows.Forms.NumericUpDown();
            this.chkDisableLanOnly = new System.Windows.Forms.CheckBox();
            this.lblForcePort = new System.Windows.Forms.Label();
            this.chkDisableSharingStatsWithGameserver = new System.Windows.Forms.CheckBox();
            this.chkDisableSourceQuery = new System.Windows.Forms.CheckBox();
            this.chkDisableLobbyCreation = new System.Windows.Forms.CheckBox();
            this.chkDownloadSteamhttpRequests = new System.Windows.Forms.CheckBox();
            this.grpMatchmakingSettings = new System.Windows.Forms.GroupBox();
            this.chkBlockUnknownClients = new System.Windows.Forms.CheckBox();
            this.chkMatchmaking = new System.Windows.Forms.CheckBox();
            this.chkImmediateGameserverStats = new System.Windows.Forms.CheckBox();
            this.chkMatchmakingServerListActualType = new System.Windows.Forms.CheckBox();
            this.chkMatchmakingServerDetailsViaSourceQuery = new System.Windows.Forms.CheckBox();
            this.tabInventory = new System.Windows.Forms.TabPage();
            this.grpInventoryEditor = new System.Windows.Forms.GroupBox();
            this.lblInventoryHint = new System.Windows.Forms.Label();
            this.pnlInventoryButtons = new System.Windows.Forms.Panel();
            this.chkUse32BitInventoryItemIds = new System.Windows.Forms.CheckBox();
            this.btnReloadInventoryFromDisk = new System.Windows.Forms.Button();
            this.lstInventoryItems = new System.Windows.Forms.ListView();
            this.colInventoryItemDefId = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colInventoryDisplayName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colInventoryQuantity = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colInventoryType = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colInventoryFiller = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.txtInventoryRaw = new System.Windows.Forms.TextBox();
            this.tabMods = new System.Windows.Forms.TabPage();
            this.grpMods = new System.Windows.Forms.GroupBox();
            this.lblModsHint = new System.Windows.Forms.Label();
            this.pnlModsToolbar = new System.Windows.Forms.Panel();
            this.btnCopyFoldersToMods = new System.Windows.Forms.Button();
            this.btnCopyFilesToMods = new System.Windows.Forms.Button();
            this.btnOpenModsFolder = new System.Windows.Forms.Button();
            this.lstModsSummary = new System.Windows.Forms.ListView();
            this.colModsSummaryId = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colModsSummaryName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colModsSummaryFiller = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.chkShowExtraSteamLaunchOptions = new System.Windows.Forms.CheckBox();
            this.chkSteamDeck = new System.Windows.Forms.CheckBox();
            this.chkDisableWarningBadAppId = new System.Windows.Forms.CheckBox();
            this.chkEnableSteamPreownedIds = new System.Windows.Forms.CheckBox();
            this.tabAdvancedFeatures = new System.Windows.Forms.TabPage();
            this.grpEmulation = new System.Windows.Forms.GroupBox();
            this.chkDisableSteamoverlaygameidEnvVar = new System.Windows.Forms.CheckBox();
            this.chkEnableExperimentalOverlayGame = new System.Windows.Forms.CheckBox();
            this.chkForceSteamhttpSuccess = new System.Windows.Forms.CheckBox();
            this.chkFreeWeekend = new System.Windows.Forms.CheckBox();
            this.chkEnableVoiceChat = new System.Windows.Forms.CheckBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.tabControl.SuspendLayout();
            this.tabGameInfo.SuspendLayout();
            this.grpPaths.SuspendLayout();
            this.grpSteamLaunchOptions.SuspendLayout();
            this.grpBasicInfo.SuspendLayout();
            this.tabDLCContent.SuspendLayout();
            this.grpSubscribedGroups.SuspendLayout();
            this.grpDLCManagement.SuspendLayout();
            this.tabOtherSettings.SuspendLayout();
            this.grpAuthenticationSettings.SuspendLayout();
            this.grpAdvancedAuth.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numAltSteamIdCount)).BeginInit();
            this.tabStatsAchievements.SuspendLayout();
            this.grpAchievementsFile.SuspendLayout();
            this.grpCustomStats.SuspendLayout();
            this.pnlStatsDisplayScroll.SuspendLayout();
            this.grpStatsSettings.SuspendLayout();
            this.grpOtherStatsSettings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numIconsPerIteration)).BeginInit();
            this.tabServerMultiplayer.SuspendLayout();
            this.grpNetworkSettings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numOldP2PPacketSharingMode)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numForcePort)).BeginInit();
            this.grpMatchmakingSettings.SuspendLayout();
            this.tabInventory.SuspendLayout();
            this.grpInventoryEditor.SuspendLayout();
            this.pnlInventoryButtons.SuspendLayout();
            this.tabMods.SuspendLayout();
            this.grpMods.SuspendLayout();
            this.pnlModsToolbar.SuspendLayout();
            this.tabAdvancedFeatures.SuspendLayout();
            this.grpEmulation.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl
            // 
            this.tabControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl.Controls.Add(this.tabGameInfo);
            this.tabControl.Controls.Add(this.tabDLCContent);
            this.tabControl.Controls.Add(this.tabOtherSettings);
            this.tabControl.Controls.Add(this.tabStatsAchievements);
            this.tabControl.Controls.Add(this.tabServerMultiplayer);
            this.tabControl.Controls.Add(this.tabInventory);
            this.tabControl.Controls.Add(this.tabMods);
            this.tabControl.Location = new System.Drawing.Point(0, 0);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(689, 619);
            this.tabControl.TabIndex = 0;
            // 
            // tabGameInfo
            // 
            this.tabGameInfo.Controls.Add(this.grpPaths);
            this.tabGameInfo.Controls.Add(this.grpSteamLaunchOptions);
            this.tabGameInfo.Controls.Add(this.grpBasicInfo);
            this.tabGameInfo.Location = new System.Drawing.Point(4, 22);
            this.tabGameInfo.Name = "tabGameInfo";
            this.tabGameInfo.Padding = new System.Windows.Forms.Padding(3);
            this.tabGameInfo.Size = new System.Drawing.Size(681, 593);
            this.tabGameInfo.TabIndex = 0;
            this.tabGameInfo.Text = "Game and launch";
            this.tabGameInfo.UseVisualStyleBackColor = true;
            // 
            // grpPaths
            // 
            this.grpPaths.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpPaths.AutoSize = true;
            this.grpPaths.Controls.Add(this.lblHintGameFolder);
            this.grpPaths.Controls.Add(this.btnBrowseWorkingDirectory);
            this.grpPaths.Controls.Add(this.txtWorkingDirectory);
            this.grpPaths.Controls.Add(this.lblWorkingDirectory);
            this.grpPaths.Controls.Add(this.btnClearCustomIcon);
            this.grpPaths.Controls.Add(this.txtLaunchParameters);
            this.grpPaths.Controls.Add(this.lnkLauncherOptionsSteamDb);
            this.grpPaths.Controls.Add(this.lblCustomIcon);
            this.grpPaths.Controls.Add(this.btnBrowseCustomIcon);
            this.grpPaths.Controls.Add(this.lblLaunchParameters);
            this.grpPaths.Controls.Add(this.txtCustomIcon);
            this.grpPaths.Controls.Add(this.btnBrowseGameExecutable);
            this.grpPaths.Controls.Add(this.txtGameExecutable);
            this.grpPaths.Controls.Add(this.lblGameExecutable);
            this.grpPaths.Controls.Add(this.btnBrowseGameFolder);
            this.grpPaths.Controls.Add(this.lnkSteamCmdLineOptionsValveWiki);
            this.grpPaths.Controls.Add(this.txtGameFolder);
            this.grpPaths.Controls.Add(this.lblGameFolder);
            this.grpPaths.Controls.Add(this.txtBetaBranchName);
            this.grpPaths.Controls.Add(this.lblBetaBranchName);
            this.grpPaths.Controls.Add(this.chkBetaBranch);
            this.grpPaths.Location = new System.Drawing.Point(20, 216);
            this.grpPaths.Name = "grpPaths";
            this.grpPaths.Size = new System.Drawing.Size(635, 249);
            this.grpPaths.TabIndex = 1;
            this.grpPaths.TabStop = false;
            this.grpPaths.Text = "Game Settings";
            // 
            // lblHintGameFolder
            // 
            this.lblHintGameFolder.AutoSize = true;
            this.lblHintGameFolder.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblHintGameFolder.Location = new System.Drawing.Point(113, 23);
            this.lblHintGameFolder.Name = "lblHintGameFolder";
            this.lblHintGameFolder.Size = new System.Drawing.Size(24, 17);
            this.lblHintGameFolder.TabIndex = 20;
            this.lblHintGameFolder.Text = "⚠️";
            // 
            // btnBrowseWorkingDirectory
            // 
            this.btnBrowseWorkingDirectory.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseWorkingDirectory.Location = new System.Drawing.Point(575, 72);
            this.btnBrowseWorkingDirectory.Name = "btnBrowseWorkingDirectory";
            this.btnBrowseWorkingDirectory.Size = new System.Drawing.Size(23, 23);
            this.btnBrowseWorkingDirectory.TabIndex = 5;
            this.btnBrowseWorkingDirectory.Text = "🔍";
            this.btnBrowseWorkingDirectory.UseVisualStyleBackColor = true;
            this.btnBrowseWorkingDirectory.Click += new System.EventHandler(this.OnBrowseWorkingDirectory_Click);
            // 
            // txtWorkingDirectory
            // 
            this.txtWorkingDirectory.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtWorkingDirectory.Location = new System.Drawing.Point(143, 74);
            this.txtWorkingDirectory.Name = "txtWorkingDirectory";
            this.txtWorkingDirectory.Size = new System.Drawing.Size(424, 20);
            this.txtWorkingDirectory.TabIndex = 4;
            // 
            // lblWorkingDirectory
            // 
            this.lblWorkingDirectory.AutoSize = true;
            this.lblWorkingDirectory.Location = new System.Drawing.Point(13, 77);
            this.lblWorkingDirectory.Name = "lblWorkingDirectory";
            this.lblWorkingDirectory.Size = new System.Drawing.Size(93, 13);
            this.lblWorkingDirectory.TabIndex = 6;
            this.lblWorkingDirectory.Text = "Working directory:";
            // 
            // btnClearCustomIcon
            // 
            this.btnClearCustomIcon.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClearCustomIcon.Location = new System.Drawing.Point(573, 149);
            this.btnClearCustomIcon.Name = "btnClearCustomIcon";
            this.btnClearCustomIcon.Size = new System.Drawing.Size(23, 23);
            this.btnClearCustomIcon.TabIndex = 11;
            this.btnClearCustomIcon.Text = "❌";
            this.btnClearCustomIcon.UseVisualStyleBackColor = true;
            this.btnClearCustomIcon.Click += new System.EventHandler(this.OnClearCustomIcon_Click);
            // 
            // txtLaunchParameters
            // 
            this.txtLaunchParameters.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtLaunchParameters.Location = new System.Drawing.Point(143, 100);
            this.txtLaunchParameters.Name = "txtLaunchParameters";
            this.txtLaunchParameters.Size = new System.Drawing.Size(455, 20);
            this.txtLaunchParameters.TabIndex = 6;
            // 
            // lnkLauncherOptionsSteamDb
            // 
            this.lnkLauncherOptionsSteamDb.AutoSize = true;
            this.lnkLauncherOptionsSteamDb.Location = new System.Drawing.Point(140, 123);
            this.lnkLauncherOptionsSteamDb.Name = "lnkLauncherOptionsSteamDb";
            this.lnkLauncherOptionsSteamDb.Size = new System.Drawing.Size(145, 13);
            this.lnkLauncherOptionsSteamDb.TabIndex = 7;
            this.lnkLauncherOptionsSteamDb.TabStop = true;
            this.lnkLauncherOptionsSteamDb.Text = "Launcher Options (SteamDB)";
            this.lnkLauncherOptionsSteamDb.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.OnLauncherOptionsSteamDb_LinkClicked);
            // 
            // lblCustomIcon
            // 
            this.lblCustomIcon.AutoSize = true;
            this.lblCustomIcon.Location = new System.Drawing.Point(13, 154);
            this.lblCustomIcon.Name = "lblCustomIcon";
            this.lblCustomIcon.Size = new System.Drawing.Size(69, 13);
            this.lblCustomIcon.TabIndex = 13;
            this.lblCustomIcon.Text = "Custom Icon:";
            // 
            // btnBrowseCustomIcon
            // 
            this.btnBrowseCustomIcon.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseCustomIcon.Location = new System.Drawing.Point(544, 149);
            this.btnBrowseCustomIcon.Name = "btnBrowseCustomIcon";
            this.btnBrowseCustomIcon.Size = new System.Drawing.Size(23, 23);
            this.btnBrowseCustomIcon.TabIndex = 10;
            this.btnBrowseCustomIcon.Text = "🔍";
            this.btnBrowseCustomIcon.UseVisualStyleBackColor = true;
            this.btnBrowseCustomIcon.Click += new System.EventHandler(this.OnBrowseCustomIcon_Click);
            // 
            // lblLaunchParameters
            // 
            this.lblLaunchParameters.AutoSize = true;
            this.lblLaunchParameters.Location = new System.Drawing.Point(13, 103);
            this.lblLaunchParameters.Name = "lblLaunchParameters";
            this.lblLaunchParameters.Size = new System.Drawing.Size(60, 13);
            this.lblLaunchParameters.TabIndex = 9;
            this.lblLaunchParameters.Text = "Arguments:";
            // 
            // txtCustomIcon
            // 
            this.txtCustomIcon.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtCustomIcon.Location = new System.Drawing.Point(143, 151);
            this.txtCustomIcon.Name = "txtCustomIcon";
            this.txtCustomIcon.Size = new System.Drawing.Size(395, 20);
            this.txtCustomIcon.TabIndex = 9;
            // 
            // btnBrowseGameExecutable
            // 
            this.btnBrowseGameExecutable.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseGameExecutable.Location = new System.Drawing.Point(575, 46);
            this.btnBrowseGameExecutable.Name = "btnBrowseGameExecutable";
            this.btnBrowseGameExecutable.Size = new System.Drawing.Size(23, 23);
            this.btnBrowseGameExecutable.TabIndex = 3;
            this.btnBrowseGameExecutable.Text = "🔍";
            this.btnBrowseGameExecutable.UseVisualStyleBackColor = true;
            this.btnBrowseGameExecutable.Click += new System.EventHandler(this.OnBrowseGameExecutable_Click);
            // 
            // txtGameExecutable
            // 
            this.txtGameExecutable.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtGameExecutable.Location = new System.Drawing.Point(143, 48);
            this.txtGameExecutable.Name = "txtGameExecutable";
            this.txtGameExecutable.Size = new System.Drawing.Size(424, 20);
            this.txtGameExecutable.TabIndex = 2;
            // 
            // lblGameExecutable
            // 
            this.lblGameExecutable.AutoSize = true;
            this.lblGameExecutable.Location = new System.Drawing.Point(13, 51);
            this.lblGameExecutable.Name = "lblGameExecutable";
            this.lblGameExecutable.Size = new System.Drawing.Size(63, 13);
            this.lblGameExecutable.TabIndex = 3;
            this.lblGameExecutable.Text = "Executable:";
            // 
            // btnBrowseGameFolder
            // 
            this.btnBrowseGameFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseGameFolder.Location = new System.Drawing.Point(575, 20);
            this.btnBrowseGameFolder.Name = "btnBrowseGameFolder";
            this.btnBrowseGameFolder.Size = new System.Drawing.Size(23, 23);
            this.btnBrowseGameFolder.TabIndex = 1;
            this.btnBrowseGameFolder.Text = "🔍";
            this.btnBrowseGameFolder.UseVisualStyleBackColor = true;
            this.btnBrowseGameFolder.Click += new System.EventHandler(this.OnBrowseGameFolder_Click);
            // 
            // lnkSteamCmdLineOptionsValveWiki
            // 
            this.lnkSteamCmdLineOptionsValveWiki.AutoSize = true;
            this.lnkSteamCmdLineOptionsValveWiki.Location = new System.Drawing.Point(291, 123);
            this.lnkSteamCmdLineOptionsValveWiki.Name = "lnkSteamCmdLineOptionsValveWiki";
            this.lnkSteamCmdLineOptionsValveWiki.Size = new System.Drawing.Size(160, 13);
            this.lnkSteamCmdLineOptionsValveWiki.TabIndex = 8;
            this.lnkSteamCmdLineOptionsValveWiki.TabStop = true;
            this.lnkSteamCmdLineOptionsValveWiki.Text = "Arguments List Hints (Valve wiki)";
            this.lnkSteamCmdLineOptionsValveWiki.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.OnSteamCmdLineOptionsValveWiki_LinkClicked);
            // 
            // txtGameFolder
            // 
            this.txtGameFolder.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtGameFolder.Location = new System.Drawing.Point(143, 22);
            this.txtGameFolder.Name = "txtGameFolder";
            this.txtGameFolder.Size = new System.Drawing.Size(424, 20);
            this.txtGameFolder.TabIndex = 0;
            // 
            // lblGameFolder
            // 
            this.lblGameFolder.AutoSize = true;
            this.lblGameFolder.Location = new System.Drawing.Point(13, 25);
            this.lblGameFolder.Name = "lblGameFolder";
            this.lblGameFolder.Size = new System.Drawing.Size(88, 13);
            this.lblGameFolder.TabIndex = 0;
            this.lblGameFolder.Text = "Game root folder:";
            // 
            // txtBetaBranchName
            // 
            this.txtBetaBranchName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtBetaBranchName.Location = new System.Drawing.Point(143, 210);
            this.txtBetaBranchName.Name = "txtBetaBranchName";
            this.txtBetaBranchName.Size = new System.Drawing.Size(395, 20);
            this.txtBetaBranchName.TabIndex = 13;
            // 
            // lblBetaBranchName
            // 
            this.lblBetaBranchName.AutoSize = true;
            this.lblBetaBranchName.Location = new System.Drawing.Point(13, 213);
            this.lblBetaBranchName.Name = "lblBetaBranchName";
            this.lblBetaBranchName.Size = new System.Drawing.Size(100, 13);
            this.lblBetaBranchName.TabIndex = 18;
            this.lblBetaBranchName.Text = "Beta Branch Name:";
            // 
            // chkBetaBranch
            // 
            this.chkBetaBranch.AutoSize = true;
            this.chkBetaBranch.Location = new System.Drawing.Point(143, 187);
            this.chkBetaBranch.Name = "chkBetaBranch";
            this.chkBetaBranch.Size = new System.Drawing.Size(85, 17);
            this.chkBetaBranch.TabIndex = 12;
            this.chkBetaBranch.Text = "Beta Branch";
            this.chkBetaBranch.UseVisualStyleBackColor = true;
            // 
            // grpSteamLaunchOptions
            // 
            this.grpSteamLaunchOptions.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpSteamLaunchOptions.Controls.Add(this.lblSteamLaunchOptions);
            this.grpSteamLaunchOptions.Controls.Add(this.cmbSteamLaunchOptions);
            this.grpSteamLaunchOptions.Controls.Add(this.btnRemoveUserLaunchOption);
            this.grpSteamLaunchOptions.Controls.Add(this.lblUserLaunchOptionName);
            this.grpSteamLaunchOptions.Controls.Add(this.txtUserLaunchOptionName);
            this.grpSteamLaunchOptions.Controls.Add(this.btnSaveUserLaunchOption);
            this.grpSteamLaunchOptions.Location = new System.Drawing.Point(20, 470);
            this.grpSteamLaunchOptions.Name = "grpSteamLaunchOptions";
            this.grpSteamLaunchOptions.Size = new System.Drawing.Size(640, 88);
            this.grpSteamLaunchOptions.TabIndex = 2;
            this.grpSteamLaunchOptions.TabStop = false;
            this.grpSteamLaunchOptions.Text = "Launch Options";
            // 
            // lblSteamLaunchOptions
            // 
            this.lblSteamLaunchOptions.AutoSize = true;
            this.lblSteamLaunchOptions.Location = new System.Drawing.Point(20, 24);
            this.lblSteamLaunchOptions.Name = "lblSteamLaunchOptions";
            this.lblSteamLaunchOptions.Size = new System.Drawing.Size(98, 13);
            this.lblSteamLaunchOptions.TabIndex = 0;
            this.lblSteamLaunchOptions.Text = "Load launch option";
            this.lblSteamLaunchOptions.Visible = false;
            // 
            // cmbSteamLaunchOptions
            // 
            this.cmbSteamLaunchOptions.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.cmbSteamLaunchOptions.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbSteamLaunchOptions.FormattingEnabled = true;
            this.cmbSteamLaunchOptions.Location = new System.Drawing.Point(150, 21);
            this.cmbSteamLaunchOptions.Name = "cmbSteamLaunchOptions";
            this.cmbSteamLaunchOptions.Size = new System.Drawing.Size(304, 21);
            this.cmbSteamLaunchOptions.TabIndex = 1;
            this.cmbSteamLaunchOptions.Visible = false;
            this.cmbSteamLaunchOptions.SelectedIndexChanged += new System.EventHandler(this.CmbSteamLaunchOptions_SelectedIndexChanged);
            // 
            // btnRemoveUserLaunchOption
            // 
            this.btnRemoveUserLaunchOption.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRemoveUserLaunchOption.Enabled = false;
            this.btnRemoveUserLaunchOption.Location = new System.Drawing.Point(460, 20);
            this.btnRemoveUserLaunchOption.Name = "btnRemoveUserLaunchOption";
            this.btnRemoveUserLaunchOption.Size = new System.Drawing.Size(23, 23);
            this.btnRemoveUserLaunchOption.TabIndex = 2;
            this.btnRemoveUserLaunchOption.Text = "🗑️";
            this.btnRemoveUserLaunchOption.UseVisualStyleBackColor = true;
            this.btnRemoveUserLaunchOption.Click += new System.EventHandler(this.OnRemoveUserLaunchOption_Click);
            // 
            // lblUserLaunchOptionName
            // 
            this.lblUserLaunchOptionName.AutoSize = true;
            this.lblUserLaunchOptionName.Location = new System.Drawing.Point(20, 52);
            this.lblUserLaunchOptionName.Name = "lblUserLaunchOptionName";
            this.lblUserLaunchOptionName.Size = new System.Drawing.Size(108, 13);
            this.lblUserLaunchOptionName.TabIndex = 4;
            this.lblUserLaunchOptionName.Text = "Save current options:";
            // 
            // txtUserLaunchOptionName
            // 
            this.txtUserLaunchOptionName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtUserLaunchOptionName.Location = new System.Drawing.Point(150, 49);
            this.txtUserLaunchOptionName.Name = "txtUserLaunchOptionName";
            this.txtUserLaunchOptionName.Size = new System.Drawing.Size(422, 20);
            this.txtUserLaunchOptionName.TabIndex = 5;
            // 
            // btnSaveUserLaunchOption
            // 
            this.btnSaveUserLaunchOption.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSaveUserLaunchOption.Location = new System.Drawing.Point(578, 47);
            this.btnSaveUserLaunchOption.Name = "btnSaveUserLaunchOption";
            this.btnSaveUserLaunchOption.Size = new System.Drawing.Size(23, 23);
            this.btnSaveUserLaunchOption.TabIndex = 6;
            this.btnSaveUserLaunchOption.Text = "💾";
            this.btnSaveUserLaunchOption.UseVisualStyleBackColor = true;
            this.btnSaveUserLaunchOption.Click += new System.EventHandler(this.OnSaveUserLaunchOption_Click);
            // 
            // grpBasicInfo
            // 
            this.grpBasicInfo.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpBasicInfo.AutoSize = true;
            this.grpBasicInfo.Controls.Add(this.lblAppID);
            this.grpBasicInfo.Controls.Add(this.txtAppID);
            this.grpBasicInfo.Controls.Add(this.btnLookupAppID);
            this.grpBasicInfo.Controls.Add(this.lblGameName);
            this.grpBasicInfo.Controls.Add(this.txtGameName);
            this.grpBasicInfo.Controls.Add(this.btnLookupGameName);
            this.grpBasicInfo.Controls.Add(this.lblSteamAPIStatus);
            this.grpBasicInfo.Controls.Add(this.lblSteamAPIStatusX32Value);
            this.grpBasicInfo.Controls.Add(this.lblSteamAPIStatusX64Value);
            this.grpBasicInfo.Controls.Add(this.lblSteamApiHealth);
            this.grpBasicInfo.Controls.Add(this.lblSteamApiHealthValue);
            this.grpBasicInfo.Controls.Add(this.lblSteamApiHealthNote);
            this.grpBasicInfo.Controls.Add(this.lblPatchOpMessage);
            this.grpBasicInfo.Controls.Add(this.lblLaunchMode);
            this.grpBasicInfo.Controls.Add(this.rdoLaunchSteamClient);
            this.grpBasicInfo.Controls.Add(this.rdoLaunchExperimentalMode);
            this.grpBasicInfo.Controls.Add(this.rdoLaunchSteamDll);
            this.grpBasicInfo.Controls.Add(this.rdoLaunchNoEmulation);
            this.grpBasicInfo.Controls.Add(this.btnRestoreDlls);
            this.grpBasicInfo.Location = new System.Drawing.Point(20, 10);
            this.grpBasicInfo.Name = "grpBasicInfo";
            this.grpBasicInfo.Padding = new System.Windows.Forms.Padding(10);
            this.grpBasicInfo.Size = new System.Drawing.Size(635, 207);
            this.grpBasicInfo.TabIndex = 0;
            this.grpBasicInfo.TabStop = false;
            this.grpBasicInfo.Text = "Game Info";
            // 
            // lblAppID
            // 
            this.lblAppID.AutoSize = true;
            this.lblAppID.Location = new System.Drawing.Point(20, 28);
            this.lblAppID.Name = "lblAppID";
            this.lblAppID.Size = new System.Drawing.Size(40, 13);
            this.lblAppID.TabIndex = 0;
            this.lblAppID.Text = "AppID:";
            // 
            // txtAppID
            // 
            this.txtAppID.Location = new System.Drawing.Point(150, 25);
            this.txtAppID.Name = "txtAppID";
            this.txtAppID.Size = new System.Drawing.Size(130, 20);
            this.txtAppID.TabIndex = 1;
            // 
            // btnLookupAppID
            // 
            this.btnLookupAppID.Location = new System.Drawing.Point(286, 23);
            this.btnLookupAppID.Name = "btnLookupAppID";
            this.btnLookupAppID.Size = new System.Drawing.Size(23, 23);
            this.btnLookupAppID.TabIndex = 2;
            this.btnLookupAppID.Text = "🔍";
            this.btnLookupAppID.UseVisualStyleBackColor = true;
            this.btnLookupAppID.Click += new System.EventHandler(this.OnLookupAppID_Click);
            // 
            // lblGameName
            // 
            this.lblGameName.AutoSize = true;
            this.lblGameName.Location = new System.Drawing.Point(20, 58);
            this.lblGameName.Name = "lblGameName";
            this.lblGameName.Size = new System.Drawing.Size(69, 13);
            this.lblGameName.TabIndex = 5;
            this.lblGameName.Text = "Game Name:";
            // 
            // txtGameName
            // 
            this.txtGameName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtGameName.Location = new System.Drawing.Point(150, 55);
            this.txtGameName.Name = "txtGameName";
            this.txtGameName.Size = new System.Drawing.Size(417, 20);
            this.txtGameName.TabIndex = 5;
            // 
            // btnLookupGameName
            // 
            this.btnLookupGameName.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnLookupGameName.Location = new System.Drawing.Point(573, 53);
            this.btnLookupGameName.Name = "btnLookupGameName";
            this.btnLookupGameName.Size = new System.Drawing.Size(23, 23);
            this.btnLookupGameName.TabIndex = 6;
            this.btnLookupGameName.Text = "🔍";
            this.btnLookupGameName.UseVisualStyleBackColor = true;
            this.btnLookupGameName.Click += new System.EventHandler(this.OnLookupGameName_Click);
            // 
            // lblSteamAPIStatus
            // 
            this.lblSteamAPIStatus.AutoSize = true;
            this.lblSteamAPIStatus.Location = new System.Drawing.Point(20, 118);
            this.lblSteamAPIStatus.Name = "lblSteamAPIStatus";
            this.lblSteamAPIStatus.Size = new System.Drawing.Size(93, 13);
            this.lblSteamAPIStatus.TabIndex = 8;
            this.lblSteamAPIStatus.Text = "Steam API Status:";
            // 
            // lblSteamAPIStatusX32Value
            // 
            this.lblSteamAPIStatusX32Value.AutoSize = true;
            this.lblSteamAPIStatusX32Value.Location = new System.Drawing.Point(147, 105);
            this.lblSteamAPIStatusX32Value.Name = "lblSteamAPIStatusX32Value";
            this.lblSteamAPIStatusX32Value.Size = new System.Drawing.Size(140, 13);
            this.lblSteamAPIStatusX32Value.TabIndex = 9;
            this.lblSteamAPIStatusX32Value.Text = "lblSteamAPIStatusX32Value";
            // 
            // lblSteamAPIStatusX64Value
            // 
            this.lblSteamAPIStatusX64Value.AutoSize = true;
            this.lblSteamAPIStatusX64Value.Location = new System.Drawing.Point(147, 118);
            this.lblSteamAPIStatusX64Value.Name = "lblSteamAPIStatusX64Value";
            this.lblSteamAPIStatusX64Value.Size = new System.Drawing.Size(140, 13);
            this.lblSteamAPIStatusX64Value.TabIndex = 10;
            this.lblSteamAPIStatusX64Value.Text = "lblSteamAPIStatusX64Value";
            // 
            // lblSteamApiHealth
            // 
            this.lblSteamApiHealth.AutoSize = true;
            this.lblSteamApiHealth.Location = new System.Drawing.Point(20, 145);
            this.lblSteamApiHealth.Name = "lblSteamApiHealth";
            this.lblSteamApiHealth.Size = new System.Drawing.Size(94, 13);
            this.lblSteamApiHealth.TabIndex = 15;
            this.lblSteamApiHealth.Text = "Steam API Health:";
            // 
            // lblSteamApiHealthValue
            // 
            this.lblSteamApiHealthValue.AutoSize = true;
            this.lblSteamApiHealthValue.Location = new System.Drawing.Point(147, 145);
            this.lblSteamApiHealthValue.Name = "lblSteamApiHealthValue";
            this.lblSteamApiHealthValue.Size = new System.Drawing.Size(86, 13);
            this.lblSteamApiHealthValue.TabIndex = 16;
            this.lblSteamApiHealthValue.Text = "(health summary)";
            this.lblSteamApiHealthValue.Visible = false;
            // 
            // lblSteamApiHealthNote
            // 
            this.lblSteamApiHealthNote.AutoSize = true;
            this.lblSteamApiHealthNote.Location = new System.Drawing.Point(147, 158);
            this.lblSteamApiHealthNote.Name = "lblSteamApiHealthNote";
            this.lblSteamApiHealthNote.Size = new System.Drawing.Size(66, 13);
            this.lblSteamApiHealthNote.TabIndex = 19;
            this.lblSteamApiHealthNote.Text = "(health note)";
            this.lblSteamApiHealthNote.Visible = false;
            // 
            // lblPatchOpMessage
            // 
            this.lblPatchOpMessage.AutoSize = true;
            this.lblPatchOpMessage.Location = new System.Drawing.Point(147, 171);
            this.lblPatchOpMessage.Name = "lblPatchOpMessage";
            this.lblPatchOpMessage.Size = new System.Drawing.Size(87, 13);
            this.lblPatchOpMessage.TabIndex = 14;
            this.lblPatchOpMessage.Text = "(patch operation)";
            this.lblPatchOpMessage.Visible = false;
            // 
            // lblLaunchMode
            // 
            this.lblLaunchMode.AutoSize = true;
            this.lblLaunchMode.Location = new System.Drawing.Point(20, 81);
            this.lblLaunchMode.Name = "lblLaunchMode";
            this.lblLaunchMode.Size = new System.Drawing.Size(75, 13);
            this.lblLaunchMode.TabIndex = 50;
            this.lblLaunchMode.Text = "Launch mode:";
            // 
            // rdoLaunchSteamClient
            // 
            this.rdoLaunchSteamClient.AutoSize = true;
            this.rdoLaunchSteamClient.Checked = true;
            this.rdoLaunchSteamClient.Location = new System.Drawing.Point(150, 79);
            this.rdoLaunchSteamClient.Name = "rdoLaunchSteamClient";
            this.rdoLaunchSteamClient.Size = new System.Drawing.Size(114, 17);
            this.rdoLaunchSteamClient.TabIndex = 0;
            this.rdoLaunchSteamClient.TabStop = true;
            this.rdoLaunchSteamClient.Text = "Steam Client Mode";
            this.rdoLaunchSteamClient.UseVisualStyleBackColor = true;
            // 
            // rdoLaunchExperimentalMode
            // 
            this.rdoLaunchExperimentalMode.AutoSize = true;
            this.rdoLaunchExperimentalMode.Location = new System.Drawing.Point(272, 79);
            this.rdoLaunchExperimentalMode.Name = "rdoLaunchExperimentalMode";
            this.rdoLaunchExperimentalMode.Size = new System.Drawing.Size(115, 17);
            this.rdoLaunchExperimentalMode.TabIndex = 1;
            this.rdoLaunchExperimentalMode.Text = "Experimental Mode";
            this.rdoLaunchExperimentalMode.UseVisualStyleBackColor = true;
            // 
            // rdoLaunchSteamDll
            // 
            this.rdoLaunchSteamDll.AutoSize = true;
            this.rdoLaunchSteamDll.Location = new System.Drawing.Point(395, 79);
            this.rdoLaunchSteamDll.Name = "rdoLaunchSteamDll";
            this.rdoLaunchSteamDll.Size = new System.Drawing.Size(98, 17);
            this.rdoLaunchSteamDll.TabIndex = 2;
            this.rdoLaunchSteamDll.Text = "Steam.dll Mode";
            this.rdoLaunchSteamDll.UseVisualStyleBackColor = true;
            // 
            // rdoLaunchNoEmulation
            // 
            this.rdoLaunchNoEmulation.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.rdoLaunchNoEmulation.AutoSize = true;
            this.rdoLaunchNoEmulation.Location = new System.Drawing.Point(496, 79);
            this.rdoLaunchNoEmulation.Name = "rdoLaunchNoEmulation";
            this.rdoLaunchNoEmulation.Size = new System.Drawing.Size(87, 17);
            this.rdoLaunchNoEmulation.TabIndex = 3;
            this.rdoLaunchNoEmulation.Text = "No emulation";
            this.rdoLaunchNoEmulation.UseVisualStyleBackColor = true;
            // 
            // btnRestoreDlls
            // 
            this.btnRestoreDlls.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRestoreDlls.Enabled = false;
            this.btnRestoreDlls.Location = new System.Drawing.Point(508, 140);
            this.btnRestoreDlls.Name = "btnRestoreDlls";
            this.btnRestoreDlls.Size = new System.Drawing.Size(88, 23);
            this.btnRestoreDlls.TabIndex = 8;
            this.btnRestoreDlls.Text = "Restore";
            this.btnRestoreDlls.UseVisualStyleBackColor = false;
            this.btnRestoreDlls.Click += new System.EventHandler(this.OnRestoreDlls_Click);
            // 
            // tabDLCContent
            // 
            this.tabDLCContent.Controls.Add(this.grpSubscribedGroups);
            this.tabDLCContent.Controls.Add(this.grpDLCManagement);
            this.tabDLCContent.Location = new System.Drawing.Point(4, 22);
            this.tabDLCContent.Name = "tabDLCContent";
            this.tabDLCContent.Padding = new System.Windows.Forms.Padding(3);
            this.tabDLCContent.Size = new System.Drawing.Size(681, 593);
            this.tabDLCContent.TabIndex = 1;
            this.tabDLCContent.Text = "DLC and content";
            this.tabDLCContent.UseVisualStyleBackColor = true;
            // 
            // grpSubscribedGroups
            // 
            this.grpSubscribedGroups.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpSubscribedGroups.Controls.Add(this.lblSubscribedGroups);
            this.grpSubscribedGroups.Controls.Add(this.lblSubscribedGroupsClans);
            this.grpSubscribedGroups.Controls.Add(this.lstSubscribedGroups);
            this.grpSubscribedGroups.Controls.Add(this.lstSubscribedGroupsClans);
            this.grpSubscribedGroups.Controls.Add(this.txtSubscribedGroupIdEntry);
            this.grpSubscribedGroups.Controls.Add(this.btnAddSubscribedGroup);
            this.grpSubscribedGroups.Controls.Add(this.btnRemoveSubscribedGroup);
            this.grpSubscribedGroups.Controls.Add(this.txtSubscribedGroupClanEntry);
            this.grpSubscribedGroups.Controls.Add(this.btnAddSubscribedGroupClan);
            this.grpSubscribedGroups.Controls.Add(this.btnRemoveSubscribedGroupClan);
            this.grpSubscribedGroups.Location = new System.Drawing.Point(20, 316);
            this.grpSubscribedGroups.Name = "grpSubscribedGroups";
            this.grpSubscribedGroups.Size = new System.Drawing.Size(640, 243);
            this.grpSubscribedGroups.TabIndex = 2;
            this.grpSubscribedGroups.TabStop = false;
            this.grpSubscribedGroups.Text = "Subscribed groups";
            // 
            // lblSubscribedGroups
            // 
            this.lblSubscribedGroups.AutoSize = true;
            this.lblSubscribedGroups.Location = new System.Drawing.Point(14, 20);
            this.lblSubscribedGroups.Name = "lblSubscribedGroups";
            this.lblSubscribedGroups.Size = new System.Drawing.Size(100, 13);
            this.lblSubscribedGroups.TabIndex = 0;
            this.lblSubscribedGroups.Text = "Subscribed Groups:";
            this.lblSubscribedGroups.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // lblSubscribedGroupsClans
            // 
            this.lblSubscribedGroupsClans.AutoSize = true;
            this.lblSubscribedGroupsClans.Location = new System.Drawing.Point(332, 20);
            this.lblSubscribedGroupsClans.Name = "lblSubscribedGroupsClans";
            this.lblSubscribedGroupsClans.Size = new System.Drawing.Size(121, 13);
            this.lblSubscribedGroupsClans.TabIndex = 1;
            this.lblSubscribedGroupsClans.Text = "Subscribed clan groups:";
            this.lblSubscribedGroupsClans.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // lstSubscribedGroups
            // 
            this.lstSubscribedGroups.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.lstSubscribedGroups.FormattingEnabled = true;
            this.lstSubscribedGroups.HorizontalScrollbar = true;
            this.lstSubscribedGroups.IntegralHeight = false;
            this.lstSubscribedGroups.Location = new System.Drawing.Point(14, 42);
            this.lstSubscribedGroups.Name = "lstSubscribedGroups";
            this.lstSubscribedGroups.Size = new System.Drawing.Size(305, 166);
            this.lstSubscribedGroups.TabIndex = 0;
            this.lstSubscribedGroups.SelectedIndexChanged += new System.EventHandler(this.LstSubscribedGroups_SelectedIndexChanged);
            // 
            // lstSubscribedGroupsClans
            // 
            this.lstSubscribedGroupsClans.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lstSubscribedGroupsClans.FormattingEnabled = true;
            this.lstSubscribedGroupsClans.HorizontalScrollbar = true;
            this.lstSubscribedGroupsClans.IntegralHeight = false;
            this.lstSubscribedGroupsClans.Location = new System.Drawing.Point(332, 42);
            this.lstSubscribedGroupsClans.Name = "lstSubscribedGroupsClans";
            this.lstSubscribedGroupsClans.Size = new System.Drawing.Size(294, 166);
            this.lstSubscribedGroupsClans.TabIndex = 2;
            this.lstSubscribedGroupsClans.SelectedIndexChanged += new System.EventHandler(this.LstSubscribedGroupsClans_SelectedIndexChanged);
            // 
            // txtSubscribedGroupIdEntry
            // 
            this.txtSubscribedGroupIdEntry.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.txtSubscribedGroupIdEntry.Location = new System.Drawing.Point(14, 214);
            this.txtSubscribedGroupIdEntry.Name = "txtSubscribedGroupIdEntry";
            this.txtSubscribedGroupIdEntry.Size = new System.Drawing.Size(247, 20);
            this.txtSubscribedGroupIdEntry.TabIndex = 3;
            // 
            // btnAddSubscribedGroup
            // 
            this.btnAddSubscribedGroup.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnAddSubscribedGroup.Location = new System.Drawing.Point(267, 212);
            this.btnAddSubscribedGroup.Name = "btnAddSubscribedGroup";
            this.btnAddSubscribedGroup.Size = new System.Drawing.Size(23, 23);
            this.btnAddSubscribedGroup.TabIndex = 4;
            this.btnAddSubscribedGroup.Text = "➕";
            this.btnAddSubscribedGroup.UseVisualStyleBackColor = true;
            this.btnAddSubscribedGroup.Click += new System.EventHandler(this.OnAddSubscribedGroup_Click);
            // 
            // btnRemoveSubscribedGroup
            // 
            this.btnRemoveSubscribedGroup.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnRemoveSubscribedGroup.Location = new System.Drawing.Point(296, 212);
            this.btnRemoveSubscribedGroup.Name = "btnRemoveSubscribedGroup";
            this.btnRemoveSubscribedGroup.Size = new System.Drawing.Size(23, 23);
            this.btnRemoveSubscribedGroup.TabIndex = 5;
            this.btnRemoveSubscribedGroup.Text = "➖";
            this.btnRemoveSubscribedGroup.UseVisualStyleBackColor = true;
            this.btnRemoveSubscribedGroup.Click += new System.EventHandler(this.OnRemoveSubscribedGroup_Click);
            // 
            // txtSubscribedGroupClanEntry
            // 
            this.txtSubscribedGroupClanEntry.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.txtSubscribedGroupClanEntry.Location = new System.Drawing.Point(332, 214);
            this.txtSubscribedGroupClanEntry.Name = "txtSubscribedGroupClanEntry";
            this.txtSubscribedGroupClanEntry.Size = new System.Drawing.Size(236, 20);
            this.txtSubscribedGroupClanEntry.TabIndex = 6;
            // 
            // btnAddSubscribedGroupClan
            // 
            this.btnAddSubscribedGroupClan.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnAddSubscribedGroupClan.Location = new System.Drawing.Point(574, 212);
            this.btnAddSubscribedGroupClan.Name = "btnAddSubscribedGroupClan";
            this.btnAddSubscribedGroupClan.Size = new System.Drawing.Size(23, 23);
            this.btnAddSubscribedGroupClan.TabIndex = 7;
            this.btnAddSubscribedGroupClan.Text = "➕";
            this.btnAddSubscribedGroupClan.UseVisualStyleBackColor = true;
            this.btnAddSubscribedGroupClan.Click += new System.EventHandler(this.OnAddSubscribedGroupClan_Click);
            // 
            // btnRemoveSubscribedGroupClan
            // 
            this.btnRemoveSubscribedGroupClan.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnRemoveSubscribedGroupClan.Location = new System.Drawing.Point(603, 212);
            this.btnRemoveSubscribedGroupClan.Name = "btnRemoveSubscribedGroupClan";
            this.btnRemoveSubscribedGroupClan.Size = new System.Drawing.Size(23, 23);
            this.btnRemoveSubscribedGroupClan.TabIndex = 8;
            this.btnRemoveSubscribedGroupClan.Text = "➖";
            this.btnRemoveSubscribedGroupClan.UseVisualStyleBackColor = true;
            this.btnRemoveSubscribedGroupClan.Click += new System.EventHandler(this.OnRemoveSubscribedGroupClan_Click);
            // 
            // grpDLCManagement
            // 
            this.grpDLCManagement.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpDLCManagement.Controls.Add(this.btnFindDLCs);
            this.grpDLCManagement.Controls.Add(this.txtDLCList);
            this.grpDLCManagement.Controls.Add(this.chkUnlockAllDLC);
            this.grpDLCManagement.Location = new System.Drawing.Point(20, 10);
            this.grpDLCManagement.Name = "grpDLCManagement";
            this.grpDLCManagement.Size = new System.Drawing.Size(640, 300);
            this.grpDLCManagement.TabIndex = 0;
            this.grpDLCManagement.TabStop = false;
            this.grpDLCManagement.Text = "DLC list";
            // 
            // btnFindDLCs
            // 
            this.btnFindDLCs.Location = new System.Drawing.Point(545, 26);
            this.btnFindDLCs.Name = "btnFindDLCs";
            this.btnFindDLCs.Size = new System.Drawing.Size(75, 23);
            this.btnFindDLCs.TabIndex = 1;
            this.btnFindDLCs.Text = "Find DLCs";
            this.btnFindDLCs.UseVisualStyleBackColor = true;
            this.btnFindDLCs.Click += new System.EventHandler(this.OnFindDLCs_Click);
            // 
            // txtDLCList
            // 
            this.txtDLCList.AcceptsReturn = true;
            this.txtDLCList.AcceptsTab = true;
            this.txtDLCList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtDLCList.Location = new System.Drawing.Point(20, 55);
            this.txtDLCList.Multiline = true;
            this.txtDLCList.Name = "txtDLCList";
            this.txtDLCList.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtDLCList.Size = new System.Drawing.Size(600, 222);
            this.txtDLCList.TabIndex = 2;
            // 
            // chkUnlockAllDLC
            // 
            this.chkUnlockAllDLC.AutoSize = true;
            this.chkUnlockAllDLC.Location = new System.Drawing.Point(20, 30);
            this.chkUnlockAllDLC.Name = "chkUnlockAllDLC";
            this.chkUnlockAllDLC.Size = new System.Drawing.Size(98, 17);
            this.chkUnlockAllDLC.TabIndex = 0;
            this.chkUnlockAllDLC.Text = "Unlock All DLC";
            this.chkUnlockAllDLC.UseVisualStyleBackColor = true;
            // 
            // tabOtherSettings
            // 
            this.tabOtherSettings.AutoScroll = true;
            this.tabOtherSettings.Controls.Add(this.grpAuthenticationSettings);
            this.tabOtherSettings.Controls.Add(this.grpAdvancedAuth);
            this.tabOtherSettings.Location = new System.Drawing.Point(4, 22);
            this.tabOtherSettings.Name = "tabOtherSettings";
            this.tabOtherSettings.Padding = new System.Windows.Forms.Padding(3);
            this.tabOtherSettings.Size = new System.Drawing.Size(681, 593);
            this.tabOtherSettings.TabIndex = 2;
            this.tabOtherSettings.Text = "Account overrides";
            this.tabOtherSettings.UseVisualStyleBackColor = true;
            // 
            // grpAuthenticationSettings
            // 
            this.grpAuthenticationSettings.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpAuthenticationSettings.Controls.Add(this.txtForceIpCountry);
            this.grpAuthenticationSettings.Controls.Add(this.lblForceIpCountry);
            this.grpAuthenticationSettings.Controls.Add(this.txtClanTag);
            this.grpAuthenticationSettings.Controls.Add(this.lblClanTag);
            this.grpAuthenticationSettings.Controls.Add(this.txtForceSteamId);
            this.grpAuthenticationSettings.Controls.Add(this.lblForceSteamId);
            this.grpAuthenticationSettings.Controls.Add(this.cmbForceLanguage);
            this.grpAuthenticationSettings.Controls.Add(this.lblForceLanguage);
            this.grpAuthenticationSettings.Controls.Add(this.txtForceAccountName);
            this.grpAuthenticationSettings.Controls.Add(this.lblForceAccountName);
            this.grpAuthenticationSettings.Location = new System.Drawing.Point(20, 10);
            this.grpAuthenticationSettings.Name = "grpAuthenticationSettings";
            this.grpAuthenticationSettings.Size = new System.Drawing.Size(640, 178);
            this.grpAuthenticationSettings.TabIndex = 0;
            this.grpAuthenticationSettings.TabStop = false;
            this.grpAuthenticationSettings.Text = "Profile overrides";
            // 
            // txtForceIpCountry
            // 
            this.txtForceIpCountry.Location = new System.Drawing.Point(150, 145);
            this.txtForceIpCountry.Name = "txtForceIpCountry";
            this.txtForceIpCountry.Size = new System.Drawing.Size(200, 20);
            this.txtForceIpCountry.TabIndex = 9;
            // 
            // lblForceIpCountry
            // 
            this.lblForceIpCountry.AutoSize = true;
            this.lblForceIpCountry.Location = new System.Drawing.Point(20, 148);
            this.lblForceIpCountry.Name = "lblForceIpCountry";
            this.lblForceIpCountry.Size = new System.Drawing.Size(99, 13);
            this.lblForceIpCountry.TabIndex = 8;
            this.lblForceIpCountry.Text = "IP country override:";
            // 
            // txtClanTag
            // 
            this.txtClanTag.Location = new System.Drawing.Point(150, 115);
            this.txtClanTag.Name = "txtClanTag";
            this.txtClanTag.Size = new System.Drawing.Size(200, 20);
            this.txtClanTag.TabIndex = 7;
            // 
            // lblClanTag
            // 
            this.lblClanTag.AutoSize = true;
            this.lblClanTag.Location = new System.Drawing.Point(20, 118);
            this.lblClanTag.Name = "lblClanTag";
            this.lblClanTag.Size = new System.Drawing.Size(53, 13);
            this.lblClanTag.TabIndex = 6;
            this.lblClanTag.Text = "Clan Tag:";
            // 
            // txtForceSteamId
            // 
            this.txtForceSteamId.Location = new System.Drawing.Point(150, 55);
            this.txtForceSteamId.Name = "txtForceSteamId";
            this.txtForceSteamId.Size = new System.Drawing.Size(200, 20);
            this.txtForceSteamId.TabIndex = 3;
            // 
            // lblForceSteamId
            // 
            this.lblForceSteamId.AutoSize = true;
            this.lblForceSteamId.Location = new System.Drawing.Point(20, 58);
            this.lblForceSteamId.Name = "lblForceSteamId";
            this.lblForceSteamId.Size = new System.Drawing.Size(95, 13);
            this.lblForceSteamId.TabIndex = 2;
            this.lblForceSteamId.Text = "Steam ID override:";
            // 
            // cmbForceLanguage
            // 
            this.cmbForceLanguage.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbForceLanguage.FormattingEnabled = true;
            this.cmbForceLanguage.Location = new System.Drawing.Point(150, 85);
            this.cmbForceLanguage.Name = "cmbForceLanguage";
            this.cmbForceLanguage.Size = new System.Drawing.Size(200, 21);
            this.cmbForceLanguage.TabIndex = 5;
            // 
            // lblForceLanguage
            // 
            this.lblForceLanguage.AutoSize = true;
            this.lblForceLanguage.Location = new System.Drawing.Point(20, 88);
            this.lblForceLanguage.Name = "lblForceLanguage";
            this.lblForceLanguage.Size = new System.Drawing.Size(99, 13);
            this.lblForceLanguage.TabIndex = 4;
            this.lblForceLanguage.Text = "Language override:";
            // 
            // txtForceAccountName
            // 
            this.txtForceAccountName.Location = new System.Drawing.Point(150, 25);
            this.txtForceAccountName.Name = "txtForceAccountName";
            this.txtForceAccountName.Size = new System.Drawing.Size(200, 20);
            this.txtForceAccountName.TabIndex = 1;
            // 
            // lblForceAccountName
            // 
            this.lblForceAccountName.AutoSize = true;
            this.lblForceAccountName.Location = new System.Drawing.Point(20, 28);
            this.lblForceAccountName.Name = "lblForceAccountName";
            this.lblForceAccountName.Size = new System.Drawing.Size(120, 13);
            this.lblForceAccountName.TabIndex = 0;
            this.lblForceAccountName.Text = "Account name override:";
            // 
            // grpAdvancedAuth
            // 
            this.grpAdvancedAuth.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpAdvancedAuth.Controls.Add(this.numAltSteamIdCount);
            this.grpAdvancedAuth.Controls.Add(this.lblAltSteamIdCount);
            this.grpAdvancedAuth.Controls.Add(this.txtAltSteamId);
            this.grpAdvancedAuth.Controls.Add(this.lblAltSteamId);
            this.grpAdvancedAuth.Controls.Add(this.txtUserTicket);
            this.grpAdvancedAuth.Controls.Add(this.lblUserTicket);
            this.grpAdvancedAuth.Location = new System.Drawing.Point(20, 194);
            this.grpAdvancedAuth.Name = "grpAdvancedAuth";
            this.grpAdvancedAuth.Size = new System.Drawing.Size(640, 170);
            this.grpAdvancedAuth.TabIndex = 1;
            this.grpAdvancedAuth.TabStop = false;
            this.grpAdvancedAuth.Text = "Tickets & alternate Steam IDs";
            // 
            // numAltSteamIdCount
            // 
            this.numAltSteamIdCount.Location = new System.Drawing.Point(150, 134);
            this.numAltSteamIdCount.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.numAltSteamIdCount.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numAltSteamIdCount.Name = "numAltSteamIdCount";
            this.numAltSteamIdCount.Size = new System.Drawing.Size(100, 20);
            this.numAltSteamIdCount.TabIndex = 2;
            this.numAltSteamIdCount.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // lblAltSteamIdCount
            // 
            this.lblAltSteamIdCount.AutoSize = true;
            this.lblAltSteamIdCount.Location = new System.Drawing.Point(20, 137);
            this.lblAltSteamIdCount.Name = "lblAltSteamIdCount";
            this.lblAltSteamIdCount.Size = new System.Drawing.Size(90, 13);
            this.lblAltSteamIdCount.TabIndex = 4;
            this.lblAltSteamIdCount.Text = "Alternate ID slots:";
            // 
            // txtAltSteamId
            // 
            this.txtAltSteamId.Location = new System.Drawing.Point(150, 104);
            this.txtAltSteamId.Name = "txtAltSteamId";
            this.txtAltSteamId.Size = new System.Drawing.Size(200, 20);
            this.txtAltSteamId.TabIndex = 1;
            // 
            // lblAltSteamId
            // 
            this.lblAltSteamId.AutoSize = true;
            this.lblAltSteamId.Location = new System.Drawing.Point(20, 107);
            this.lblAltSteamId.Name = "lblAltSteamId";
            this.lblAltSteamId.Size = new System.Drawing.Size(104, 13);
            this.lblAltSteamId.TabIndex = 2;
            this.lblAltSteamId.Text = "Alternate Steam IDs:";
            // 
            // txtUserTicket
            // 
            this.txtUserTicket.Location = new System.Drawing.Point(150, 20);
            this.txtUserTicket.Multiline = true;
            this.txtUserTicket.Name = "txtUserTicket";
            this.txtUserTicket.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtUserTicket.Size = new System.Drawing.Size(470, 78);
            this.txtUserTicket.TabIndex = 0;
            // 
            // lblUserTicket
            // 
            this.lblUserTicket.AutoSize = true;
            this.lblUserTicket.Location = new System.Drawing.Point(20, 23);
            this.lblUserTicket.Name = "lblUserTicket";
            this.lblUserTicket.Size = new System.Drawing.Size(121, 13);
            this.lblUserTicket.TabIndex = 0;
            this.lblUserTicket.Text = "Session ticket (Base64):";
            // 
            // tabStatsAchievements
            // 
            this.tabStatsAchievements.Controls.Add(this.grpAchievementsFile);
            this.tabStatsAchievements.Controls.Add(this.grpCustomStats);
            this.tabStatsAchievements.Controls.Add(this.grpStatsSettings);
            this.tabStatsAchievements.Controls.Add(this.grpOtherStatsSettings);
            this.tabStatsAchievements.Location = new System.Drawing.Point(4, 22);
            this.tabStatsAchievements.Name = "tabStatsAchievements";
            this.tabStatsAchievements.Padding = new System.Windows.Forms.Padding(3);
            this.tabStatsAchievements.Size = new System.Drawing.Size(681, 593);
            this.tabStatsAchievements.TabIndex = 3;
            this.tabStatsAchievements.Text = "Achievements and stats";
            this.tabStatsAchievements.UseVisualStyleBackColor = true;
            // 
            // grpAchievementsFile
            // 
            this.grpAchievementsFile.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpAchievementsFile.Controls.Add(this.lstAchievementsPreview);
            this.grpAchievementsFile.Controls.Add(this.lblAchievementsPreview);
            this.grpAchievementsFile.Controls.Add(this.lblAchievementsFilter);
            this.grpAchievementsFile.Controls.Add(this.txtAchievementsFilter);
            this.grpAchievementsFile.Controls.Add(this.btnRefreshAchievements);
            this.grpAchievementsFile.Location = new System.Drawing.Point(286, 10);
            this.grpAchievementsFile.Name = "grpAchievementsFile";
            this.grpAchievementsFile.Size = new System.Drawing.Size(387, 566);
            this.grpAchievementsFile.TabIndex = 3;
            this.grpAchievementsFile.TabStop = false;
            this.grpAchievementsFile.Text = "Achievements file";
            // 
            // lstAchievementsPreview
            // 
            this.lstAchievementsPreview.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lstAchievementsPreview.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lstAchievementsPreview.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colAchievementName,
            this.colAchievementDescription});
            this.lstAchievementsPreview.FullRowSelect = true;
            this.lstAchievementsPreview.HideSelection = false;
            this.lstAchievementsPreview.Location = new System.Drawing.Point(6, 19);
            this.lstAchievementsPreview.MultiSelect = false;
            this.lstAchievementsPreview.Name = "lstAchievementsPreview";
            this.lstAchievementsPreview.OwnerDraw = true;
            this.lstAchievementsPreview.ShowItemToolTips = true;
            this.lstAchievementsPreview.Size = new System.Drawing.Size(375, 512);
            this.lstAchievementsPreview.SmallImageList = this.imgAchievementsPreview;
            this.lstAchievementsPreview.TabIndex = 0;
            this.lstAchievementsPreview.UseCompatibleStateImageBehavior = false;
            this.lstAchievementsPreview.View = System.Windows.Forms.View.Details;
            this.lstAchievementsPreview.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.LstAchievementsPreview_ColumnClick);
            // 
            // colAchievementName
            // 
            this.colAchievementName.Text = "Achievement";
            this.colAchievementName.Width = 160;
            // 
            // colAchievementDescription
            // 
            this.colAchievementDescription.Text = "Description";
            this.colAchievementDescription.Width = 190;
            // 
            // imgAchievementsPreview
            // 
            this.imgAchievementsPreview.ColorDepth = System.Windows.Forms.ColorDepth.Depth32Bit;
            this.imgAchievementsPreview.ImageSize = new System.Drawing.Size(32, 32);
            this.imgAchievementsPreview.TransparentColor = System.Drawing.Color.Transparent;
            // 
            // lblAchievementsPreview
            // 
            this.lblAchievementsPreview.AutoSize = true;
            this.lblAchievementsPreview.Location = new System.Drawing.Point(6, 18);
            this.lblAchievementsPreview.Name = "lblAchievementsPreview";
            this.lblAchievementsPreview.Size = new System.Drawing.Size(0, 13);
            this.lblAchievementsPreview.TabIndex = 21;
            this.lblAchievementsPreview.Visible = false;
            // 
            // lblAchievementsFilter
            // 
            this.lblAchievementsFilter.AutoSize = true;
            this.lblAchievementsFilter.Location = new System.Drawing.Point(6, 542);
            this.lblAchievementsFilter.Name = "lblAchievementsFilter";
            this.lblAchievementsFilter.Size = new System.Drawing.Size(32, 13);
            this.lblAchievementsFilter.TabIndex = 19;
            this.lblAchievementsFilter.Text = "Filter:";
            // 
            // txtAchievementsFilter
            // 
            this.txtAchievementsFilter.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtAchievementsFilter.Location = new System.Drawing.Point(44, 539);
            this.txtAchievementsFilter.Name = "txtAchievementsFilter";
            this.txtAchievementsFilter.Size = new System.Drawing.Size(256, 20);
            this.txtAchievementsFilter.TabIndex = 1;
            this.txtAchievementsFilter.TextChanged += new System.EventHandler(this.TxtAchievementsFilter_TextChanged);
            // 
            // btnRefreshAchievements
            // 
            this.btnRefreshAchievements.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRefreshAchievements.Location = new System.Drawing.Point(306, 537);
            this.btnRefreshAchievements.Name = "btnRefreshAchievements";
            this.btnRefreshAchievements.Size = new System.Drawing.Size(75, 23);
            this.btnRefreshAchievements.TabIndex = 2;
            this.btnRefreshAchievements.Text = "Refresh";
            this.btnRefreshAchievements.UseVisualStyleBackColor = true;
            this.btnRefreshAchievements.Click += new System.EventHandler(this.OnRefreshAchievements_Click);
            // 
            // grpCustomStats
            // 
            this.grpCustomStats.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpCustomStats.Controls.Add(this.pnlStatsDisplayScroll);
            this.grpCustomStats.Controls.Add(this.btnRefreshStats);
            this.grpCustomStats.Location = new System.Drawing.Point(8, 357);
            this.grpCustomStats.Name = "grpCustomStats";
            this.grpCustomStats.Size = new System.Drawing.Size(272, 222);
            this.grpCustomStats.TabIndex = 2;
            this.grpCustomStats.TabStop = false;
            this.grpCustomStats.Text = "Stats definitions (JSON)";
            // 
            // pnlStatsDisplayScroll
            // 
            this.pnlStatsDisplayScroll.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pnlStatsDisplayScroll.AutoScroll = true;
            this.pnlStatsDisplayScroll.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pnlStatsDisplayScroll.Controls.Add(this.lblCustomStatsDisplay);
            this.pnlStatsDisplayScroll.Location = new System.Drawing.Point(6, 19);
            this.pnlStatsDisplayScroll.Name = "pnlStatsDisplayScroll";
            this.pnlStatsDisplayScroll.Size = new System.Drawing.Size(260, 167);
            this.pnlStatsDisplayScroll.TabIndex = 0;
            // 
            // lblCustomStatsDisplay
            // 
            this.lblCustomStatsDisplay.AutoSize = true;
            this.lblCustomStatsDisplay.Location = new System.Drawing.Point(0, 0);
            this.lblCustomStatsDisplay.Margin = new System.Windows.Forms.Padding(4);
            this.lblCustomStatsDisplay.Name = "lblCustomStatsDisplay";
            this.lblCustomStatsDisplay.Padding = new System.Windows.Forms.Padding(4);
            this.lblCustomStatsDisplay.Size = new System.Drawing.Size(92, 21);
            this.lblCustomStatsDisplay.TabIndex = 0;
            this.lblCustomStatsDisplay.Text = "No stats loaded.";
            // 
            // btnRefreshStats
            // 
            this.btnRefreshStats.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRefreshStats.Location = new System.Drawing.Point(191, 193);
            this.btnRefreshStats.Margin = new System.Windows.Forms.Padding(3, 4, 3, 3);
            this.btnRefreshStats.Name = "btnRefreshStats";
            this.btnRefreshStats.Size = new System.Drawing.Size(75, 23);
            this.btnRefreshStats.TabIndex = 1;
            this.btnRefreshStats.Text = "Refresh";
            this.btnRefreshStats.UseVisualStyleBackColor = true;
            this.btnRefreshStats.Click += new System.EventHandler(this.OnRefreshStats_Click);
            // 
            // grpStatsSettings
            // 
            this.grpStatsSettings.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpStatsSettings.Controls.Add(this.btnBrowseSteamGameStatsReportsDir);
            this.grpStatsSettings.Controls.Add(this.txtSteamGameStatsReportsDir);
            this.grpStatsSettings.Controls.Add(this.lblSteamGameStatsReportsDir);
            this.grpStatsSettings.Controls.Add(this.chkStats);
            this.grpStatsSettings.Controls.Add(this.chkRecordPlaytime);
            this.grpStatsSettings.Controls.Add(this.chkDisableLeaderboardsCreateUnknown);
            this.grpStatsSettings.Controls.Add(this.chkAllowUnknownStats);
            this.grpStatsSettings.Location = new System.Drawing.Point(8, 166);
            this.grpStatsSettings.Name = "grpStatsSettings";
            this.grpStatsSettings.Size = new System.Drawing.Size(272, 185);
            this.grpStatsSettings.TabIndex = 1;
            this.grpStatsSettings.TabStop = false;
            this.grpStatsSettings.Text = "Stats options";
            // 
            // btnBrowseSteamGameStatsReportsDir
            // 
            this.btnBrowseSteamGameStatsReportsDir.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseSteamGameStatsReportsDir.Location = new System.Drawing.Point(224, 143);
            this.btnBrowseSteamGameStatsReportsDir.Name = "btnBrowseSteamGameStatsReportsDir";
            this.btnBrowseSteamGameStatsReportsDir.Size = new System.Drawing.Size(23, 23);
            this.btnBrowseSteamGameStatsReportsDir.TabIndex = 7;
            this.btnBrowseSteamGameStatsReportsDir.Text = "🔍";
            this.btnBrowseSteamGameStatsReportsDir.UseVisualStyleBackColor = true;
            this.btnBrowseSteamGameStatsReportsDir.Click += new System.EventHandler(this.OnBrowseSteamGameStatsReportsDir_Click);
            // 
            // txtSteamGameStatsReportsDir
            // 
            this.txtSteamGameStatsReportsDir.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtSteamGameStatsReportsDir.Location = new System.Drawing.Point(18, 145);
            this.txtSteamGameStatsReportsDir.Name = "txtSteamGameStatsReportsDir";
            this.txtSteamGameStatsReportsDir.Size = new System.Drawing.Size(200, 20);
            this.txtSteamGameStatsReportsDir.TabIndex = 6;
            // 
            // lblSteamGameStatsReportsDir
            // 
            this.lblSteamGameStatsReportsDir.AutoSize = true;
            this.lblSteamGameStatsReportsDir.Location = new System.Drawing.Point(15, 129);
            this.lblSteamGameStatsReportsDir.Name = "lblSteamGameStatsReportsDir";
            this.lblSteamGameStatsReportsDir.Size = new System.Drawing.Size(183, 13);
            this.lblSteamGameStatsReportsDir.TabIndex = 5;
            this.lblSteamGameStatsReportsDir.Text = "Steam Game Stats Reports Directory:";
            // 
            // chkStats
            // 
            this.chkStats.AutoSize = true;
            this.chkStats.Location = new System.Drawing.Point(18, 23);
            this.chkStats.Name = "chkStats";
            this.chkStats.Size = new System.Drawing.Size(50, 17);
            this.chkStats.TabIndex = 0;
            this.chkStats.Text = "Stats";
            this.chkStats.UseVisualStyleBackColor = true;
            this.chkStats.Visible = false;
            // 
            // chkRecordPlaytime
            // 
            this.chkRecordPlaytime.AutoSize = true;
            this.chkRecordPlaytime.Location = new System.Drawing.Point(18, 46);
            this.chkRecordPlaytime.Name = "chkRecordPlaytime";
            this.chkRecordPlaytime.Size = new System.Drawing.Size(103, 17);
            this.chkRecordPlaytime.TabIndex = 1;
            this.chkRecordPlaytime.Text = "Record Playtime";
            this.chkRecordPlaytime.UseVisualStyleBackColor = true;
            // 
            // chkDisableLeaderboardsCreateUnknown
            // 
            this.chkDisableLeaderboardsCreateUnknown.AutoSize = true;
            this.chkDisableLeaderboardsCreateUnknown.Location = new System.Drawing.Point(18, 92);
            this.chkDisableLeaderboardsCreateUnknown.Name = "chkDisableLeaderboardsCreateUnknown";
            this.chkDisableLeaderboardsCreateUnknown.Size = new System.Drawing.Size(212, 17);
            this.chkDisableLeaderboardsCreateUnknown.TabIndex = 3;
            this.chkDisableLeaderboardsCreateUnknown.Text = "Disable Leaderboards Create Unknown";
            this.chkDisableLeaderboardsCreateUnknown.UseVisualStyleBackColor = true;
            // 
            // chkAllowUnknownStats
            // 
            this.chkAllowUnknownStats.AutoSize = true;
            this.chkAllowUnknownStats.Location = new System.Drawing.Point(18, 69);
            this.chkAllowUnknownStats.Name = "chkAllowUnknownStats";
            this.chkAllowUnknownStats.Size = new System.Drawing.Size(127, 17);
            this.chkAllowUnknownStats.TabIndex = 2;
            this.chkAllowUnknownStats.Text = "Allow Unknown Stats";
            this.chkAllowUnknownStats.UseVisualStyleBackColor = true;
            // 
            // grpOtherStatsSettings
            // 
            this.grpOtherStatsSettings.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpOtherStatsSettings.Controls.Add(this.lblIconsPerIteration);
            this.grpOtherStatsSettings.Controls.Add(this.numIconsPerIteration);
            this.grpOtherStatsSettings.Controls.Add(this.chkAchievementsBypass);
            this.grpOtherStatsSettings.Controls.Add(this.chkSaveOnlyHigherStatAchievementProgress);
            this.grpOtherStatsSettings.Controls.Add(this.chkStatAchievementProgressFunctionality);
            this.grpOtherStatsSettings.Location = new System.Drawing.Point(8, 10);
            this.grpOtherStatsSettings.Name = "grpOtherStatsSettings";
            this.grpOtherStatsSettings.Size = new System.Drawing.Size(272, 150);
            this.grpOtherStatsSettings.TabIndex = 0;
            this.grpOtherStatsSettings.TabStop = false;
            this.grpOtherStatsSettings.Text = "Achievement behavior";
            // 
            // lblIconsPerIteration
            // 
            this.lblIconsPerIteration.AutoSize = true;
            this.lblIconsPerIteration.Location = new System.Drawing.Point(20, 108);
            this.lblIconsPerIteration.Name = "lblIconsPerIteration";
            this.lblIconsPerIteration.Size = new System.Drawing.Size(95, 13);
            this.lblIconsPerIteration.TabIndex = 3;
            this.lblIconsPerIteration.Text = "Icons per Iteration:";
            // 
            // numIconsPerIteration
            // 
            this.numIconsPerIteration.Location = new System.Drawing.Point(150, 106);
            this.numIconsPerIteration.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numIconsPerIteration.Name = "numIconsPerIteration";
            this.numIconsPerIteration.Size = new System.Drawing.Size(60, 20);
            this.numIconsPerIteration.TabIndex = 4;
            this.numIconsPerIteration.Value = new decimal(new int[] {
            5,
            0,
            0,
            0});
            // 
            // chkAchievementsBypass
            // 
            this.chkAchievementsBypass.AutoSize = true;
            this.chkAchievementsBypass.Location = new System.Drawing.Point(20, 78);
            this.chkAchievementsBypass.Name = "chkAchievementsBypass";
            this.chkAchievementsBypass.Size = new System.Drawing.Size(130, 17);
            this.chkAchievementsBypass.TabIndex = 2;
            this.chkAchievementsBypass.Text = "Achievements Bypass";
            this.chkAchievementsBypass.UseVisualStyleBackColor = true;
            // 
            // chkSaveOnlyHigherStatAchievementProgress
            // 
            this.chkSaveOnlyHigherStatAchievementProgress.AutoSize = true;
            this.chkSaveOnlyHigherStatAchievementProgress.Location = new System.Drawing.Point(20, 55);
            this.chkSaveOnlyHigherStatAchievementProgress.Name = "chkSaveOnlyHigherStatAchievementProgress";
            this.chkSaveOnlyHigherStatAchievementProgress.Size = new System.Drawing.Size(240, 17);
            this.chkSaveOnlyHigherStatAchievementProgress.TabIndex = 1;
            this.chkSaveOnlyHigherStatAchievementProgress.Text = "Save Only Higher Stat Achievement Progress";
            this.chkSaveOnlyHigherStatAchievementProgress.UseVisualStyleBackColor = true;
            // 
            // chkStatAchievementProgressFunctionality
            // 
            this.chkStatAchievementProgressFunctionality.AutoSize = true;
            this.chkStatAchievementProgressFunctionality.Location = new System.Drawing.Point(20, 30);
            this.chkStatAchievementProgressFunctionality.Name = "chkStatAchievementProgressFunctionality";
            this.chkStatAchievementProgressFunctionality.Size = new System.Drawing.Size(216, 17);
            this.chkStatAchievementProgressFunctionality.TabIndex = 0;
            this.chkStatAchievementProgressFunctionality.Text = "Stat Achievement Progress Functionality";
            this.chkStatAchievementProgressFunctionality.UseVisualStyleBackColor = true;
            // 
            // tabServerMultiplayer
            // 
            this.tabServerMultiplayer.AutoScroll = true;
            this.tabServerMultiplayer.Controls.Add(this.grpNetworkSettings);
            this.tabServerMultiplayer.Controls.Add(this.grpMatchmakingSettings);
            this.tabServerMultiplayer.Location = new System.Drawing.Point(4, 22);
            this.tabServerMultiplayer.Name = "tabServerMultiplayer";
            this.tabServerMultiplayer.Padding = new System.Windows.Forms.Padding(3);
            this.tabServerMultiplayer.Size = new System.Drawing.Size(681, 593);
            this.tabServerMultiplayer.TabIndex = 5;
            this.tabServerMultiplayer.Text = "Multiplayer and network";
            this.tabServerMultiplayer.UseVisualStyleBackColor = true;
            // 
            // grpNetworkSettings
            // 
            this.grpNetworkSettings.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpNetworkSettings.Controls.Add(this.numOldP2PPacketSharingMode);
            this.grpNetworkSettings.Controls.Add(this.lblOldP2PPacketSharingMode);
            this.grpNetworkSettings.Controls.Add(this.chkOffline);
            this.grpNetworkSettings.Controls.Add(this.chkDisableNetworking);
            this.grpNetworkSettings.Controls.Add(this.chkShareLeaderboardsOverNetwork);
            this.grpNetworkSettings.Controls.Add(this.numForcePort);
            this.grpNetworkSettings.Controls.Add(this.chkDisableLanOnly);
            this.grpNetworkSettings.Controls.Add(this.lblForcePort);
            this.grpNetworkSettings.Controls.Add(this.chkDisableSharingStatsWithGameserver);
            this.grpNetworkSettings.Controls.Add(this.chkDisableSourceQuery);
            this.grpNetworkSettings.Controls.Add(this.chkDisableLobbyCreation);
            this.grpNetworkSettings.Controls.Add(this.chkDownloadSteamhttpRequests);
            this.grpNetworkSettings.Location = new System.Drawing.Point(20, 10);
            this.grpNetworkSettings.Name = "grpNetworkSettings";
            this.grpNetworkSettings.Size = new System.Drawing.Size(640, 214);
            this.grpNetworkSettings.TabIndex = 0;
            this.grpNetworkSettings.TabStop = false;
            this.grpNetworkSettings.Text = "Connectivity";
            // 
            // numOldP2PPacketSharingMode
            // 
            this.numOldP2PPacketSharingMode.Location = new System.Drawing.Point(164, 172);
            this.numOldP2PPacketSharingMode.Name = "numOldP2PPacketSharingMode";
            this.numOldP2PPacketSharingMode.Size = new System.Drawing.Size(68, 20);
            this.numOldP2PPacketSharingMode.TabIndex = 6;
            // 
            // lblOldP2PPacketSharingMode
            // 
            this.lblOldP2PPacketSharingMode.AutoSize = true;
            this.lblOldP2PPacketSharingMode.Location = new System.Drawing.Point(17, 174);
            this.lblOldP2PPacketSharingMode.Name = "lblOldP2PPacketSharingMode";
            this.lblOldP2PPacketSharingMode.Size = new System.Drawing.Size(141, 13);
            this.lblOldP2PPacketSharingMode.TabIndex = 13;
            this.lblOldP2PPacketSharingMode.Text = "Legacy P2P packet sharing:";
            // 
            // chkOffline
            // 
            this.chkOffline.AutoSize = true;
            this.chkOffline.Location = new System.Drawing.Point(20, 118);
            this.chkOffline.Name = "chkOffline";
            this.chkOffline.Size = new System.Drawing.Size(129, 17);
            this.chkOffline.TabIndex = 4;
            this.chkOffline.Text = "Offline (no Steam link)";
            this.chkOffline.UseVisualStyleBackColor = true;
            // 
            // chkDisableNetworking
            // 
            this.chkDisableNetworking.AutoSize = true;
            this.chkDisableNetworking.Location = new System.Drawing.Point(20, 22);
            this.chkDisableNetworking.Name = "chkDisableNetworking";
            this.chkDisableNetworking.Size = new System.Drawing.Size(118, 17);
            this.chkDisableNetworking.TabIndex = 0;
            this.chkDisableNetworking.Text = "Disable Networking";
            this.chkDisableNetworking.UseVisualStyleBackColor = true;
            // 
            // chkShareLeaderboardsOverNetwork
            // 
            this.chkShareLeaderboardsOverNetwork.AutoSize = true;
            this.chkShareLeaderboardsOverNetwork.Location = new System.Drawing.Point(330, 22);
            this.chkShareLeaderboardsOverNetwork.Name = "chkShareLeaderboardsOverNetwork";
            this.chkShareLeaderboardsOverNetwork.Size = new System.Drawing.Size(191, 17);
            this.chkShareLeaderboardsOverNetwork.TabIndex = 7;
            this.chkShareLeaderboardsOverNetwork.Text = "Share Leaderboards Over Network";
            this.chkShareLeaderboardsOverNetwork.UseVisualStyleBackColor = true;
            // 
            // numForcePort
            // 
            this.numForcePort.Location = new System.Drawing.Point(164, 146);
            this.numForcePort.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this.numForcePort.Minimum = new decimal(new int[] {
            1024,
            0,
            0,
            0});
            this.numForcePort.Name = "numForcePort";
            this.numForcePort.Size = new System.Drawing.Size(68, 20);
            this.numForcePort.TabIndex = 11;
            this.numForcePort.Value = new decimal(new int[] {
            47584,
            0,
            0,
            0});
            // 
            // chkDisableLanOnly
            // 
            this.chkDisableLanOnly.AutoSize = true;
            this.chkDisableLanOnly.Location = new System.Drawing.Point(20, 46);
            this.chkDisableLanOnly.Name = "chkDisableLanOnly";
            this.chkDisableLanOnly.Size = new System.Drawing.Size(109, 17);
            this.chkDisableLanOnly.TabIndex = 2;
            this.chkDisableLanOnly.Text = "Disable LAN Only";
            this.chkDisableLanOnly.UseVisualStyleBackColor = true;
            // 
            // lblForcePort
            // 
            this.lblForcePort.AutoSize = true;
            this.lblForcePort.Location = new System.Drawing.Point(20, 148);
            this.lblForcePort.Name = "lblForcePort";
            this.lblForcePort.Size = new System.Drawing.Size(59, 13);
            this.lblForcePort.TabIndex = 4;
            this.lblForcePort.Text = "Listen port:";
            // 
            // chkDisableSharingStatsWithGameserver
            // 
            this.chkDisableSharingStatsWithGameserver.AutoSize = true;
            this.chkDisableSharingStatsWithGameserver.Location = new System.Drawing.Point(20, 70);
            this.chkDisableSharingStatsWithGameserver.Name = "chkDisableSharingStatsWithGameserver";
            this.chkDisableSharingStatsWithGameserver.Size = new System.Drawing.Size(212, 17);
            this.chkDisableSharingStatsWithGameserver.TabIndex = 2;
            this.chkDisableSharingStatsWithGameserver.Text = "Disable Sharing Stats With Gameserver";
            this.chkDisableSharingStatsWithGameserver.UseVisualStyleBackColor = true;
            // 
            // chkDisableSourceQuery
            // 
            this.chkDisableSourceQuery.AutoSize = true;
            this.chkDisableSourceQuery.Location = new System.Drawing.Point(20, 94);
            this.chkDisableSourceQuery.Name = "chkDisableSourceQuery";
            this.chkDisableSourceQuery.Size = new System.Drawing.Size(129, 17);
            this.chkDisableSourceQuery.TabIndex = 3;
            this.chkDisableSourceQuery.Text = "Disable Source Query";
            this.chkDisableSourceQuery.UseVisualStyleBackColor = true;
            // 
            // chkDisableLobbyCreation
            // 
            this.chkDisableLobbyCreation.AutoSize = true;
            this.chkDisableLobbyCreation.Location = new System.Drawing.Point(330, 46);
            this.chkDisableLobbyCreation.Name = "chkDisableLobbyCreation";
            this.chkDisableLobbyCreation.Size = new System.Drawing.Size(135, 17);
            this.chkDisableLobbyCreation.TabIndex = 8;
            this.chkDisableLobbyCreation.Text = "Disable Lobby Creation";
            this.chkDisableLobbyCreation.UseVisualStyleBackColor = true;
            // 
            // chkDownloadSteamhttpRequests
            // 
            this.chkDownloadSteamhttpRequests.AutoSize = true;
            this.chkDownloadSteamhttpRequests.Location = new System.Drawing.Point(330, 70);
            this.chkDownloadSteamhttpRequests.Name = "chkDownloadSteamhttpRequests";
            this.chkDownloadSteamhttpRequests.Size = new System.Drawing.Size(187, 17);
            this.chkDownloadSteamhttpRequests.TabIndex = 9;
            this.chkDownloadSteamhttpRequests.Text = "Download Steam HTTP Requests";
            this.chkDownloadSteamhttpRequests.UseVisualStyleBackColor = true;
            // 
            // grpMatchmakingSettings
            // 
            this.grpMatchmakingSettings.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpMatchmakingSettings.Controls.Add(this.chkBlockUnknownClients);
            this.grpMatchmakingSettings.Controls.Add(this.chkMatchmaking);
            this.grpMatchmakingSettings.Controls.Add(this.chkImmediateGameserverStats);
            this.grpMatchmakingSettings.Controls.Add(this.chkMatchmakingServerListActualType);
            this.grpMatchmakingSettings.Controls.Add(this.chkMatchmakingServerDetailsViaSourceQuery);
            this.grpMatchmakingSettings.Location = new System.Drawing.Point(20, 230);
            this.grpMatchmakingSettings.Name = "grpMatchmakingSettings";
            this.grpMatchmakingSettings.Size = new System.Drawing.Size(640, 120);
            this.grpMatchmakingSettings.TabIndex = 1;
            this.grpMatchmakingSettings.TabStop = false;
            this.grpMatchmakingSettings.Text = "Matchmaking";
            // 
            // chkBlockUnknownClients
            // 
            this.chkBlockUnknownClients.AutoSize = true;
            this.chkBlockUnknownClients.Location = new System.Drawing.Point(330, 50);
            this.chkBlockUnknownClients.Name = "chkBlockUnknownClients";
            this.chkBlockUnknownClients.Size = new System.Drawing.Size(136, 17);
            this.chkBlockUnknownClients.TabIndex = 3;
            this.chkBlockUnknownClients.Text = "Block Unknown Clients";
            this.chkBlockUnknownClients.UseVisualStyleBackColor = true;
            // 
            // chkMatchmaking
            // 
            this.chkMatchmaking.AutoSize = true;
            this.chkMatchmaking.Location = new System.Drawing.Point(20, 30);
            this.chkMatchmaking.Name = "chkMatchmaking";
            this.chkMatchmaking.Size = new System.Drawing.Size(90, 17);
            this.chkMatchmaking.TabIndex = 0;
            this.chkMatchmaking.Text = "Matchmaking";
            this.chkMatchmaking.UseVisualStyleBackColor = true;
            // 
            // chkImmediateGameserverStats
            // 
            this.chkImmediateGameserverStats.AutoSize = true;
            this.chkImmediateGameserverStats.Location = new System.Drawing.Point(200, 30);
            this.chkImmediateGameserverStats.Name = "chkImmediateGameserverStats";
            this.chkImmediateGameserverStats.Size = new System.Drawing.Size(161, 17);
            this.chkImmediateGameserverStats.TabIndex = 1;
            this.chkImmediateGameserverStats.Text = "Immediate Gameserver Stats";
            this.chkImmediateGameserverStats.UseVisualStyleBackColor = true;
            // 
            // chkMatchmakingServerListActualType
            // 
            this.chkMatchmakingServerListActualType.AutoSize = true;
            this.chkMatchmakingServerListActualType.Location = new System.Drawing.Point(20, 55);
            this.chkMatchmakingServerListActualType.Name = "chkMatchmakingServerListActualType";
            this.chkMatchmakingServerListActualType.Size = new System.Drawing.Size(203, 17);
            this.chkMatchmakingServerListActualType.TabIndex = 2;
            this.chkMatchmakingServerListActualType.Text = "Matchmaking Server List Actual Type";
            this.chkMatchmakingServerListActualType.UseVisualStyleBackColor = true;
            // 
            // chkMatchmakingServerDetailsViaSourceQuery
            // 
            this.chkMatchmakingServerDetailsViaSourceQuery.AutoSize = true;
            this.chkMatchmakingServerDetailsViaSourceQuery.Location = new System.Drawing.Point(20, 80);
            this.chkMatchmakingServerDetailsViaSourceQuery.Name = "chkMatchmakingServerDetailsViaSourceQuery";
            this.chkMatchmakingServerDetailsViaSourceQuery.Size = new System.Drawing.Size(245, 17);
            this.chkMatchmakingServerDetailsViaSourceQuery.TabIndex = 3;
            this.chkMatchmakingServerDetailsViaSourceQuery.Text = "Matchmaking Server Details Via Source Query";
            this.chkMatchmakingServerDetailsViaSourceQuery.UseVisualStyleBackColor = true;
            // 
            // tabInventory
            // 
            this.tabInventory.Controls.Add(this.grpInventoryEditor);
            this.tabInventory.Location = new System.Drawing.Point(4, 22);
            this.tabInventory.Name = "tabInventory";
            this.tabInventory.Padding = new System.Windows.Forms.Padding(3);
            this.tabInventory.Size = new System.Drawing.Size(681, 593);
            this.tabInventory.TabIndex = 6;
            this.tabInventory.Text = "Inventory";
            this.tabInventory.UseVisualStyleBackColor = true;
            // 
            // grpInventoryEditor
            // 
            this.grpInventoryEditor.Controls.Add(this.lblInventoryHint);
            this.grpInventoryEditor.Controls.Add(this.pnlInventoryButtons);
            this.grpInventoryEditor.Controls.Add(this.lstInventoryItems);
            this.grpInventoryEditor.Controls.Add(this.txtInventoryRaw);
            this.grpInventoryEditor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpInventoryEditor.Location = new System.Drawing.Point(3, 3);
            this.grpInventoryEditor.Name = "grpInventoryEditor";
            this.grpInventoryEditor.Padding = new System.Windows.Forms.Padding(8);
            this.grpInventoryEditor.Size = new System.Drawing.Size(675, 587);
            this.grpInventoryEditor.TabIndex = 0;
            this.grpInventoryEditor.TabStop = false;
            this.grpInventoryEditor.Text = "items.json";
            // 
            // lblInventoryHint
            // 
            this.lblInventoryHint.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblInventoryHint.Location = new System.Drawing.Point(11, 24);
            this.lblInventoryHint.Name = "lblInventoryHint";
            this.lblInventoryHint.Size = new System.Drawing.Size(653, 40);
            this.lblInventoryHint.TabIndex = 0;
            this.lblInventoryHint.Text = "Item definitions (keyed by itemdefid). Reload from disk after editing items.json " +
    "elsewhere; double-click Qty (or F2) to change quantity only.";
            // 
            // pnlInventoryButtons
            // 
            this.pnlInventoryButtons.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pnlInventoryButtons.Controls.Add(this.chkUse32BitInventoryItemIds);
            this.pnlInventoryButtons.Controls.Add(this.btnReloadInventoryFromDisk);
            this.pnlInventoryButtons.Location = new System.Drawing.Point(11, 68);
            this.pnlInventoryButtons.Name = "pnlInventoryButtons";
            this.pnlInventoryButtons.Size = new System.Drawing.Size(653, 32);
            this.pnlInventoryButtons.TabIndex = 1;
            // 
            // chkUse32BitInventoryItemIds
            // 
            this.chkUse32BitInventoryItemIds.AutoSize = true;
            this.chkUse32BitInventoryItemIds.Location = new System.Drawing.Point(160, 9);
            this.chkUse32BitInventoryItemIds.Name = "chkUse32BitInventoryItemIds";
            this.chkUse32BitInventoryItemIds.Size = new System.Drawing.Size(163, 17);
            this.chkUse32BitInventoryItemIds.TabIndex = 1;
            this.chkUse32BitInventoryItemIds.Text = "Use 32-bit Inventory Item IDs";
            this.chkUse32BitInventoryItemIds.UseVisualStyleBackColor = true;
            // 
            // btnReloadInventoryFromDisk
            // 
            this.btnReloadInventoryFromDisk.Location = new System.Drawing.Point(0, 4);
            this.btnReloadInventoryFromDisk.Name = "btnReloadInventoryFromDisk";
            this.btnReloadInventoryFromDisk.Size = new System.Drawing.Size(140, 28);
            this.btnReloadInventoryFromDisk.TabIndex = 0;
            this.btnReloadInventoryFromDisk.Text = "Reload from disk";
            this.btnReloadInventoryFromDisk.UseVisualStyleBackColor = true;
            // 
            // lstInventoryItems
            // 
            this.lstInventoryItems.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lstInventoryItems.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colInventoryItemDefId,
            this.colInventoryDisplayName,
            this.colInventoryQuantity,
            this.colInventoryType,
            this.colInventoryFiller});
            this.lstInventoryItems.FullRowSelect = true;
            this.lstInventoryItems.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.lstInventoryItems.HideSelection = false;
            this.lstInventoryItems.Location = new System.Drawing.Point(11, 104);
            this.lstInventoryItems.Name = "lstInventoryItems";
            this.lstInventoryItems.OwnerDraw = true;
            this.lstInventoryItems.ShowItemToolTips = true;
            this.lstInventoryItems.Size = new System.Drawing.Size(653, 470);
            this.lstInventoryItems.TabIndex = 2;
            this.lstInventoryItems.UseCompatibleStateImageBehavior = false;
            this.lstInventoryItems.View = System.Windows.Forms.View.Details;
            // 
            // colInventoryItemDefId
            // 
            this.colInventoryItemDefId.Text = "Item ID";
            this.colInventoryItemDefId.Width = 90;
            // 
            // colInventoryDisplayName
            // 
            this.colInventoryDisplayName.Text = "Name";
            this.colInventoryDisplayName.Width = 220;
            // 
            // colInventoryQuantity
            // 
            this.colInventoryQuantity.Text = "Qty";
            this.colInventoryQuantity.Width = 52;
            // 
            // colInventoryType
            // 
            this.colInventoryType.Text = "Type";
            this.colInventoryType.Width = 88;
            // 
            // colInventoryFiller
            // 
            this.colInventoryFiller.Text = "";
            this.colInventoryFiller.Width = 100;
            // 
            // txtInventoryRaw
            // 
            this.txtInventoryRaw.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtInventoryRaw.Location = new System.Drawing.Point(11, 574);
            this.txtInventoryRaw.Multiline = true;
            this.txtInventoryRaw.Name = "txtInventoryRaw";
            this.txtInventoryRaw.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtInventoryRaw.Size = new System.Drawing.Size(653, 0);
            this.txtInventoryRaw.TabIndex = 3;
            this.txtInventoryRaw.Visible = false;
            // 
            // tabMods
            // 
            this.tabMods.Controls.Add(this.grpMods);
            this.tabMods.Location = new System.Drawing.Point(4, 22);
            this.tabMods.Name = "tabMods";
            this.tabMods.Padding = new System.Windows.Forms.Padding(3);
            this.tabMods.Size = new System.Drawing.Size(681, 593);
            this.tabMods.TabIndex = 6;
            this.tabMods.Text = "Mods";
            this.tabMods.UseVisualStyleBackColor = true;
            // 
            // grpMods
            // 
            this.grpMods.Controls.Add(this.lblModsHint);
            this.grpMods.Controls.Add(this.pnlModsToolbar);
            this.grpMods.Controls.Add(this.lstModsSummary);
            this.grpMods.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpMods.Location = new System.Drawing.Point(3, 3);
            this.grpMods.Name = "grpMods";
            this.grpMods.Padding = new System.Windows.Forms.Padding(10);
            this.grpMods.Size = new System.Drawing.Size(675, 587);
            this.grpMods.TabIndex = 0;
            this.grpMods.TabStop = false;
            this.grpMods.Text = "Mods folder";
            // 
            // lblModsHint
            // 
            this.lblModsHint.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblModsHint.Location = new System.Drawing.Point(13, 23);
            this.lblModsHint.Name = "lblModsHint";
            this.lblModsHint.Size = new System.Drawing.Size(649, 82);
            this.lblModsHint.TabIndex = 0;
            this.lblModsHint.Text = resources.GetString("lblModsHint.Text");
            // 
            // pnlModsToolbar
            // 
            this.pnlModsToolbar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pnlModsToolbar.Controls.Add(this.btnCopyFoldersToMods);
            this.pnlModsToolbar.Controls.Add(this.btnCopyFilesToMods);
            this.pnlModsToolbar.Controls.Add(this.btnOpenModsFolder);
            this.pnlModsToolbar.Location = new System.Drawing.Point(13, 111);
            this.pnlModsToolbar.Name = "pnlModsToolbar";
            this.pnlModsToolbar.Size = new System.Drawing.Size(649, 30);
            this.pnlModsToolbar.TabIndex = 1;
            // 
            // btnCopyFoldersToMods
            // 
            this.btnCopyFoldersToMods.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCopyFoldersToMods.AutoSize = true;
            this.btnCopyFoldersToMods.Location = new System.Drawing.Point(511, 4);
            this.btnCopyFoldersToMods.Name = "btnCopyFoldersToMods";
            this.btnCopyFoldersToMods.Size = new System.Drawing.Size(135, 23);
            this.btnCopyFoldersToMods.TabIndex = 2;
            this.btnCopyFoldersToMods.Text = "Copy folder to mods…";
            this.btnCopyFoldersToMods.UseVisualStyleBackColor = true;
            this.btnCopyFoldersToMods.Click += new System.EventHandler(this.OnCopyFoldersToMods_Click);
            // 
            // btnCopyFilesToMods
            // 
            this.btnCopyFilesToMods.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCopyFilesToMods.AutoSize = true;
            this.btnCopyFilesToMods.Location = new System.Drawing.Point(382, 4);
            this.btnCopyFilesToMods.Name = "btnCopyFilesToMods";
            this.btnCopyFilesToMods.Size = new System.Drawing.Size(123, 23);
            this.btnCopyFilesToMods.TabIndex = 1;
            this.btnCopyFilesToMods.Text = "Copy files to mods…";
            this.btnCopyFilesToMods.UseVisualStyleBackColor = true;
            this.btnCopyFilesToMods.Click += new System.EventHandler(this.OnCopyFilesToMods_Click);
            // 
            // btnOpenModsFolder
            // 
            this.btnOpenModsFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOpenModsFolder.AutoSize = true;
            this.btnOpenModsFolder.Location = new System.Drawing.Point(259, 4);
            this.btnOpenModsFolder.Name = "btnOpenModsFolder";
            this.btnOpenModsFolder.Size = new System.Drawing.Size(118, 23);
            this.btnOpenModsFolder.TabIndex = 0;
            this.btnOpenModsFolder.Text = "Open mods folder";
            this.btnOpenModsFolder.UseVisualStyleBackColor = true;
            this.btnOpenModsFolder.Click += new System.EventHandler(this.OnOpenModsFolder_Click);
            // 
            // lstModsSummary
            // 
            this.lstModsSummary.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lstModsSummary.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colModsSummaryId,
            this.colModsSummaryName,
            this.colModsSummaryFiller});
            this.lstModsSummary.FullRowSelect = true;
            this.lstModsSummary.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.lstModsSummary.HideSelection = false;
            this.lstModsSummary.Location = new System.Drawing.Point(13, 147);
            this.lstModsSummary.Name = "lstModsSummary";
            this.lstModsSummary.OwnerDraw = true;
            this.lstModsSummary.ShowItemToolTips = true;
            this.lstModsSummary.Size = new System.Drawing.Size(649, 427);
            this.lstModsSummary.TabIndex = 3;
            this.lstModsSummary.UseCompatibleStateImageBehavior = false;
            this.lstModsSummary.View = System.Windows.Forms.View.Details;
            this.lstModsSummary.Resize += new System.EventHandler(this.lstModsSummary_Resize);
            // 
            // colModsSummaryId
            // 
            this.colModsSummaryId.Text = "Mod ID";
            this.colModsSummaryId.Width = 120;
            // 
            // colModsSummaryName
            // 
            this.colModsSummaryName.Text = "Name";
            this.colModsSummaryName.Width = 300;
            // 
            // colModsSummaryFiller
            // 
            this.colModsSummaryFiller.Text = "";
            this.colModsSummaryFiller.Width = 100;
            // 
            // chkShowExtraSteamLaunchOptions
            // 
            this.chkShowExtraSteamLaunchOptions.AutoSize = true;
            this.chkShowExtraSteamLaunchOptions.Location = new System.Drawing.Point(497, 23);
            this.chkShowExtraSteamLaunchOptions.Name = "chkShowExtraSteamLaunchOptions";
            this.chkShowExtraSteamLaunchOptions.Size = new System.Drawing.Size(117, 17);
            this.chkShowExtraSteamLaunchOptions.TabIndex = 3;
            this.chkShowExtraSteamLaunchOptions.Text = "Show Beta Options";
            this.chkShowExtraSteamLaunchOptions.UseVisualStyleBackColor = true;
            this.chkShowExtraSteamLaunchOptions.Visible = false;
            // 
            // chkSteamDeck
            // 
            this.chkSteamDeck.AutoSize = true;
            this.chkSteamDeck.Location = new System.Drawing.Point(323, 27);
            this.chkSteamDeck.Name = "chkSteamDeck";
            this.chkSteamDeck.Size = new System.Drawing.Size(85, 17);
            this.chkSteamDeck.TabIndex = 3;
            this.chkSteamDeck.Text = "Steam Deck";
            this.chkSteamDeck.UseVisualStyleBackColor = true;
            // 
            // chkDisableWarningBadAppId
            // 
            this.chkDisableWarningBadAppId.AutoSize = true;
            this.chkDisableWarningBadAppId.Location = new System.Drawing.Point(424, 27);
            this.chkDisableWarningBadAppId.Name = "chkDisableWarningBadAppId";
            this.chkDisableWarningBadAppId.Size = new System.Drawing.Size(179, 17);
            this.chkDisableWarningBadAppId.TabIndex = 4;
            this.chkDisableWarningBadAppId.Text = "Suppress invalid App ID warning";
            this.chkDisableWarningBadAppId.UseVisualStyleBackColor = true;
            // 
            // chkEnableSteamPreownedIds
            // 
            this.chkEnableSteamPreownedIds.AutoSize = true;
            this.chkEnableSteamPreownedIds.Location = new System.Drawing.Point(140, 30);
            this.chkEnableSteamPreownedIds.Name = "chkEnableSteamPreownedIds";
            this.chkEnableSteamPreownedIds.Size = new System.Drawing.Size(162, 17);
            this.chkEnableSteamPreownedIds.TabIndex = 3;
            this.chkEnableSteamPreownedIds.Text = "Enable Steam Preowned IDs";
            this.chkEnableSteamPreownedIds.UseVisualStyleBackColor = true;
            // 
            // tabAdvancedFeatures
            // 
            this.tabAdvancedFeatures.AutoScroll = true;
            this.tabAdvancedFeatures.Controls.Add(this.grpEmulation);
            this.tabAdvancedFeatures.Location = new System.Drawing.Point(4, 22);
            this.tabAdvancedFeatures.Name = "tabAdvancedFeatures";
            this.tabAdvancedFeatures.Padding = new System.Windows.Forms.Padding(3);
            this.tabAdvancedFeatures.Size = new System.Drawing.Size(681, 593);
            this.tabAdvancedFeatures.TabIndex = 6;
            this.tabAdvancedFeatures.Text = "Advanced";
            this.tabAdvancedFeatures.UseVisualStyleBackColor = true;
            // 
            // grpEmulation
            // 
            this.grpEmulation.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpEmulation.Controls.Add(this.chkDisableSteamoverlaygameidEnvVar);
            this.grpEmulation.Controls.Add(this.chkEnableExperimentalOverlayGame);
            this.grpEmulation.Location = new System.Drawing.Point(20, 20);
            this.grpEmulation.Name = "grpEmulation";
            this.grpEmulation.Size = new System.Drawing.Size(657, 90);
            this.grpEmulation.TabIndex = 1;
            this.grpEmulation.TabStop = false;
            this.grpEmulation.Text = "Overlay and integration";
            // 
            // chkDisableSteamoverlaygameidEnvVar
            // 
            this.chkDisableSteamoverlaygameidEnvVar.AutoSize = true;
            this.chkDisableSteamoverlaygameidEnvVar.Location = new System.Drawing.Point(330, 30);
            this.chkDisableSteamoverlaygameidEnvVar.Name = "chkDisableSteamoverlaygameidEnvVar";
            this.chkDisableSteamoverlaygameidEnvVar.Size = new System.Drawing.Size(206, 17);
            this.chkDisableSteamoverlaygameidEnvVar.TabIndex = 1;
            this.chkDisableSteamoverlaygameidEnvVar.Text = "Disable SteamOverlayGameId env var";
            this.chkDisableSteamoverlaygameidEnvVar.UseVisualStyleBackColor = true;
            // 
            // chkEnableExperimentalOverlayGame
            // 
            this.chkEnableExperimentalOverlayGame.AutoSize = true;
            this.chkEnableExperimentalOverlayGame.Location = new System.Drawing.Point(20, 30);
            this.chkEnableExperimentalOverlayGame.Name = "chkEnableExperimentalOverlayGame";
            this.chkEnableExperimentalOverlayGame.Size = new System.Drawing.Size(191, 17);
            this.chkEnableExperimentalOverlayGame.TabIndex = 0;
            this.chkEnableExperimentalOverlayGame.Text = "Enable experimental Steam overlay";
            this.chkEnableExperimentalOverlayGame.UseVisualStyleBackColor = true;
            // 
            // chkForceSteamhttpSuccess
            // 
            this.chkForceSteamhttpSuccess.AutoSize = true;
            this.chkForceSteamhttpSuccess.Location = new System.Drawing.Point(330, 94);
            this.chkForceSteamhttpSuccess.Name = "chkForceSteamhttpSuccess";
            this.chkForceSteamhttpSuccess.Size = new System.Drawing.Size(162, 17);
            this.chkForceSteamhttpSuccess.TabIndex = 12;
            this.chkForceSteamhttpSuccess.Text = "Force Steam HTTP Success";
            this.chkForceSteamhttpSuccess.UseVisualStyleBackColor = true;
            // 
            // chkFreeWeekend
            // 
            this.chkFreeWeekend.AutoSize = true;
            this.chkFreeWeekend.Location = new System.Drawing.Point(20, 142);
            this.chkFreeWeekend.Name = "chkFreeWeekend";
            this.chkFreeWeekend.Size = new System.Drawing.Size(134, 17);
            this.chkFreeWeekend.TabIndex = 5;
            this.chkFreeWeekend.Text = "Simulate free weekend";
            this.chkFreeWeekend.UseVisualStyleBackColor = true;
            // 
            // chkEnableVoiceChat
            // 
            this.chkEnableVoiceChat.AutoSize = true;
            this.chkEnableVoiceChat.Location = new System.Drawing.Point(330, 118);
            this.chkEnableVoiceChat.Name = "chkEnableVoiceChat";
            this.chkEnableVoiceChat.Size = new System.Drawing.Size(112, 17);
            this.chkEnableVoiceChat.TabIndex = 10;
            this.chkEnableVoiceChat.Text = "Enable voice chat";
            this.chkEnableVoiceChat.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(602, 635);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.OnCancel_Click);
            // 
            // btnSave
            // 
            this.btnSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSave.Location = new System.Drawing.Point(521, 635);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(75, 23);
            this.btnSave.TabIndex = 1;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.OnSave_Click);
            // 
            // toolTip
            // 
            this.toolTip.AutoPopDelay = 16000;
            this.toolTip.InitialDelay = 500;
            this.toolTip.ReshowDelay = 200;
            this.toolTip.SetToolTip(this.lblHintGameFolder, "Pick the folder that contains the whole game, not only the .exe.\r\n\r\nExample:\r\nGame folder: ...\\steamapps\\common\\Duke Nukem 3D\r\nExecutable: bin\\duke3d.exe");
            this.toolTip.SetToolTip(this.txtGameFolder, "Full path to the game install folder (usually steamapps\\common\\GameName).");
            this.toolTip.SetToolTip(this.btnBrowseGameFolder, "Browse for the game install folder.");
            this.toolTip.SetToolTip(this.txtGameExecutable, "Path to the game .exe. Can be relative to the game folder or a full path.");
            this.toolTip.SetToolTip(this.btnBrowseGameExecutable, "Browse for the game executable.");
            this.toolTip.SetToolTip(this.txtWorkingDirectory, "Folder used as the working directory when the game starts. Leave empty to use the game folder.");
            this.toolTip.SetToolTip(this.btnBrowseWorkingDirectory, "Browse for the working directory.");
            this.toolTip.SetToolTip(this.txtLaunchParameters, "Command-line arguments passed to the game, same as Steam launch options.");
            this.toolTip.SetToolTip(this.txtCustomIcon, "Optional .ico file shown for this game in the launcher library.");
            this.toolTip.SetToolTip(this.btnBrowseCustomIcon, "Browse for a custom icon file.");
            this.toolTip.SetToolTip(this.btnClearCustomIcon, "Clear the custom icon path.");
            this.toolTip.SetToolTip(this.txtAppID, "Steam App ID (the number from the store page URL).");
            this.toolTip.SetToolTip(this.btnLookupAppID, "Look up the App ID from Steam using the game name.");
            this.toolTip.SetToolTip(this.txtGameName, "Name shown in the launcher library.");
            this.toolTip.SetToolTip(this.btnLookupGameName, "Look up the game name from Steam using the App ID.");
            this.toolTip.SetToolTip(this.lblLaunchMode, "How the launcher starts this game with Goldberg.");
            this.toolTip.SetToolTip(this.rdoLaunchSteamClient, "Use Goldberg Steam client mode.\r\n\r\nCopies steamclient DLLs and sets registry so the game uses the emulator.");
            this.toolTip.SetToolTip(this.rdoLaunchExperimentalMode, "Use experimental steam_api beside the game.\r\n\r\nReplaces steam_api.dll in the game folder and links steam_settings.");
            this.toolTip.SetToolTip(this.rdoLaunchSteamDll, "Copy Steam.dll next to the game .exe.\r\n\r\nFor older games that load a local Steam.dll without the overlay.");
            this.toolTip.SetToolTip(this.rdoLaunchNoEmulation, "Start the game .exe directly.\r\n\r\nNo Goldberg DLLs or registry setup.");
            this.toolTip.SetToolTip(this.btnRestoreDlls, "Replace modified steam_api DLLs with a clean copy from elsewhere in the game folder.");
            this.toolTip.SetToolTip(this.lnkLauncherOptionsSteamDb, "Open SteamDB launch options for this App ID in your browser.");
            this.toolTip.SetToolTip(this.lnkSteamCmdLineOptionsValveWiki, "Open Valve\'s list of Steam command-line options in your browser.");
            this.toolTip.SetToolTip(this.cmbSteamLaunchOptions, "Pick a launch option from Steam assets or your saved presets.");
            this.toolTip.SetToolTip(this.btnRemoveUserLaunchOption, "Remove the selected saved launch option.");
            this.toolTip.SetToolTip(this.txtUserLaunchOptionName, "Name for saving the current launch parameters as a preset.");
            this.toolTip.SetToolTip(this.btnSaveUserLaunchOption, "Save the current launch parameters under this name.");
            this.toolTip.SetToolTip(this.chkUnlockAllDLC, "Tell the game every DLC is owned.\r\n\r\nTurn off to use only the DLC list below.\r\n\r\nDefault: on");
            this.toolTip.SetToolTip(this.btnFindDLCs, "Fetch DLC App IDs from Steam and fill the list.");
            this.toolTip.SetToolTip(this.txtDLCList, "DLC entries as AppID=name.\r\n\r\nWritten to configs.app.ini when unlock all is off.");
            this.toolTip.SetToolTip(this.chkBetaBranch, "Tell the game it is running on a beta branch.\r\n\r\nDefault: off");
            this.toolTip.SetToolTip(this.txtBetaBranchName, "Beta branch name. Must exist in branches.json or the public branch is used.\r\n\r\nDefault: public");
            this.toolTip.SetToolTip(this.lstSubscribedGroups, "Steam group IDs, one per line.\r\n\r\nSaved to subscribed_groups.txt. Some games use this for unlocks.");
            this.toolTip.SetToolTip(this.txtSubscribedGroupIdEntry, "Group ID to add. Find IDs at a Steam group page URL with /memberslistxml/?xml=1");
            this.toolTip.SetToolTip(this.btnAddSubscribedGroup, "Add the group ID to the list.");
            this.toolTip.SetToolTip(this.btnRemoveSubscribedGroup, "Remove the selected group ID.");
            this.toolTip.SetToolTip(this.lstSubscribedGroupsClans, "Clan groups as ID, name, and tag per line (tab-separated).\r\n\r\nSaved to subscribed_groups_clans.txt.");
            this.toolTip.SetToolTip(this.txtSubscribedGroupClanEntry, "Clan line to add: ID, name, and tag separated by tabs.");
            this.toolTip.SetToolTip(this.btnAddSubscribedGroupClan, "Add the clan line to the list.");
            this.toolTip.SetToolTip(this.btnRemoveSubscribedGroupClan, "Remove the selected clan row.");
            this.toolTip.SetToolTip(this.chkBlockUnknownClients, "Game servers accept only real Steam clients and known emulators.\r\n\r\nDefault: off");
            this.toolTip.SetToolTip(this.chkSteamDeck, "Tell the game it is running on a Steam Deck.\r\n\r\nDefault: off");
            this.toolTip.SetToolTip(this.txtForceAccountName, "Account name reported to the game.\r\n\r\nDefault: from global settings (gse orca)");
            this.toolTip.SetToolTip(this.txtForceSteamId, "Your Steam64 ID. Invalid values are ignored and a generated ID is used.\r\n\r\nDefault: random ID saved in global settings");
            this.toolTip.SetToolTip(this.txtUserTicket, "Base64 Steam ticket for this user (advanced).");
            this.toolTip.SetToolTip(this.txtAltSteamId, "Alternate Steam ID for encrypted save games.");
            this.toolTip.SetToolTip(this.numAltSteamIdCount, "How many calls before switching to the alternate Steam ID.\r\n\r\nDefault: 5");
            this.toolTip.SetToolTip(this.cmbForceLanguage, "Language reported to the game. Must be in supported_languages.txt.\r\n\r\nDefault: english");
            this.toolTip.SetToolTip(this.txtForceIpCountry, "Country code (ISO alpha-2) reported when the game asks for your region.\r\n\r\nDefault: US");
            this.toolTip.SetToolTip(this.txtClanTag, "Clan tag reported to the game.");
            this.toolTip.SetToolTip(this.btnRefreshStats, "Reload stats.json from disk or generate it from Steam if missing.");
            this.toolTip.SetToolTip(this.btnRefreshAchievements, "Reload achievements.json from disk or fetch from Steam.");
            this.toolTip.SetToolTip(this.txtAchievementsFilter, "Filter the achievement preview by name or description.");
            this.toolTip.SetToolTip(this.chkDisableLeaderboardsCreateUnknown, "Do not auto-create unknown leaderboards when the game looks them up.\r\n\r\nDefault: off");
            this.toolTip.SetToolTip(this.chkAllowUnknownStats, "Allow saving stats that are not listed in stats.json.\r\n\r\nDefault: off");
            this.toolTip.SetToolTip(this.chkStatAchievementProgressFunctionality, "Update achievement progress when a linked stat changes.\r\n\r\nCan cause extra disk writes and overlay popups.\r\n\r\nDefault: on");
            this.toolTip.SetToolTip(this.chkSaveOnlyHigherStatAchievementProgress, "Only save achievement progress from stats when the new value is higher.\r\n\r\nDefault: on");
            this.toolTip.SetToolTip(this.numIconsPerIteration, "How many achievement icons to load per callback (two icons per achievement).\r\n\r\nDefault: 10");
            this.toolTip.SetToolTip(this.chkRecordPlaytime, "Record play time to a playtime.txt file under GSE Saves.\r\n\r\nDefault: off");
            this.toolTip.SetToolTip(this.chkAchievementsBypass, "Always make unlock-achievement calls succeed.\r\n\r\nWorkaround for some games.\r\n\r\nDefault: off");
            this.toolTip.SetToolTip(this.txtSteamGameStatsReportsDir, "Folder where ISteamGameStats reports are saved.\r\n\r\nLeave empty to disable.\r\n\r\nDefault: empty");
            this.toolTip.SetToolTip(this.btnBrowseSteamGameStatsReportsDir, "Browse for the game stats reports folder.");
            this.toolTip.SetToolTip(this.chkDisableNetworking, "Turn off all Steam networking (lobbies, P2P, etc.).\r\n\r\nDefault: off");
            this.toolTip.SetToolTip(this.chkDisableLanOnly, "Allow the game to reach the real internet instead of LAN-only hooks.\r\n\r\nNeeded for some HTTP downloads.\r\n\r\nDefault: off");
            this.toolTip.SetToolTip(this.numForcePort, "UDP/TCP port the emulator listens on. Everyone on the LAN must use the same port.\r\n\r\nDefault: 47584");
            this.toolTip.SetToolTip(this.chkOffline, "Pretend Steam is in offline mode.\r\n\r\nDefault: off");
            this.toolTip.SetToolTip(this.chkDisableSharingStatsWithGameserver, "Do not share stats or achievements with game servers.\r\n\r\nDefault: off");
            this.toolTip.SetToolTip(this.chkDisableSourceQuery, "Do not send server details to the server browser (game servers only).\r\n\r\nDefault: off");
            this.toolTip.SetToolTip(this.chkShareLeaderboardsOverNetwork, "Share leaderboard scores with others on the same LAN (experimental).\r\n\r\nDefault: off");
            this.toolTip.SetToolTip(this.chkDisableLobbyCreation, "Block creating lobbies in Steam matchmaking.\r\n\r\nDefault: off");
            this.toolTip.SetToolTip(this.chkDownloadSteamhttpRequests, "Save Steam HTTP downloads under steam_settings\\http\\.\r\n\r\nNeeds LAN-only off and networking on.\r\n\r\nDefault: off");
            this.toolTip.SetToolTip(this.numOldP2PPacketSharingMode, "Legacy P2P packet sharing workaround.\r\n\r\nDefault: 0");
            this.toolTip.SetToolTip(this.chkMatchmaking, "Matchmaking and lobby options in configs.main.ini (below).");
            this.toolTip.SetToolTip(this.chkImmediateGameserverStats, "Sync stats with game servers immediately instead of waiting for callbacks.\r\n\r\nNot recommended.\r\n\r\nDefault: off");
            this.toolTip.SetToolTip(this.chkMatchmakingServerListActualType, "Return the real server list type (internet, friends, etc.).\r\n\r\nOff always reports LAN.\r\n\r\nNot recommended.\r\n\r\nDefault: off");
            this.toolTip.SetToolTip(this.chkMatchmakingServerDetailsViaSourceQuery, "Query servers for matchmaking details instead of LAN discovery.\r\n\r\nNot recommended; can break some games.\r\n\r\nDefault: off");
            this.toolTip.SetToolTip(this.lblInventoryHint, "Inventory item definitions live in steam_settings\\items.json.");
            this.toolTip.SetToolTip(this.btnReloadInventoryFromDisk, "Reload items.json from the game steam_settings folder.");
            this.toolTip.SetToolTip(this.txtInventoryRaw, "Raw items.json text for this game.");
            this.toolTip.SetToolTip(this.lblModsHint, "Lists files in steam_settings\\mods\\. Names use Workshop titles when a Steam API key is set.");
            this.toolTip.SetToolTip(this.btnOpenModsFolder, "Open the mods folder in File Explorer.");
            this.toolTip.SetToolTip(this.btnCopyFilesToMods, "Copy selected files into steam_settings\\mods\\.");
            this.toolTip.SetToolTip(this.btnCopyFoldersToMods, "Copy selected folders into steam_settings\\mods\\.");
            this.toolTip.SetToolTip(this.chkEnableExperimentalOverlayGame, "Enable Goldberg\'s experimental in-game overlay.\r\n\r\nMay cause crashes.\r\n\r\nDefault: off");
            this.toolTip.SetToolTip(this.chkForceSteamhttpSuccess, "Make Steam HTTP requests always report success.\r\n\r\nDefault: off");
            this.toolTip.SetToolTip(this.chkDisableSteamoverlaygameidEnvVar, "Do not set SteamOverlayGameId env var so Steam Input can work.\r\n\r\nDefault: off");
            this.toolTip.SetToolTip(this.chkEnableSteamPreownedIds, "Add many Steam apps to owned DLC and installed app lists.\r\n\r\nDefault: off");
            this.toolTip.SetToolTip(this.chkFreeWeekend, "Pretend a free-weekend player is online.\r\n\r\nSome games give extra bonuses.\r\n\r\nDefault: off");
            this.toolTip.SetToolTip(this.chkEnableVoiceChat, "Enable experimental voice chat.\r\n\r\nDefault: off");
            this.toolTip.SetToolTip(this.chkUse32BitInventoryItemIds, "Use 32-bit inventory item IDs.\r\n\r\nWorkaround for very old Team Fortress 2 builds.\r\n\r\nDefault: off");
            // 
            // GameSettingsForm
            // 
            this.AcceptButton = this.btnSave;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(689, 665);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.tabControl);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(640, 560);
            this.Name = "GameSettingsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Game Settings";
            this.Load += new System.EventHandler(this.GameSettingsForm_Load);
            this.tabControl.ResumeLayout(false);
            this.tabGameInfo.ResumeLayout(false);
            this.tabGameInfo.PerformLayout();
            this.grpPaths.ResumeLayout(false);
            this.grpPaths.PerformLayout();
            this.grpSteamLaunchOptions.ResumeLayout(false);
            this.grpSteamLaunchOptions.PerformLayout();
            this.grpBasicInfo.ResumeLayout(false);
            this.grpBasicInfo.PerformLayout();
            this.tabDLCContent.ResumeLayout(false);
            this.grpSubscribedGroups.ResumeLayout(false);
            this.grpSubscribedGroups.PerformLayout();
            this.grpDLCManagement.ResumeLayout(false);
            this.grpDLCManagement.PerformLayout();
            this.tabOtherSettings.ResumeLayout(false);
            this.grpAuthenticationSettings.ResumeLayout(false);
            this.grpAuthenticationSettings.PerformLayout();
            this.grpAdvancedAuth.ResumeLayout(false);
            this.grpAdvancedAuth.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numAltSteamIdCount)).EndInit();
            this.tabStatsAchievements.ResumeLayout(false);
            this.grpAchievementsFile.ResumeLayout(false);
            this.grpAchievementsFile.PerformLayout();
            this.grpCustomStats.ResumeLayout(false);
            this.pnlStatsDisplayScroll.ResumeLayout(false);
            this.pnlStatsDisplayScroll.PerformLayout();
            this.grpStatsSettings.ResumeLayout(false);
            this.grpStatsSettings.PerformLayout();
            this.grpOtherStatsSettings.ResumeLayout(false);
            this.grpOtherStatsSettings.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numIconsPerIteration)).EndInit();
            this.tabServerMultiplayer.ResumeLayout(false);
            this.grpNetworkSettings.ResumeLayout(false);
            this.grpNetworkSettings.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numOldP2PPacketSharingMode)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numForcePort)).EndInit();
            this.grpMatchmakingSettings.ResumeLayout(false);
            this.grpMatchmakingSettings.PerformLayout();
            this.tabInventory.ResumeLayout(false);
            this.grpInventoryEditor.ResumeLayout(false);
            this.grpInventoryEditor.PerformLayout();
            this.pnlInventoryButtons.ResumeLayout(false);
            this.pnlInventoryButtons.PerformLayout();
            this.tabMods.ResumeLayout(false);
            this.grpMods.ResumeLayout(false);
            this.pnlModsToolbar.ResumeLayout(false);
            this.pnlModsToolbar.PerformLayout();
            this.tabAdvancedFeatures.ResumeLayout(false);
            this.grpEmulation.ResumeLayout(false);
            this.grpEmulation.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabGameInfo;
        private System.Windows.Forms.TabPage tabOtherSettings;
        private System.Windows.Forms.TabPage tabDLCContent;
        private System.Windows.Forms.TabPage tabStatsAchievements;
        private System.Windows.Forms.TabPage tabAdvancedFeatures;
        private System.Windows.Forms.TabPage tabServerMultiplayer;
        private System.Windows.Forms.TabPage tabInventory;
        private System.Windows.Forms.GroupBox grpInventoryEditor;
        private System.Windows.Forms.Label lblInventoryHint;
        private System.Windows.Forms.Panel pnlInventoryButtons;
        private System.Windows.Forms.Button btnReloadInventoryFromDisk;
        private System.Windows.Forms.TextBox txtInventoryRaw;
        private System.Windows.Forms.TabPage tabMods;
        private System.Windows.Forms.GroupBox grpMods;
        private System.Windows.Forms.Label lblModsHint;
        // Tab 1: Game Info Controls
        private System.Windows.Forms.GroupBox grpBasicInfo;
        private System.Windows.Forms.GroupBox grpAuthenticationSettings;
        private System.Windows.Forms.GroupBox grpAdvancedAuth;
        private System.Windows.Forms.GroupBox grpNetworkSettings;
        private System.Windows.Forms.GroupBox grpOtherStatsSettings;
        private System.Windows.Forms.GroupBox grpAchievementsFile;
        private System.Windows.Forms.GroupBox grpMatchmakingSettings;
        private System.Windows.Forms.TextBox txtAppID;
        private System.Windows.Forms.Label lblAppID;
        private System.Windows.Forms.Button btnLookupAppID;
        private System.Windows.Forms.TextBox txtGameName;
        private System.Windows.Forms.Label lblGameName;
        private System.Windows.Forms.Button btnLookupGameName;
        private System.Windows.Forms.TextBox txtClanTag;
        private System.Windows.Forms.Label lblClanTag;
        private System.Windows.Forms.Button btnRestoreDlls;
        private System.Windows.Forms.Label lblSteamAPIStatusX32Value;
        private System.Windows.Forms.Label lblSteamAPIStatus;
        
        private System.Windows.Forms.GroupBox grpPaths;
        private System.Windows.Forms.GroupBox grpSteamLaunchOptions;
        private System.Windows.Forms.CheckBox chkShowExtraSteamLaunchOptions;
        private System.Windows.Forms.ComboBox cmbSteamLaunchOptions;
        private System.Windows.Forms.Label lblSteamLaunchOptions;
        private System.Windows.Forms.TextBox txtGameFolder;
        private System.Windows.Forms.Label lblGameFolder;
        private System.Windows.Forms.LinkLabel lnkLauncherOptionsSteamDb;
        private System.Windows.Forms.LinkLabel lnkSteamCmdLineOptionsValveWiki;
        private System.Windows.Forms.Button btnBrowseGameFolder;
        private System.Windows.Forms.TextBox txtGameExecutable;
        private System.Windows.Forms.Label lblGameExecutable;
        private System.Windows.Forms.Button btnBrowseGameExecutable;
        private System.Windows.Forms.TextBox txtLaunchParameters;
        private System.Windows.Forms.Label lblLaunchParameters;
        private System.Windows.Forms.TextBox txtCustomIcon;
        private System.Windows.Forms.Label lblCustomIcon;
        private System.Windows.Forms.Button btnClearCustomIcon;
        private System.Windows.Forms.Button btnBrowseCustomIcon;
        
        // Tab 2: Authentication Controls
        private System.Windows.Forms.CheckBox chkBlockUnknownClients;
        
        
        // Additional controls for remaining tabs...
        private System.Windows.Forms.GroupBox grpStatsSettings;
        private System.Windows.Forms.CheckBox chkStats;
        private System.Windows.Forms.NumericUpDown numIconsPerIteration;
        private System.Windows.Forms.Label lblIconsPerIteration;
        
        private System.Windows.Forms.GroupBox grpCustomStats;
        private System.Windows.Forms.Panel pnlStatsDisplayScroll;
        private System.Windows.Forms.Label lblCustomStatsDisplay;
        
        private System.Windows.Forms.GroupBox grpDLCManagement;
        private System.Windows.Forms.CheckBox chkUnlockAllDLC;
        private System.Windows.Forms.TextBox txtDLCList;
        private System.Windows.Forms.Button btnFindDLCs;
        
        private System.Windows.Forms.CheckBox chkBetaBranch;
        private System.Windows.Forms.TextBox txtBetaBranchName;
        private System.Windows.Forms.Label lblBetaBranchName;
        
        private System.Windows.Forms.GroupBox grpSubscribedGroups;
        
        private System.Windows.Forms.CheckBox chkMatchmaking;
        
        private System.Windows.Forms.GroupBox grpEmulation;

        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.ToolTip toolTip;
        
        // Missing control declarations
        private System.Windows.Forms.CheckBox chkEnableExperimentalOverlayGame;
        private System.Windows.Forms.CheckBox chkUse32BitInventoryItemIds;
        private System.Windows.Forms.CheckBox chkDisableNetworking;
        private System.Windows.Forms.CheckBox chkDisableWarningBadAppId;
        
        // Additional Goldberg per-game controls
        private System.Windows.Forms.CheckBox chkSteamDeck;
        private System.Windows.Forms.CheckBox chkImmediateGameserverStats;
        private System.Windows.Forms.CheckBox chkMatchmakingServerListActualType;
        private System.Windows.Forms.CheckBox chkMatchmakingServerDetailsViaSourceQuery;
        private System.Windows.Forms.CheckBox chkDisableLeaderboardsCreateUnknown;
        private System.Windows.Forms.CheckBox chkAllowUnknownStats;
        private System.Windows.Forms.CheckBox chkStatAchievementProgressFunctionality;
        private System.Windows.Forms.CheckBox chkSaveOnlyHigherStatAchievementProgress;
        private System.Windows.Forms.CheckBox chkRecordPlaytime;
        private System.Windows.Forms.CheckBox chkDisableLanOnly;
        private System.Windows.Forms.CheckBox chkDisableSharingStatsWithGameserver;
        private System.Windows.Forms.CheckBox chkDisableSourceQuery;
        private System.Windows.Forms.CheckBox chkShareLeaderboardsOverNetwork;
        private System.Windows.Forms.CheckBox chkDisableLobbyCreation;
        private System.Windows.Forms.CheckBox chkDownloadSteamhttpRequests;
        private System.Windows.Forms.CheckBox chkAchievementsBypass;
        private System.Windows.Forms.CheckBox chkForceSteamhttpSuccess;
        private System.Windows.Forms.CheckBox chkDisableSteamoverlaygameidEnvVar;
        private System.Windows.Forms.CheckBox chkEnableSteamPreownedIds;
        private System.Windows.Forms.TextBox txtSteamGameStatsReportsDir;
        private System.Windows.Forms.Label lblSteamGameStatsReportsDir;
        private System.Windows.Forms.Button btnBrowseSteamGameStatsReportsDir;
        
        // Authentication & Security Controls
        private System.Windows.Forms.TextBox txtForceAccountName;
        private System.Windows.Forms.Label lblForceAccountName;
        private System.Windows.Forms.TextBox txtForceSteamId;
        private System.Windows.Forms.Label lblForceSteamId;
        private System.Windows.Forms.ComboBox cmbForceLanguage;
        private System.Windows.Forms.Label lblForceLanguage;
        
        // Network & Connectivity Controls
        private System.Windows.Forms.NumericUpDown numForcePort;
        private System.Windows.Forms.Label lblForcePort;
        private System.Windows.Forms.TextBox txtForceIpCountry;
        private System.Windows.Forms.Label lblForceIpCountry;
        private System.Windows.Forms.CheckBox chkOffline;
        private System.Windows.Forms.NumericUpDown numOldP2PPacketSharingMode;
        private System.Windows.Forms.Label lblOldP2PPacketSharingMode;
        
        // Additional Main Settings Controls
        private System.Windows.Forms.CheckBox chkEnableVoiceChat;
        private System.Windows.Forms.CheckBox chkFreeWeekend;
        
        // Additional User Settings Controls
        private System.Windows.Forms.TextBox txtUserTicket;
        private System.Windows.Forms.Label lblUserTicket;
        private System.Windows.Forms.TextBox txtAltSteamId;
        private System.Windows.Forms.Label lblAltSteamId;
        private System.Windows.Forms.NumericUpDown numAltSteamIdCount;
        private System.Windows.Forms.Label lblAltSteamIdCount;
        
        // Additional File Controls
        private System.Windows.Forms.ListBox lstSubscribedGroups;
        private System.Windows.Forms.Label lblSubscribedGroups;
        private System.Windows.Forms.ListBox lstSubscribedGroupsClans;
        private System.Windows.Forms.Label lblSubscribedGroupsClans;
        private System.Windows.Forms.ListView lstModsSummary;
        private System.Windows.Forms.ColumnHeader colModsSummaryId;
        private System.Windows.Forms.ColumnHeader colModsSummaryName;
        private System.Windows.Forms.ColumnHeader colModsSummaryFiller;
        private System.Windows.Forms.Panel pnlModsToolbar;
        private System.Windows.Forms.Button btnOpenModsFolder;
        private System.Windows.Forms.Button btnCopyFilesToMods;
        private System.Windows.Forms.Button btnCopyFoldersToMods;
        private System.Windows.Forms.ImageList imgAchievementsPreview;
        private System.Windows.Forms.Button btnRefreshAchievements;
        private System.Windows.Forms.Button btnRefreshStats;
        private Label lblAchievementsPreview;
        private Label lblAchievementsFilter;
        private TextBox txtAchievementsFilter;
        private ListView lstAchievementsPreview;
        private ColumnHeader colAchievementName;
        private ColumnHeader colAchievementDescription;
        private Button btnBrowseWorkingDirectory;
        private TextBox txtWorkingDirectory;
        private Label lblWorkingDirectory;
        private Button btnRemoveUserLaunchOption;
        private Button btnSaveUserLaunchOption;
        private TextBox txtUserLaunchOptionName;
        private Label lblUserLaunchOptionName;
        private Label lblHintGameFolder;
        private Label lblLaunchMode;
        private RadioButton rdoLaunchSteamClient;
        private RadioButton rdoLaunchExperimentalMode;
        private RadioButton rdoLaunchSteamDll;
        private RadioButton rdoLaunchNoEmulation;
        private System.Windows.Forms.ListView lstInventoryItems;
        private System.Windows.Forms.ColumnHeader colInventoryItemDefId;
        private System.Windows.Forms.ColumnHeader colInventoryDisplayName;
        private System.Windows.Forms.ColumnHeader colInventoryQuantity;
        private System.Windows.Forms.ColumnHeader colInventoryType;
        private System.Windows.Forms.ColumnHeader colInventoryFiller;
        private Label lblPatchOpMessage;
        private Label lblSteamAPIStatusX64Value;
        private Label lblSteamApiHealthValue;
        private Label lblSteamApiHealthNote;
        private Label lblSteamApiHealth;
        private Button btnAddSubscribedGroupClan;
        private Button btnRemoveSubscribedGroupClan;
        private Button btnRemoveSubscribedGroup;
        private Button btnAddSubscribedGroup;
        private TextBox txtSubscribedGroupClanEntry;
        private TextBox txtSubscribedGroupIdEntry;
    }
}
