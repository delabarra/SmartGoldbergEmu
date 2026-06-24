// SmartGoldbergEmu — see LICENSE in the repository root.
//#pragma warning disable 0169, 0414, 0649 // Field is never used / Field is assigned but never used / Field is never assigned to
using System;
using System.Windows.Forms;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Forms
{
    partial class MainForm
    {
        /// <summary>
        /// Variable n�cessaire au concepteur.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Nettoyage des ressources utilis�es.
        /// </summary>
        /// <param name="disposing">true si les ressources manag�es doivent �tre supprim�es�; sinon, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            
            // Cleanup helpers (handled in MainForm.cs OnFormClosed)
            
            base.Dispose(disposing);
        }

        #region Code g�n�r� par le Concepteur Windows Form

        /// <summary>
        /// M�thode requise pour la prise en charge du concepteur - ne modifiez pas
        /// le contenu de cette m�thode avec l'�diteur de code.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.mnuMain = new System.Windows.Forms.MenuStrip();
            this.miMnuFile = new System.Windows.Forms.ToolStripMenuItem();
            this.miMnuFileAddGame = new System.Windows.Forms.ToolStripMenuItem();
            this.sepMnuFileAfterAddGame = new System.Windows.Forms.ToolStripSeparator();
            this.miMnuFileGoldbergUpdate = new System.Windows.Forms.ToolStripMenuItem();
            this.miMnuFileForkSelect = new System.Windows.Forms.ToolStripMenuItem();
            this.miMnuFileCheckUpdates = new System.Windows.Forms.ToolStripMenuItem();
            this.miMnuFileReinstall = new System.Windows.Forms.ToolStripMenuItem();
            this.sepMnuFileBeforeGoldbergFolders = new System.Windows.Forms.ToolStripSeparator();
            this.miMnuFileOpenGoldbergFolder = new System.Windows.Forms.ToolStripMenuItem();
            this.miMnuFileOpenExtraDllsFolder = new System.Windows.Forms.ToolStripMenuItem();
            this.miMnuFileCheckLauncherUpdates = new System.Windows.Forms.ToolStripMenuItem();
            this.miMnuFileSettings = new System.Windows.Forms.ToolStripMenuItem();
            this.sepMnuFileBeforeExit = new System.Windows.Forms.ToolStripSeparator();
            this.miMnuFileExit = new System.Windows.Forms.ToolStripMenuItem();
            this.miMnuBarView = new System.Windows.Forms.ToolStripMenuItem();
            this.miMnuBarViewTile = new System.Windows.Forms.ToolStripMenuItem();
            this.miMnuBarViewCompactTiles = new System.Windows.Forms.ToolStripMenuItem();
            this.miMnuBarViewLogos = new System.Windows.Forms.ToolStripMenuItem();
            this.miMnuBarViewIcons = new System.Windows.Forms.ToolStripMenuItem();
            this.miMnuBarViewDetails = new System.Windows.Forms.ToolStripMenuItem();
            this.sepMnuBarAfterViewModes = new System.Windows.Forms.ToolStripSeparator();
            this.miMnuBarSort = new System.Windows.Forms.ToolStripMenuItem();
            this.miMnuBarSortName = new System.Windows.Forms.ToolStripMenuItem();
            this.miMnuBarSortNameAsc = new System.Windows.Forms.ToolStripMenuItem();
            this.miMnuBarSortNameDesc = new System.Windows.Forms.ToolStripMenuItem();
            this.miMnuBarSortAppId = new System.Windows.Forms.ToolStripMenuItem();
            this.miMnuBarSortAppIdAsc = new System.Windows.Forms.ToolStripMenuItem();
            this.miMnuBarSortAppIdDesc = new System.Windows.Forms.ToolStripMenuItem();
            this.sepMnuBarSortBeforeNone = new System.Windows.Forms.ToolStripSeparator();
            this.miMnuBarSortNone = new System.Windows.Forms.ToolStripMenuItem();
            this.sepMnuBarBeforeViewRefresh = new System.Windows.Forms.ToolStripSeparator();
            this.miMnuViewRefresh = new System.Windows.Forms.ToolStripMenuItem();
            this.miMnuAbout = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxViewCompactTiles = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxViewSort = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxViewSortName = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxViewSortNameAsc = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxViewSortNameDesc = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxViewSortAppId = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxViewSortAppIdAsc = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxViewSortAppIdDesc = new System.Windows.Forms.ToolStripMenuItem();
            this.sepCtxViewSortBeforeNone = new System.Windows.Forms.ToolStripSeparator();
            this.miCtxViewSortNone = new System.Windows.Forms.ToolStripMenuItem();
            this.sepCtxViewBeforeRefresh = new System.Windows.Forms.ToolStripSeparator();
            this.miCtxViewMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxViewTile = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxViewLogos = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxViewIcons = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxViewDetails = new System.Windows.Forms.ToolStripMenuItem();
            this.lstGames = new System.Windows.Forms.ListView();
            this.ctxGamesItem = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.miCtxRowRun = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxRowRunWithoutEmu = new System.Windows.Forms.ToolStripMenuItem();
            this.sepCtxRowAfterRun = new System.Windows.Forms.ToolStripSeparator();
            this.miCtxRowSteamPages = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxRowSteamStore = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxRowSteamCommunity = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxRowSteamWorkshop = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxRowSteamDb = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxRowTroubleshooting = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxRowGameDependencies = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxRowLauncherOptions = new System.Windows.Forms.ToolStripMenuItem();
            this.sepCtxRowBeforeEmulation = new System.Windows.Forms.ToolStripSeparator();
            this.miCtxRowEmulation = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxRowGenAchievements = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxRowGenItems = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxRowCreateSteamAppIdFile = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxRowOpenValveDataFile = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxRowOpenExtraDllsFolder = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxRowFilesFolders = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxRowOpenExecutableFolder = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxRowOpenSettingsFolder = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxRowOpenInventoryFile = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxRowTools = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxRowApplySteamless = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxRowCreateShortcut = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxRowCopyGuid = new System.Windows.Forms.ToolStripMenuItem();
            this.sepCtxRowBeforeProps = new System.Windows.Forms.ToolStripSeparator();
            this.miCtxRowProperties = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxRowRemove = new System.Windows.Forms.ToolStripMenuItem();
            this.stripMain = new System.Windows.Forms.StatusStrip();
            this.prgFeedback = new System.Windows.Forms.ToolStripProgressBar();
            this.lblFeedback = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblStatusSpring = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblApiKeyStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.btnTheme = new System.Windows.Forms.ToolStripDropDownButton();
            this.miThemeLight = new System.Windows.Forms.ToolStripMenuItem();
            this.miThemeDark = new System.Windows.Forms.ToolStripMenuItem();
            this.miThemeSystem = new System.Windows.Forms.ToolStripMenuItem();
            this.ctxGamesView = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.miCtxViewRefresh = new System.Windows.Forms.ToolStripMenuItem();
            this.miCtxViewAddGame = new System.Windows.Forms.ToolStripMenuItem();
            this.sepCtxViewAfterAdd = new System.Windows.Forms.ToolStripSeparator();
            this.mnuMain.SuspendLayout();
            this.ctxGamesItem.SuspendLayout();
            this.stripMain.SuspendLayout();
            this.ctxGamesView.SuspendLayout();
            this.SuspendLayout();
            // 
            // mnuMain
            // 
            this.mnuMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miMnuFile,
            this.miMnuBarView,
            this.miMnuAbout});
            this.mnuMain.Location = new System.Drawing.Point(0, 0);
            this.mnuMain.Name = "mnuMain";
            this.mnuMain.Size = new System.Drawing.Size(314, 24);
            this.mnuMain.TabIndex = 0;
            this.mnuMain.Text = "";
            // 
            // miMnuFile
            // 
            this.miMnuFile.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miMnuFileAddGame,
            this.sepMnuFileAfterAddGame,
            this.miMnuFileGoldbergUpdate,
            this.miMnuFileCheckLauncherUpdates,
            this.miMnuFileSettings,
            this.sepMnuFileBeforeExit,
            this.miMnuFileExit});
            this.miMnuFile.Name = "miMnuFile";
            this.miMnuFile.Size = new System.Drawing.Size(37, 20);
            this.miMnuFile.Text = "File";
            // 
            // miMnuFileAddGame
            // 
            this.miMnuFileAddGame.Name = "miMnuFileAddGame";
            this.miMnuFileAddGame.Size = new System.Drawing.Size(179, 22);
            this.miMnuFileAddGame.Text = "➕ Add Game";
            this.miMnuFileAddGame.Click += new System.EventHandler(this.OnAddGame_Click);
            // 
            // sepMnuFileAfterAddGame
            // 
            this.sepMnuFileAfterAddGame.Name = "sepMnuFileAfterAddGame";
            this.sepMnuFileAfterAddGame.Size = new System.Drawing.Size(176, 6);
            // 
            // miMnuFileGoldbergUpdate
            // 
            this.miMnuFileGoldbergUpdate.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miMnuFileForkSelect,
            this.miMnuFileCheckUpdates,
            this.miMnuFileReinstall,
            this.sepMnuFileBeforeGoldbergFolders,
            this.miMnuFileOpenGoldbergFolder,
            this.miMnuFileOpenExtraDllsFolder});
            this.miMnuFileGoldbergUpdate.Name = "miMnuFileGoldbergUpdate";
            this.miMnuFileGoldbergUpdate.Size = new System.Drawing.Size(179, 22);
            this.miMnuFileGoldbergUpdate.Text = "🎮 Goldberg Update";
            // 
            // miMnuFileForkSelect
            // 
            this.miMnuFileForkSelect.Name = "miMnuFileForkSelect";
            this.miMnuFileForkSelect.Size = new System.Drawing.Size(161, 22);
            this.miMnuFileForkSelect.Text = "⇅ Emulator Fork…";
            this.miMnuFileForkSelect.Click += new System.EventHandler(this.OnForkSelect_Click);
            // 
            // miMnuFileCheckUpdates
            // 
            this.miMnuFileCheckUpdates.Name = "miMnuFileCheckUpdates";
            this.miMnuFileCheckUpdates.Size = new System.Drawing.Size(161, 22);
            this.miMnuFileCheckUpdates.Text = "🡇 Check for Updates";
            this.miMnuFileCheckUpdates.Click += new System.EventHandler(this.OnCheckUpdates_Click);
            // 
            // miMnuFileReinstall
            // 
            this.miMnuFileReinstall.Name = "miMnuFileReinstall";
            this.miMnuFileReinstall.Size = new System.Drawing.Size(161, 22);
            this.miMnuFileReinstall.Text = "♻️ Reinstall";
            this.miMnuFileReinstall.Click += new System.EventHandler(this.OnReinstall_Click);
            // 
            // sepMnuFileBeforeGoldbergFolders
            // 
            this.sepMnuFileBeforeGoldbergFolders.Name = "sepMnuFileBeforeGoldbergFolders";
            this.sepMnuFileBeforeGoldbergFolders.Size = new System.Drawing.Size(158, 6);
            // 
            // miMnuFileOpenGoldbergFolder
            // 
            this.miMnuFileOpenGoldbergFolder.Name = "miMnuFileOpenGoldbergFolder";
            this.miMnuFileOpenGoldbergFolder.Size = new System.Drawing.Size(161, 22);
            this.miMnuFileOpenGoldbergFolder.Text = "Open goldberg folder";
            this.miMnuFileOpenGoldbergFolder.Click += new System.EventHandler(this.OnOpenGoldbergFolder_Click);
            // 
            // miMnuFileOpenExtraDllsFolder
            // 
            this.miMnuFileOpenExtraDllsFolder.Name = "miMnuFileOpenExtraDllsFolder";
            this.miMnuFileOpenExtraDllsFolder.Size = new System.Drawing.Size(161, 22);
            this.miMnuFileOpenExtraDllsFolder.Text = "Open extra DLLs folder";
            this.miMnuFileOpenExtraDllsFolder.Click += new System.EventHandler(this.OnOpenExtraDllsFolder_Click);
            // 
            // miMnuFileCheckLauncherUpdates
            // 
            this.miMnuFileCheckLauncherUpdates.Name = "miMnuFileCheckLauncherUpdates";
            this.miMnuFileCheckLauncherUpdates.Size = new System.Drawing.Size(179, 22);
            this.miMnuFileCheckLauncherUpdates.Text = "🡇 Launcher Update";
            this.miMnuFileCheckLauncherUpdates.Click += new System.EventHandler(this.OnCheckLauncherUpdates_Click);
            // 
            // miMnuFileSettings
            // 
            this.miMnuFileSettings.Name = "miMnuFileSettings";
            this.miMnuFileSettings.Size = new System.Drawing.Size(179, 22);
            this.miMnuFileSettings.Text = "⚙️ Settings";
            this.miMnuFileSettings.Click += new System.EventHandler(this.OnSettings_Click);
            // 
            // sepMnuFileBeforeExit
            // 
            this.sepMnuFileBeforeExit.Name = "sepMnuFileBeforeExit";
            this.sepMnuFileBeforeExit.Size = new System.Drawing.Size(176, 6);
            // 
            // miMnuFileExit
            // 
            this.miMnuFileExit.Name = "miMnuFileExit";
            this.miMnuFileExit.Size = new System.Drawing.Size(179, 22);
            this.miMnuFileExit.Text = "     Exit";
            this.miMnuFileExit.Click += new System.EventHandler(this.OnExit_Click);
            // 
            // miMnuBarView
            // 
            this.miMnuBarView.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miMnuBarViewTile,
            this.miMnuBarViewCompactTiles,
            this.miMnuBarViewLogos,
            this.miMnuBarViewIcons,
            this.miMnuBarViewDetails,
            this.sepMnuBarAfterViewModes,
            this.miMnuBarSort,
            this.sepMnuBarBeforeViewRefresh,
            this.miMnuViewRefresh});
            this.miMnuBarView.Name = "miMnuBarView";
            this.miMnuBarView.Size = new System.Drawing.Size(44, 20);
            this.miMnuBarView.Text = "View";
            // 
            // miMnuBarViewTile
            // 
            this.miMnuBarViewTile.Name = "miMnuBarViewTile";
            this.miMnuBarViewTile.Size = new System.Drawing.Size(146, 22);
            this.miMnuBarViewTile.Text = "Store Banner";
            this.miMnuBarViewTile.Click += new System.EventHandler(this.OnViewModeTile_Click);
            // 
            // miMnuBarViewCompactTiles
            // 
            this.miMnuBarViewCompactTiles.Name = "miMnuBarViewCompactTiles";
            this.miMnuBarViewCompactTiles.Size = new System.Drawing.Size(146, 22);
            this.miMnuBarViewCompactTiles.Text = "Library Cover";
            this.miMnuBarViewCompactTiles.Click += new System.EventHandler(this.OnViewModeCompactTiles_Click);
            // 
            // miMnuBarViewLogos
            // 
            this.miMnuBarViewLogos.Name = "miMnuBarViewLogos";
            this.miMnuBarViewLogos.Size = new System.Drawing.Size(146, 22);
            this.miMnuBarViewLogos.Text = "Logos";
            this.miMnuBarViewLogos.Click += new System.EventHandler(this.OnViewModeLogos_Click);
            // 
            // miMnuBarViewIcons
            // 
            this.miMnuBarViewIcons.Name = "miMnuBarViewIcons";
            this.miMnuBarViewIcons.Size = new System.Drawing.Size(146, 22);
            this.miMnuBarViewIcons.Text = "Icons";
            this.miMnuBarViewIcons.Click += new System.EventHandler(this.OnViewModeIcons_Click);
            // 
            // miMnuBarViewDetails
            // 
            this.miMnuBarViewDetails.Name = "miMnuBarViewDetails";
            this.miMnuBarViewDetails.Size = new System.Drawing.Size(146, 22);
            this.miMnuBarViewDetails.Text = "Details";
            this.miMnuBarViewDetails.Click += new System.EventHandler(this.OnViewModeDetails_Click);
            // 
            // sepMnuBarAfterViewModes
            // 
            this.sepMnuBarAfterViewModes.Name = "sepMnuBarAfterViewModes";
            this.sepMnuBarAfterViewModes.Size = new System.Drawing.Size(143, 6);
            // 
            // miMnuBarSort
            // 
            this.miMnuBarSort.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miMnuBarSortName,
            this.miMnuBarSortAppId,
            this.sepMnuBarSortBeforeNone,
            this.miMnuBarSortNone});
            this.miMnuBarSort.Name = "miMnuBarSort";
            this.miMnuBarSort.Size = new System.Drawing.Size(146, 22);
            this.miMnuBarSort.Text = "Sort";
            // 
            // miMnuBarSortName
            // 
            this.miMnuBarSortName.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miMnuBarSortNameAsc,
            this.miMnuBarSortNameDesc});
            this.miMnuBarSortName.Name = "miMnuBarSortName";
            this.miMnuBarSortName.Size = new System.Drawing.Size(106, 22);
            this.miMnuBarSortName.Text = "Name";
            // 
            // miMnuBarSortNameAsc
            // 
            this.miMnuBarSortNameAsc.Name = "miMnuBarSortNameAsc";
            this.miMnuBarSortNameAsc.Size = new System.Drawing.Size(99, 22);
            this.miMnuBarSortNameAsc.Text = "Asc";
            this.miMnuBarSortNameAsc.Click += new System.EventHandler(this.OnSortNameAsc_Click);
            // 
            // miMnuBarSortNameDesc
            // 
            this.miMnuBarSortNameDesc.Name = "miMnuBarSortNameDesc";
            this.miMnuBarSortNameDesc.Size = new System.Drawing.Size(99, 22);
            this.miMnuBarSortNameDesc.Text = "Desc";
            this.miMnuBarSortNameDesc.Click += new System.EventHandler(this.OnSortNameDesc_Click);
            // 
            // miMnuBarSortAppId
            // 
            this.miMnuBarSortAppId.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miMnuBarSortAppIdAsc,
            this.miMnuBarSortAppIdDesc});
            this.miMnuBarSortAppId.Name = "miMnuBarSortAppId";
            this.miMnuBarSortAppId.Size = new System.Drawing.Size(106, 22);
            this.miMnuBarSortAppId.Text = "App ID";
            // 
            // miMnuBarSortAppIdAsc
            // 
            this.miMnuBarSortAppIdAsc.Name = "miMnuBarSortAppIdAsc";
            this.miMnuBarSortAppIdAsc.Size = new System.Drawing.Size(99, 22);
            this.miMnuBarSortAppIdAsc.Text = "Asc";
            this.miMnuBarSortAppIdAsc.Click += new System.EventHandler(this.OnSortAppIdAsc_Click);
            // 
            // miMnuBarSortAppIdDesc
            // 
            this.miMnuBarSortAppIdDesc.Name = "miMnuBarSortAppIdDesc";
            this.miMnuBarSortAppIdDesc.Size = new System.Drawing.Size(99, 22);
            this.miMnuBarSortAppIdDesc.Text = "Desc";
            this.miMnuBarSortAppIdDesc.Click += new System.EventHandler(this.OnSortAppIdDesc_Click);
            // 
            // sepMnuBarSortBeforeNone
            // 
            this.sepMnuBarSortBeforeNone.Name = "sepMnuBarSortBeforeNone";
            this.sepMnuBarSortBeforeNone.Size = new System.Drawing.Size(103, 6);
            // 
            // miMnuBarSortNone
            // 
            this.miMnuBarSortNone.Name = "miMnuBarSortNone";
            this.miMnuBarSortNone.Size = new System.Drawing.Size(106, 22);
            this.miMnuBarSortNone.Text = "None";
            this.miMnuBarSortNone.Click += new System.EventHandler(this.OnSortNone_Click);
            // 
            // sepMnuBarBeforeViewRefresh
            // 
            this.sepMnuBarBeforeViewRefresh.Name = "sepMnuBarBeforeViewRefresh";
            this.sepMnuBarBeforeViewRefresh.Size = new System.Drawing.Size(143, 6);
            // 
            // miMnuViewRefresh
            // 
            this.miMnuViewRefresh.Name = "miMnuViewRefresh";
            this.miMnuViewRefresh.Size = new System.Drawing.Size(146, 22);
            this.miMnuViewRefresh.Text = "Refresh";
            this.miMnuViewRefresh.Click += new System.EventHandler(this.OnBarViewRefresh_Click);
            // 
            // miMnuAbout
            // 
            this.miMnuAbout.Name = "miMnuAbout";
            this.miMnuAbout.Size = new System.Drawing.Size(52, 20);
            this.miMnuAbout.Text = "About";
            this.miMnuAbout.Click += new System.EventHandler(this.OnAbout_Click);
            // 
            // miCtxViewCompactTiles
            // 
            this.miCtxViewCompactTiles.Name = "miCtxViewCompactTiles";
            this.miCtxViewCompactTiles.Size = new System.Drawing.Size(146, 22);
            this.miCtxViewCompactTiles.Text = "Library Cover";
            this.miCtxViewCompactTiles.Click += new System.EventHandler(this.OnViewModeCompactTiles_Click);
            // 
            // miCtxViewSort
            // 
            this.miCtxViewSort.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miCtxViewSortName,
            this.miCtxViewSortAppId,
            this.sepCtxViewSortBeforeNone,
            this.miCtxViewSortNone});
            this.miCtxViewSort.Name = "miCtxViewSort";
            this.miCtxViewSort.Size = new System.Drawing.Size(113, 22);
            this.miCtxViewSort.Text = "Sort";
            // 
            // miCtxViewSortName
            // 
            this.miCtxViewSortName.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miCtxViewSortNameAsc,
            this.miCtxViewSortNameDesc});
            this.miCtxViewSortName.Name = "miCtxViewSortName";
            this.miCtxViewSortName.Size = new System.Drawing.Size(106, 22);
            this.miCtxViewSortName.Text = "Name";
            // 
            // miCtxViewSortNameAsc
            // 
            this.miCtxViewSortNameAsc.Name = "miCtxViewSortNameAsc";
            this.miCtxViewSortNameAsc.Size = new System.Drawing.Size(139, 22);
            this.miCtxViewSortNameAsc.Text = "Ascending (A–Z)";
            this.miCtxViewSortNameAsc.Click += new System.EventHandler(this.OnSortNameAsc_Click);
            // 
            // miCtxViewSortNameDesc
            // 
            this.miCtxViewSortNameDesc.Name = "miCtxViewSortNameDesc";
            this.miCtxViewSortNameDesc.Size = new System.Drawing.Size(139, 22);
            this.miCtxViewSortNameDesc.Text = "Descending (Z–A)";
            this.miCtxViewSortNameDesc.Click += new System.EventHandler(this.OnSortNameDesc_Click);
            // 
            // miCtxViewSortAppId
            // 
            this.miCtxViewSortAppId.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miCtxViewSortAppIdAsc,
            this.miCtxViewSortAppIdDesc});
            this.miCtxViewSortAppId.Name = "miCtxViewSortAppId";
            this.miCtxViewSortAppId.Size = new System.Drawing.Size(106, 22);
            this.miCtxViewSortAppId.Text = "App ID";
            // 
            // miCtxViewSortAppIdAsc
            // 
            this.miCtxViewSortAppIdAsc.Name = "miCtxViewSortAppIdAsc";
            this.miCtxViewSortAppIdAsc.Size = new System.Drawing.Size(160, 22);
            this.miCtxViewSortAppIdAsc.Text = "Ascending (low to high)";
            this.miCtxViewSortAppIdAsc.Click += new System.EventHandler(this.OnSortAppIdAsc_Click);
            // 
            // miCtxViewSortAppIdDesc
            // 
            this.miCtxViewSortAppIdDesc.Name = "miCtxViewSortAppIdDesc";
            this.miCtxViewSortAppIdDesc.Size = new System.Drawing.Size(160, 22);
            this.miCtxViewSortAppIdDesc.Text = "Descending (high to low)";
            this.miCtxViewSortAppIdDesc.Click += new System.EventHandler(this.OnSortAppIdDesc_Click);
            // 
            // sepCtxViewSortBeforeNone
            // 
            this.sepCtxViewSortBeforeNone.Name = "sepCtxViewSortBeforeNone";
            this.sepCtxViewSortBeforeNone.Size = new System.Drawing.Size(103, 6);
            // 
            // miCtxViewSortNone
            // 
            this.miCtxViewSortNone.Name = "miCtxViewSortNone";
            this.miCtxViewSortNone.Size = new System.Drawing.Size(106, 22);
            this.miCtxViewSortNone.Text = "None";
            this.miCtxViewSortNone.Click += new System.EventHandler(this.OnSortNone_Click);
            // 
            // sepCtxViewBeforeRefresh
            // 
            this.sepCtxViewBeforeRefresh.Name = "sepCtxViewBeforeRefresh";
            this.sepCtxViewBeforeRefresh.Size = new System.Drawing.Size(110, 6);
            // 
            // miCtxViewMenu
            // 
            this.miCtxViewMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miCtxViewTile,
            this.miCtxViewCompactTiles,
            this.miCtxViewLogos,
            this.miCtxViewIcons,
            this.miCtxViewDetails});
            this.miCtxViewMenu.Name = "miCtxViewMenu";
            this.miCtxViewMenu.Size = new System.Drawing.Size(113, 22);
            this.miCtxViewMenu.Text = "View";
            // 
            // miCtxViewTile
            // 
            this.miCtxViewTile.Name = "miCtxViewTile";
            this.miCtxViewTile.Size = new System.Drawing.Size(146, 22);
            this.miCtxViewTile.Text = "Store Banner";
            this.miCtxViewTile.Click += new System.EventHandler(this.OnViewModeTile_Click);
            // 
            // miCtxViewLogos
            // 
            this.miCtxViewLogos.Name = "miCtxViewLogos";
            this.miCtxViewLogos.Size = new System.Drawing.Size(146, 22);
            this.miCtxViewLogos.Text = "Logos";
            this.miCtxViewLogos.Click += new System.EventHandler(this.OnViewModeLogos_Click);
            // 
            // miCtxViewIcons
            // 
            this.miCtxViewIcons.Name = "miCtxViewIcons";
            this.miCtxViewIcons.Size = new System.Drawing.Size(146, 22);
            this.miCtxViewIcons.Text = "Icons";
            this.miCtxViewIcons.Click += new System.EventHandler(this.OnViewModeIcons_Click);
            // 
            // miCtxViewDetails
            // 
            this.miCtxViewDetails.Name = "miCtxViewDetails";
            this.miCtxViewDetails.Size = new System.Drawing.Size(146, 22);
            this.miCtxViewDetails.Text = "Details";
            this.miCtxViewDetails.Click += new System.EventHandler(this.OnViewModeDetails_Click);
            // 
            // lstGames
            // 
            this.lstGames.AllowDrop = true;
            this.lstGames.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstGames.FullRowSelect = true;
            this.lstGames.HideSelection = false;
            this.lstGames.Location = new System.Drawing.Point(0, 24);
            this.lstGames.Name = "lstGames";
            this.lstGames.ShowItemToolTips = true;
            this.lstGames.Size = new System.Drawing.Size(314, 365);
            this.lstGames.TabIndex = 1;
            this.lstGames.UseCompatibleStateImageBehavior = false;
            this.lstGames.View = System.Windows.Forms.View.Details;
            // 
            // ctxGamesItem
            // 
            this.ctxGamesItem.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miCtxRowRun,
            this.miCtxRowRunWithoutEmu,
            this.sepCtxRowAfterRun,
            this.miCtxRowSteamPages,
            this.miCtxRowTroubleshooting,
            this.sepCtxRowBeforeEmulation,
            this.miCtxRowEmulation,
            this.miCtxRowFilesFolders,
            this.miCtxRowTools,
            this.sepCtxRowBeforeProps,
            this.miCtxRowProperties,
            this.miCtxRowRemove});
            this.ctxGamesItem.Name = "ctxGamesItem";
            this.ctxGamesItem.Size = new System.Drawing.Size(206, 270);
            // 
            // miCtxRowRun
            // 
            this.miCtxRowRun.Name = "miCtxRowRun";
            this.miCtxRowRun.Size = new System.Drawing.Size(205, 22);
            this.miCtxRowRun.Text = "▶️ Run";
            // 
            // miCtxRowRunWithoutEmu
            // 
            this.miCtxRowRunWithoutEmu.Name = "miCtxRowRunWithoutEmu";
            this.miCtxRowRunWithoutEmu.Size = new System.Drawing.Size(205, 22);
            this.miCtxRowRunWithoutEmu.Text = "▷ Run without emulator";
            // 
            // sepCtxRowAfterRun
            // 
            this.sepCtxRowAfterRun.Name = "sepCtxRowAfterRun";
            this.sepCtxRowAfterRun.Size = new System.Drawing.Size(202, 6);
            // 
            // miCtxRowSteamPages
            // 
            this.miCtxRowSteamPages.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miCtxRowSteamStore,
            this.miCtxRowSteamCommunity,
            this.miCtxRowSteamWorkshop});
            this.miCtxRowSteamPages.Name = "miCtxRowSteamPages";
            this.miCtxRowSteamPages.Size = new System.Drawing.Size(205, 22);
            this.miCtxRowSteamPages.Text = "Steam";
            // 
            // miCtxRowSteamStore
            // 
            this.miCtxRowSteamStore.Name = "miCtxRowSteamStore";
            this.miCtxRowSteamStore.Size = new System.Drawing.Size(203, 22);
            this.miCtxRowSteamStore.Text = "Store";
            // 
            // miCtxRowSteamCommunity
            // 
            this.miCtxRowSteamCommunity.Name = "miCtxRowSteamCommunity";
            this.miCtxRowSteamCommunity.Size = new System.Drawing.Size(203, 22);
            this.miCtxRowSteamCommunity.Text = "Community";
            // 
            // miCtxRowSteamWorkshop
            // 
            this.miCtxRowSteamWorkshop.Name = "miCtxRowSteamWorkshop";
            this.miCtxRowSteamWorkshop.Size = new System.Drawing.Size(203, 22);
            this.miCtxRowSteamWorkshop.Text = "Workshop";
            // 
            // miCtxRowSteamDb
            // 
            this.miCtxRowSteamDb.Name = "miCtxRowSteamDb";
            this.miCtxRowSteamDb.Size = new System.Drawing.Size(203, 22);
            this.miCtxRowSteamDb.Text = "App page";
            // 
            // miCtxRowTroubleshooting
            // 
            this.miCtxRowTroubleshooting.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miCtxRowSteamDb,
            this.miCtxRowGameDependencies,
            this.miCtxRowLauncherOptions});
            this.miCtxRowTroubleshooting.Name = "miCtxRowTroubleshooting";
            this.miCtxRowTroubleshooting.Size = new System.Drawing.Size(205, 22);
            this.miCtxRowTroubleshooting.Text = "SteamDB";
            // 
            // miCtxRowGameDependencies
            // 
            this.miCtxRowGameDependencies.Name = "miCtxRowGameDependencies";
            this.miCtxRowGameDependencies.Size = new System.Drawing.Size(241, 22);
            this.miCtxRowGameDependencies.Text = "Dependencies";
            // 
            // miCtxRowLauncherOptions
            // 
            this.miCtxRowLauncherOptions.Name = "miCtxRowLauncherOptions";
            this.miCtxRowLauncherOptions.Size = new System.Drawing.Size(241, 22);
            this.miCtxRowLauncherOptions.Text = "Launch options";
            // 
            // sepCtxRowBeforeEmulation
            // 
            this.sepCtxRowBeforeEmulation.Name = "sepCtxRowBeforeEmulation";
            this.sepCtxRowBeforeEmulation.Size = new System.Drawing.Size(202, 6);
            // 
            // miCtxRowEmulation
            // 
            this.miCtxRowEmulation.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miCtxRowGenAchievements,
            this.miCtxRowGenItems,
            this.miCtxRowCreateSteamAppIdFile});
            this.miCtxRowEmulation.Name = "miCtxRowEmulation";
            this.miCtxRowEmulation.Size = new System.Drawing.Size(205, 22);
            this.miCtxRowEmulation.Text = "Goldberg";
            // 
            // miCtxRowGenAchievements
            // 
            this.miCtxRowGenAchievements.Name = "miCtxRowGenAchievements";
            this.miCtxRowGenAchievements.Size = new System.Drawing.Size(253, 22);
            this.miCtxRowGenAchievements.Text = "Generate achievements";
            // 
            // miCtxRowGenItems
            // 
            this.miCtxRowGenItems.Name = "miCtxRowGenItems";
            this.miCtxRowGenItems.Size = new System.Drawing.Size(253, 22);
            this.miCtxRowGenItems.Text = "Generate items";
            // 
            // miCtxRowCreateSteamAppIdFile
            // 
            this.miCtxRowCreateSteamAppIdFile.Name = "miCtxRowCreateSteamAppIdFile";
            this.miCtxRowCreateSteamAppIdFile.Size = new System.Drawing.Size(253, 22);
            this.miCtxRowCreateSteamAppIdFile.Text = $"Create {PathConstants.SteamAppIdFileName}";
            // 
            // miCtxRowFilesFolders
            // 
            this.miCtxRowFilesFolders.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miCtxRowOpenExecutableFolder,
            this.miCtxRowOpenSettingsFolder,
            this.miCtxRowOpenInventoryFile,
            this.miCtxRowOpenValveDataFile,
            this.miCtxRowOpenExtraDllsFolder});
            this.miCtxRowFilesFolders.Name = "miCtxRowFilesFolders";
            this.miCtxRowFilesFolders.Size = new System.Drawing.Size(205, 22);
            this.miCtxRowFilesFolders.Text = "Files && folders";
            // 
            // miCtxRowOpenExecutableFolder
            // 
            this.miCtxRowOpenExecutableFolder.Name = "miCtxRowOpenExecutableFolder";
            this.miCtxRowOpenExecutableFolder.Size = new System.Drawing.Size(199, 22);
            this.miCtxRowOpenExecutableFolder.Text = "Open game folder";
            // 
            // miCtxRowOpenSettingsFolder
            // 
            this.miCtxRowOpenSettingsFolder.Name = "miCtxRowOpenSettingsFolder";
            this.miCtxRowOpenSettingsFolder.Size = new System.Drawing.Size(199, 22);
            this.miCtxRowOpenSettingsFolder.Text = "Open steam_settings folder";
            // 
            // miCtxRowOpenInventoryFile
            // 
            this.miCtxRowOpenInventoryFile.Name = "miCtxRowOpenInventoryFile";
            this.miCtxRowOpenInventoryFile.Size = new System.Drawing.Size(199, 22);
            this.miCtxRowOpenInventoryFile.Text = $"Open {PathConstants.GoldbergItemsJsonFileName}";
            // 
            // miCtxRowOpenValveDataFile
            // 
            this.miCtxRowOpenValveDataFile.Name = "miCtxRowOpenValveDataFile";
            this.miCtxRowOpenValveDataFile.Size = new System.Drawing.Size(199, 22);
            this.miCtxRowOpenValveDataFile.Text = "Open Valve Data File";
            // 
            // miCtxRowOpenExtraDllsFolder
            // 
            this.miCtxRowOpenExtraDllsFolder.Name = "miCtxRowOpenExtraDllsFolder";
            this.miCtxRowOpenExtraDllsFolder.Size = new System.Drawing.Size(199, 22);
            this.miCtxRowOpenExtraDllsFolder.Text = "Open extra DLLs folder";
            this.miCtxRowOpenExtraDllsFolder.Click += new System.EventHandler(this.OnOpenExtraDllsFolder_Click);
            // 
            // miCtxRowTools
            // 
            this.miCtxRowTools.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miCtxRowApplySteamless,
            this.miCtxRowCreateShortcut,
            this.miCtxRowCopyGuid});
            this.miCtxRowTools.Name = "miCtxRowTools";
            this.miCtxRowTools.Size = new System.Drawing.Size(205, 22);
            this.miCtxRowTools.Text = "Tools";
            // 
            // miCtxRowApplySteamless
            // 
            this.miCtxRowApplySteamless.Name = "miCtxRowApplySteamless";
            this.miCtxRowApplySteamless.Size = new System.Drawing.Size(156, 22);
            this.miCtxRowApplySteamless.Text = "Steamless";
            // 
            // miCtxRowCreateShortcut
            // 
            this.miCtxRowCreateShortcut.Name = "miCtxRowCreateShortcut";
            this.miCtxRowCreateShortcut.Size = new System.Drawing.Size(156, 22);
            this.miCtxRowCreateShortcut.Text = "Create shortcut";
            // 
            // miCtxRowCopyGuid
            // 
            this.miCtxRowCopyGuid.Name = "miCtxRowCopyGuid";
            this.miCtxRowCopyGuid.Size = new System.Drawing.Size(156, 22);
            this.miCtxRowCopyGuid.Text = "Copy entry GUID";
            this.miCtxRowCopyGuid.ToolTipText = "Library entry identifier (not App ID).";
            // 
            // sepCtxRowBeforeProps
            // 
            this.sepCtxRowBeforeProps.Name = "sepCtxRowBeforeProps";
            this.sepCtxRowBeforeProps.Size = new System.Drawing.Size(202, 6);
            // 
            // miCtxRowProperties
            // 
            this.miCtxRowProperties.Name = "miCtxRowProperties";
            this.miCtxRowProperties.Size = new System.Drawing.Size(205, 22);
            this.miCtxRowProperties.Text = "Properties…";
            // 
            // miCtxRowRemove
            // 
            this.miCtxRowRemove.Name = "miCtxRowRemove";
            this.miCtxRowRemove.Size = new System.Drawing.Size(205, 22);
            this.miCtxRowRemove.Text = "Remove";
            // 
            // stripMain
            // 
            this.stripMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.prgFeedback,
            this.lblFeedback,
            this.lblStatusSpring,
            this.lblApiKeyStatus,
            this.btnTheme});
            this.stripMain.Location = new System.Drawing.Point(0, 389);
            this.stripMain.Name = "stripMain";
            this.stripMain.ShowItemToolTips = true;
            this.stripMain.Size = new System.Drawing.Size(314, 22);
            this.stripMain.TabIndex = 2;
            this.stripMain.Text = "";
            // 
            // prgFeedback
            // 
            this.prgFeedback.Name = "prgFeedback";
            this.prgFeedback.Size = new System.Drawing.Size(80, 16);
            this.prgFeedback.Visible = false;
            // 
            // lblFeedback
            // 
            this.lblFeedback.Name = "lblFeedback";
            this.lblFeedback.Size = new System.Drawing.Size(0, 17);
            // 
            // lblStatusSpring
            // 
            this.lblStatusSpring.Name = "lblStatusSpring";
            this.lblStatusSpring.Size = new System.Drawing.Size(263, 17);
            this.lblStatusSpring.Spring = true;
            // 
            // lblApiKeyStatus
            // 
            this.lblApiKeyStatus.Name = "lblApiKeyStatus";
            this.lblApiKeyStatus.Size = new System.Drawing.Size(19, 17);
            this.lblApiKeyStatus.Text = "⚠️";
            this.lblApiKeyStatus.ToolTipText = "Missing Steam Web API key — click to add.";
            this.lblApiKeyStatus.Visible = false;
            // 
            // btnTheme
            // 
            this.btnTheme.AutoSize = false;
            this.btnTheme.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnTheme.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miThemeLight,
            this.miThemeDark,
            this.miThemeSystem});
            this.btnTheme.Name = "btnTheme";
            this.btnTheme.Size = new System.Drawing.Size(36, 20);
            // 
            // miThemeLight
            // 
            this.miThemeLight.Name = "miThemeLight";
            this.miThemeLight.Size = new System.Drawing.Size(127, 22);
            this.miThemeLight.Text = "☀️ Light";
            this.miThemeLight.Click += new System.EventHandler(this.OnThemeLight_Click);
            // 
            // miThemeDark
            // 
            this.miThemeDark.Name = "miThemeDark";
            this.miThemeDark.Size = new System.Drawing.Size(127, 22);
            this.miThemeDark.Text = "🌙 Dark";
            this.miThemeDark.Click += new System.EventHandler(this.OnThemeDark_Click);
            // 
            // miThemeSystem
            // 
            this.miThemeSystem.Name = "miThemeSystem";
            this.miThemeSystem.Size = new System.Drawing.Size(127, 22);
            this.miThemeSystem.Text = "🖥️ System";
            this.miThemeSystem.Click += new System.EventHandler(this.OnThemeSystem_Click);
            // 
            // ctxGamesView
            // 
            this.ctxGamesView.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miCtxViewAddGame,
            this.sepCtxViewAfterAdd,
            this.miCtxViewSort,
            this.miCtxViewMenu,
            this.sepCtxViewBeforeRefresh,
            this.miCtxViewRefresh});
            this.ctxGamesView.Name = "ctxGamesView";
            this.ctxGamesView.Size = new System.Drawing.Size(180, 120);
            // 
            // miCtxViewAddGame
            // 
            this.miCtxViewAddGame.Name = "miCtxViewAddGame";
            this.miCtxViewAddGame.Size = new System.Drawing.Size(179, 22);
            this.miCtxViewAddGame.Text = "➕ Add Game";
            this.miCtxViewAddGame.Click += new System.EventHandler(this.OnAddGame_Click);
            // 
            // sepCtxViewAfterAdd
            // 
            this.sepCtxViewAfterAdd.Name = "sepCtxViewAfterAdd";
            this.sepCtxViewAfterAdd.Size = new System.Drawing.Size(176, 6);
            // 
            // miCtxViewRefresh
            // 
            this.miCtxViewRefresh.Name = "miCtxViewRefresh";
            this.miCtxViewRefresh.Size = new System.Drawing.Size(113, 22);
            this.miCtxViewRefresh.Text = "Refresh";
            this.miCtxViewRefresh.Click += new System.EventHandler(this.OnCtxViewRefresh_Click);
            // 
            // MainForm
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(314, 411);
            this.Controls.Add(this.lstGames);
            this.Controls.Add(this.stripMain);
            this.Controls.Add(this.mnuMain);
            this.MainMenuStrip = this.mnuMain;
            this.MinimumSize = new System.Drawing.Size(320, 220);
            this.Name = "MainForm";
            this.Text = "SmartGoldbergEmu Launcher";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.mnuMain.ResumeLayout(false);
            this.mnuMain.PerformLayout();
            this.ctxGamesItem.ResumeLayout(false);
            this.stripMain.ResumeLayout(false);
            this.stripMain.PerformLayout();
            this.ctxGamesView.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip mnuMain;
        private System.Windows.Forms.ToolStripMenuItem miMnuFile;
        private System.Windows.Forms.ToolStripMenuItem miMnuFileAddGame;
        private System.Windows.Forms.ToolStripSeparator sepMnuFileAfterAddGame;
        private System.Windows.Forms.ToolStripMenuItem miMnuFileSettings;
        private System.Windows.Forms.ToolStripMenuItem miMnuFileExit;
        private System.Windows.Forms.ToolStripMenuItem miMnuAbout;
        private System.Windows.Forms.ToolStripMenuItem miMnuFileCheckLauncherUpdates;
        private System.Windows.Forms.ToolStripSeparator sepMnuFileBeforeExit;
        private System.Windows.Forms.ToolStripMenuItem miMnuFileGoldbergUpdate;
        private System.Windows.Forms.ToolStripMenuItem miMnuFileReinstall;
        private System.Windows.Forms.ListView lstGames;
        private System.Windows.Forms.ContextMenuStrip ctxGamesItem;
        private System.Windows.Forms.ToolStripMenuItem miCtxRowProperties;
        private System.Windows.Forms.ToolStripMenuItem miCtxRowRemove;
        private System.Windows.Forms.ToolStripMenuItem miCtxRowGenAchievements;
        private System.Windows.Forms.ToolStripMenuItem miCtxRowGenItems;
        private System.Windows.Forms.ToolStripMenuItem miCtxRowOpenExecutableFolder;
        private System.Windows.Forms.ToolStripMenuItem miCtxRowOpenSettingsFolder;
        private System.Windows.Forms.ToolStripMenuItem miCtxRowCreateShortcut;
        private System.Windows.Forms.ToolStripMenuItem miCtxRowSteamStore;
        private System.Windows.Forms.StatusStrip stripMain;
        private System.Windows.Forms.ToolStripProgressBar prgFeedback;
        private System.Windows.Forms.ToolStripStatusLabel lblFeedback;
        private System.Windows.Forms.ToolStripStatusLabel lblApiKeyStatus;
        private System.Windows.Forms.ContextMenuStrip ctxGamesView;
        private System.Windows.Forms.ToolStripMenuItem miCtxViewAddGame;
        private System.Windows.Forms.ToolStripSeparator sepCtxViewAfterAdd;
        private System.Windows.Forms.ToolStripMenuItem miCtxViewMenu;
        private System.Windows.Forms.ToolStripMenuItem miCtxViewRefresh;
        private System.Windows.Forms.ToolStripMenuItem miCtxViewIcons;
        private ToolStripMenuItem miCtxViewLogos;
        private System.Windows.Forms.ToolStripMenuItem miCtxViewDetails;
        private System.Windows.Forms.ToolStripMenuItem miCtxViewTile;
        private System.Windows.Forms.ToolStripMenuItem miMnuBarView;
        private System.Windows.Forms.ToolStripMenuItem miMnuBarViewTile;
        private ToolStripMenuItem miMnuBarViewLogos;
        private System.Windows.Forms.ToolStripMenuItem miMnuBarViewIcons;
        private System.Windows.Forms.ToolStripMenuItem miMnuBarViewDetails;
        private System.Windows.Forms.ToolStripMenuItem miCtxViewSort;
        private System.Windows.Forms.ToolStripMenuItem miCtxViewSortName;
        private System.Windows.Forms.ToolStripMenuItem miCtxViewSortNameAsc;
        private System.Windows.Forms.ToolStripMenuItem miCtxViewSortNameDesc;
        private System.Windows.Forms.ToolStripMenuItem miCtxViewSortAppId;
        private System.Windows.Forms.ToolStripMenuItem miCtxViewSortAppIdAsc;
        private System.Windows.Forms.ToolStripMenuItem miCtxViewSortAppIdDesc;
        private System.Windows.Forms.ToolStripMenuItem miCtxViewSortNone;
        private System.Windows.Forms.ToolStripSeparator sepCtxViewBeforeRefresh;
        private System.Windows.Forms.ToolStripMenuItem miMnuViewRefresh;
        private System.Windows.Forms.ToolStripMenuItem miCtxRowRun;
        private System.Windows.Forms.ToolStripMenuItem miCtxRowRunWithoutEmu;
        private System.Windows.Forms.ToolStripSeparator sepCtxRowAfterRun;
        private System.Windows.Forms.ToolStripMenuItem miCtxRowOpenInventoryFile;
        private System.Windows.Forms.ToolStripMenuItem miCtxRowCreateSteamAppIdFile;
        private System.Windows.Forms.ToolStripMenuItem miCtxRowCopyGuid;
        private System.Windows.Forms.ToolStripMenuItem miCtxRowSteamCommunity;
        private System.Windows.Forms.ToolStripMenuItem miCtxRowSteamWorkshop;
        private System.Windows.Forms.ToolStripMenuItem miCtxRowSteamDb;
        private System.Windows.Forms.ToolStripMenuItem miCtxRowGameDependencies;
        private System.Windows.Forms.ToolStripMenuItem miCtxRowLauncherOptions;
        private System.Windows.Forms.ToolStripMenuItem miCtxRowTroubleshooting;
        private System.Windows.Forms.ToolStripMenuItem miCtxRowSteamPages;
        private System.Windows.Forms.ToolStripMenuItem miCtxRowEmulation;
        private System.Windows.Forms.ToolStripMenuItem miCtxRowFilesFolders;
        private System.Windows.Forms.ToolStripMenuItem miCtxRowTools;
        private System.Windows.Forms.ToolStripMenuItem miCtxRowApplySteamless;
        private System.Windows.Forms.ToolStripSeparator sepCtxRowBeforeProps;
        private ToolStripSeparator sepCtxViewSortBeforeNone;
        private ToolStripSeparator sepMnuBarAfterViewModes;
        private ToolStripMenuItem miMnuBarSort;
        private ToolStripMenuItem miMnuBarSortName;
        private ToolStripMenuItem miMnuBarSortNameAsc;
        private ToolStripMenuItem miMnuBarSortNameDesc;
        private ToolStripMenuItem miMnuBarSortAppId;
        private ToolStripMenuItem miMnuBarSortAppIdAsc;
        private ToolStripMenuItem miMnuBarSortAppIdDesc;
        private ToolStripSeparator sepMnuBarSortBeforeNone;
        private ToolStripMenuItem miMnuBarSortNone;
        private ToolStripSeparator sepMnuBarBeforeViewRefresh;
        private System.Windows.Forms.ToolStripMenuItem miCtxViewCompactTiles;
        private ToolStripMenuItem miMnuBarViewCompactTiles;
        private ToolStripMenuItem miMnuFileForkSelect;
        private ToolStripMenuItem miMnuFileCheckUpdates;
        private ToolStripSeparator sepMnuFileBeforeGoldbergFolders;
        private ToolStripMenuItem miMnuFileOpenGoldbergFolder;
        private ToolStripMenuItem miMnuFileOpenExtraDllsFolder;
        private ToolStripMenuItem miCtxRowOpenValveDataFile;
        private ToolStripMenuItem miCtxRowOpenExtraDllsFolder;
        private System.Windows.Forms.ToolStripStatusLabel lblStatusSpring;
        private System.Windows.Forms.ToolStripDropDownButton btnTheme;
        private System.Windows.Forms.ToolStripMenuItem miThemeLight;
        private System.Windows.Forms.ToolStripMenuItem miThemeDark;
        private System.Windows.Forms.ToolStripMenuItem miThemeSystem;
        private ToolStripSeparator sepCtxRowBeforeEmulation;
    }
}

