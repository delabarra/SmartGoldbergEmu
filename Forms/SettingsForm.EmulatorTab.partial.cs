using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Forms
{
    partial class SettingsForm
    {
        private void WireEmulatorTab()
        {
            WireEmulatorTabChangeEvents();
            ApplyEmulatorTooltips();
        }

        private void ApplyEmulatorTooltips()
        {
            if (toolTip == null)
                return;

            var bindings = new (Control Control, string Text)[]
            {
                (chkSteamDeck, "1=Pretend the app is running on a Steam Deck."),
                (chkEnableAccountAvatar, "1=Enable account avatar functionality."),
                (chkEnableVoiceChat, "Enable experimental voice chat. May increase CPU use or cause crashes."),
                (chkModernAuthTicket, "1=Generate a modern auth ticket. Disable for very old games."),
                (chkGameCoordinatorToken, "1=Embed a Game Coordinator token in the new auth ticket."),
                (chkDisableLeaderboardsCreateUnknown,
                    "1=Do not auto-create unknown leaderboards when Steam_User_Stats::FindLeaderboard() is called."),
                (chkAllowUnknownStats, "1=Allow stats not defined in stats.json to be saved or updated."),
                (chkStatAchievementProgressFunctionality,
                    "1=Report achievement progress when a linked stat changes; may write often and show overlay popups."),
                (chkSaveOnlyHigherStatAchievementProgress,
                    "1=Save achievement progress from stats only when the new value is higher."),
                (numIconsPerIteration,
                    "How many achievement icons to load per callback (-1=off, 0=on demand). Each achievement uses two icons."),
                (chkRecordPlaytime, "1=Record playtime to playtime.txt under GSE Saves for this app."),
                (chkAchievementsBypass, "1=Force ISteamUserStats::SetAchievement() to return true (workaround for some games)."),
                (chkForceSteamhttpSuccess, "1=Force Steam_HTTP::SendHTTPRequest() to always succeed."),
                (chkDisableSteamoverlaygameidEnvVar, "1=Do not set SteamOverlayGameId (helps Steam Input for non-Steam shortcuts)."),
                (chkEnableSteamPreownedIds, "1=Add many Steam app IDs to owned DLC and installed-app lists (useful for Source games)."),
                (txtSteamGameStatsReportsDir, "Folder where ISteamGameStats reports are saved (empty=disabled). Path must be writable."),
                (chkFreeWeekend, "1=Enable free-weekend player bonuses some games check for."),
                (chkUse32BitInventoryItemIds, "1=Generate 32-bit inventory item IDs (Team Fortress 2 workaround).")
            };

            foreach (var binding in bindings)
                ToolTipHelper.SetIfPresent(toolTip, binding.Control, ToolTipHelper.FormatDescription(binding.Text));
        }

        private void LoadEmulatorSettings(MainSettings settings)
        {
            var s = settings ?? new MainSettings();
            chkModernAuthTicket.Checked = s.NewAppTicket;
            chkGameCoordinatorToken.Checked = s.GcToken;
            chkEnableAccountAvatar.Checked = s.EnableAccountAvatar;
            chkEnableVoiceChat.Checked = s.EnableVoiceChat;
            chkSteamDeck.Checked = s.SteamDeck;

            chkDisableLeaderboardsCreateUnknown.Checked = s.DisableLeaderboardsCreateUnknown;
            chkAllowUnknownStats.Checked = s.AllowUnknownStats;
            chkStatAchievementProgressFunctionality.Checked = s.StatAchievementProgressFunctionality;
            chkSaveOnlyHigherStatAchievementProgress.Checked = s.SaveOnlyHigherStatAchievementProgress;
            numIconsPerIteration.Value = ClampEmulatorNumeric(numIconsPerIteration, s.PaginatedAchievementsIcons);
            chkRecordPlaytime.Checked = s.RecordPlaytime;
            txtSteamGameStatsReportsDir.Text = s.SteamGameStatsReportsDir ?? string.Empty;

            chkAchievementsBypass.Checked = s.AchievementsBypass;
            chkForceSteamhttpSuccess.Checked = s.ForceSteamhttpSuccess;
            chkDisableSteamoverlaygameidEnvVar.Checked = s.DisableSteamoverlaygameidEnvVar;
            chkEnableSteamPreownedIds.Checked = s.EnableSteamPreownedIds;
            chkFreeWeekend.Checked = s.FreeWeekend;
            chkUse32BitInventoryItemIds.Checked = s.Use32BitInventoryItemIds;
        }

        private MainSettings BuildEmulatorSettings()
        {
            return new MainSettings
            {
                NewAppTicket = chkModernAuthTicket.Checked,
                GcToken = chkGameCoordinatorToken.Checked,
                EnableAccountAvatar = chkEnableAccountAvatar.Checked,
                EnableVoiceChat = chkEnableVoiceChat.Checked,
                SteamDeck = chkSteamDeck.Checked,
                DisableLeaderboardsCreateUnknown = chkDisableLeaderboardsCreateUnknown.Checked,
                AllowUnknownStats = chkAllowUnknownStats.Checked,
                StatAchievementProgressFunctionality = chkStatAchievementProgressFunctionality.Checked,
                SaveOnlyHigherStatAchievementProgress = chkSaveOnlyHigherStatAchievementProgress.Checked,
                PaginatedAchievementsIcons = (int)numIconsPerIteration.Value,
                RecordPlaytime = chkRecordPlaytime.Checked,
                SteamGameStatsReportsDir = txtSteamGameStatsReportsDir.Text.Trim(),
                AchievementsBypass = chkAchievementsBypass.Checked,
                ForceSteamhttpSuccess = chkForceSteamhttpSuccess.Checked,
                DisableSteamoverlaygameidEnvVar = chkDisableSteamoverlaygameidEnvVar.Checked,
                EnableSteamPreownedIds = chkEnableSteamPreownedIds.Checked,
                FreeWeekend = chkFreeWeekend.Checked,
                Use32BitInventoryItemIds = chkUse32BitInventoryItemIds.Checked
            };
        }

        private static bool AreEmulatorSettingsEqual(MainSettings a, MainSettings b)
        {
            if (a == null && b == null)
                return true;
            if (a == null || b == null)
                return false;

            return a.NewAppTicket == b.NewAppTicket &&
                   a.GcToken == b.GcToken &&
                   a.SteamDeck == b.SteamDeck &&
                   a.EnableAccountAvatar == b.EnableAccountAvatar &&
                   a.EnableVoiceChat == b.EnableVoiceChat &&
                   a.DisableLeaderboardsCreateUnknown == b.DisableLeaderboardsCreateUnknown &&
                   a.AllowUnknownStats == b.AllowUnknownStats &&
                   a.StatAchievementProgressFunctionality == b.StatAchievementProgressFunctionality &&
                   a.SaveOnlyHigherStatAchievementProgress == b.SaveOnlyHigherStatAchievementProgress &&
                   a.PaginatedAchievementsIcons == b.PaginatedAchievementsIcons &&
                   a.RecordPlaytime == b.RecordPlaytime &&
                   a.AchievementsBypass == b.AchievementsBypass &&
                   a.ForceSteamhttpSuccess == b.ForceSteamhttpSuccess &&
                   a.DisableSteamoverlaygameidEnvVar == b.DisableSteamoverlaygameidEnvVar &&
                   a.EnableSteamPreownedIds == b.EnableSteamPreownedIds &&
                   string.Equals(a.SteamGameStatsReportsDir ?? string.Empty, b.SteamGameStatsReportsDir ?? string.Empty, StringComparison.Ordinal) &&
                   a.FreeWeekend == b.FreeWeekend &&
                   a.Use32BitInventoryItemIds == b.Use32BitInventoryItemIds;
        }

        private void WireEmulatorTabChangeEvents()
        {
            foreach (Control c in GetEmulatorTabChildControls(tabEmulator))
            {
                if (c is CheckBox)
                    ((CheckBox)c).CheckedChanged += OnEmulatorSettingChanged;
                else if (c is NumericUpDown)
                    ((NumericUpDown)c).ValueChanged += OnEmulatorSettingChanged;
                else if (c is TextBox)
                    ((TextBox)c).TextChanged += OnEmulatorSettingChanged;
            }
        }

        private static IEnumerable<Control> GetEmulatorTabChildControls(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (Control nested in GetEmulatorTabChildControls(child))
                    yield return nested;
            }
        }

        private void OnEmulatorSettingChanged(object sender, EventArgs e)
        {
            if (!_isLoading)
                CheckForChanges();
        }

        private void OnBrowseEmulatorSteamGameStatsReportsDir_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select Steam game stats reports folder";
                dialog.ShowNewFolderButton = true;
                if (!string.IsNullOrEmpty(txtSteamGameStatsReportsDir.Text))
                    dialog.SelectedPath = txtSteamGameStatsReportsDir.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    txtSteamGameStatsReportsDir.Text = dialog.SelectedPath;
            }
        }

        private static decimal ClampEmulatorNumeric(NumericUpDown nud, int value)
        {
            if (value < nud.Minimum)
                return nud.Minimum;
            if (value > nud.Maximum)
                return nud.Maximum;
            return value;
        }
    }
}
