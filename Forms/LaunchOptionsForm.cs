using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Services;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu;

namespace SmartGoldbergEmu.Forms
{
    /// <summary>
    /// Form for selecting how to launch a game. The selected option overrides the game launch
    /// with its Executable (process path), Parameters (arguments), and WorkingDir.
    /// </summary>
    public partial class LaunchOptionsForm : Form
    {
        /// <summary>
        /// The selected launch option. When set, overrides the game launch with Executable, Parameters, WorkingDir.
        /// </summary>
        public LaunchOption SelectedOption { get; private set; }
        public DialogResult Result { get; private set; }
        public bool SkipLauncher { get; private set; } = false;
        public bool ExcludeConfigType { get; private set; } = true;

        private List<LaunchOption> _allLaunchOptions;
        private readonly ThemeService _themeService;
        private readonly AppDataService _appDataService;
        private readonly GameConfig _game;
        private DateTime _formLoadTime;
        private System.Windows.Forms.Timer _clipboardFeedbackTimer;

        public LaunchOptionsForm()
        {
            InitializeComponent();
            _themeService = ServiceLocator.ThemeService;
            _appDataService = ServiceLocator.AppDataService;
            _game = null;
        }

        public LaunchOptionsForm(GameConfig game, List<LaunchOption> launchOptions)
        {
            InitializeComponent();

            _game = game;
            
            _themeService = ServiceLocator.ThemeService;
            _appDataService = ServiceLocator.AppDataService;
            
            // Subscribe to theme changes
            _themeService.ThemeChanged += ThemeService_ThemeChanged;
            
            // Apply saved theme on startup
            ApplyTheme();
            
            // Ensure ListBox uses item ToString() for display
            lstLaunchOptions.DisplayMember = null;
            
            _allLaunchOptions = launchOptions ?? new List<LaunchOption>();
            
            // Set labels text
            lblTitle.Text = $"Launch Options for {game?.AppName ?? "Game"}";
            lblInstruction.Text = "Select how to launch:";

            var launchOptionService = ServiceLocator.LaunchOptionService;
            // Saved FullLaunchOptions: false => ExcludeConfigType (same as ShouldExcludeRestrictedLaunchTypes).
            ExcludeConfigType = launchOptionService.ShouldExcludeRestrictedLaunchTypes(_appDataService);
            chkShowBetaOptions.Checked = !ExcludeConfigType;

            // Initial list matches pre-dialog auto-pick filter; checkbox handler re-filters when Show Extra toggles.
            var filteredOptions = launchOptionService.FilterLaunchOptionsForCurrentSettings(_allLaunchOptions, _appDataService);
            PopulateOptions(filteredOptions);

            ServiceLocator.LogService.LogDebug(
                $"LaunchOptionsForm: {game?.AppName ?? "Game"}, {filteredOptions.Count} option(s), ExcludeConfigType={ExcludeConfigType}");

            // Set default selection
            if (lstLaunchOptions.Items.Count > 0)
                lstLaunchOptions.SelectedIndex = 0;
            else
                ServiceLocator.LogService.LogWarning("Launch options list is empty");

            // Subscribe to Load event to set focus on list box
            this.Load += LaunchOptionsForm_Load;
        }

        private void LaunchOptionsForm_Load(object sender, EventArgs e)
        {
            // Record load time to prevent immediate auto-triggering
            _formLoadTime = DateTime.Now;
            
            // Set focus on list box to prevent accidental Enter key presses on the Launch button
            lstLaunchOptions.Focus();
        }

        private void OnCopyLaunchDetails_Click(object sender, EventArgs e)
        {
            if (_game == null || !(lstLaunchOptions.SelectedItem is LaunchOptionDisplayItem displayItem))
                return;
            var resolved = ServiceLocator.GameLaunchService.GetResolvedLaunchCommand(_game, displayItem.LaunchOption);
            if (resolved == null) return;

            var parts = new List<string>();
            if (!string.IsNullOrEmpty(resolved.WorkingDirectory))
            {
                parts.Add("Workingdir:");
                parts.Add("\"" + resolved.WorkingDirectory + "\"");
            }
            if (!string.IsNullOrEmpty(resolved.ExecutablePath))
            {
                parts.Add("Executable:");
                parts.Add("\"" + resolved.ExecutablePath + "\"");
            }
            if (!string.IsNullOrEmpty(resolved.Arguments))
            {
                parts.Add("Arguments:");
                parts.Add("\"" + resolved.Arguments + "\"");
            }
            var toCopy = parts.Count > 0 ? string.Join(Environment.NewLine, parts) : "";
            if (string.IsNullOrEmpty(toCopy)) return;

            try
            {
                Clipboard.SetText(toCopy);
                lblDetails.Text = "Copied to clipboard!";
                StopAndDisposeClipboardFeedbackTimer();
                _clipboardFeedbackTimer = new System.Windows.Forms.Timer { Interval = 1500 };
                _clipboardFeedbackTimer.Tick += ClipboardFeedbackTimer_Tick;
                _clipboardFeedbackTimer.Start();
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogWarning("Could not copy launch details to clipboard: " + ex.Message);
                lblDetails.Text = "Could not copy to clipboard (clipboard busy or unavailable).";
            }
        }

        private void ClipboardFeedbackTimer_Tick(object sender, EventArgs e)
        {
            StopAndDisposeClipboardFeedbackTimer();
            if (IsDisposed || Disposing)
                return;
            UpdateDetailsLabelForCurrentSelection();
        }

        private void StopAndDisposeClipboardFeedbackTimer()
        {
            if (_clipboardFeedbackTimer == null)
                return;
            _clipboardFeedbackTimer.Stop();
            _clipboardFeedbackTimer.Tick -= ClipboardFeedbackTimer_Tick;
            _clipboardFeedbackTimer.Dispose();
            _clipboardFeedbackTimer = null;
        }

        private void chkShowBetaOptions_CheckedChanged(object sender, EventArgs e)
        {
            // Checked = full list (ExcludeConfigType = false); unchecked = hide restricted options (ExcludeConfigType = true)
            ExcludeConfigType = !chkShowBetaOptions.Checked;
            // Save the checkbox state to configuration (inverted)
            var settings = _appDataService.LoadApplicationSettings();
            settings.FullLaunchOptions = chkShowBetaOptions.Checked;
            _appDataService.SaveApplicationSettings(settings);
            
            var filteredOptions = ServiceLocator.LaunchOptionService.FilterLaunchOptionsForUi(
                _allLaunchOptions,
                ExcludeConfigType);
            PopulateOptions(filteredOptions);
            UpdateDetailsLabelForCurrentSelection();
            ServiceLocator.LogService.LogMessage(
                $"Launch options filter changed: ExcludeConfigType={ExcludeConfigType}, visible={filteredOptions.Count}");
            
            // Reset selection if current selection is no longer valid
            if (lstLaunchOptions.SelectedIndex >= lstLaunchOptions.Items.Count)
                lstLaunchOptions.SelectedIndex = lstLaunchOptions.Items.Count > 0 ? 0 : -1;
        }

        private void PopulateOptions(List<LaunchOption> launchOptions)
        {
            lstLaunchOptions.Items.Clear();
            
            foreach (var option in launchOptions)
            {
                // Create display description without arch/type suffixes
                var description = CreateEnhancedDescription(option);
                var displayItem = new LaunchOptionDisplayItem(option, description);
                lstLaunchOptions.Items.Add(displayItem);
            }
        }

        private string CreateEnhancedDescription(LaunchOption option)
        {
            if (option == null)
                return "Unknown Option";

            string baseText;
            if (!string.IsNullOrEmpty(option.Description))
                baseText = option.Description.Trim();
            else if (!string.IsNullOrEmpty(_game?.AppName))
                baseText = _game.AppName.Trim();
            else if (!string.IsNullOrEmpty(option.Executable))
                baseText = option.Executable.Trim();
            else
                baseText = "Unknown Option";

            return AppendShortLaunchTags(baseText, option);
        }

        /// <summary>
        /// Appends short bracket tags only for "extra" launch options (same as when Show Extra options reveals them): beta branch, config/dev types, etc.
        /// </summary>
        private static string AppendShortLaunchTags(string baseText, LaunchOption option)
        {
            if (option == null || string.IsNullOrEmpty(baseText))
                return baseText ?? string.Empty;
            if (!option.IsHiddenWhenFullLaunchOptionsOff())
                return baseText;

            var tags = new List<string>();
            if (!string.IsNullOrEmpty(option.BetaKey))
                tags.Add("beta");

            if (!string.IsNullOrEmpty(option.Type))
            {
                string t = option.Type.Trim();
                if (t.Equals(SteamPicsKeyNames.Default, StringComparison.OrdinalIgnoreCase))
                {
                }
                else if (t.Equals(SteamPicsKeyNames.LaunchOptionTypeBetaKey, StringComparison.OrdinalIgnoreCase) ||
                         t.Equals(SteamPicsKeyNames.LaunchOptionTypeBeta, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(option.BetaKey))
                        tags.Add("beta");
                }
                else if (t.Equals(SteamPicsKeyNames.LaunchOptionTypeDeveloper, StringComparison.OrdinalIgnoreCase))
                    tags.Add("dev");
                else
                    tags.Add(t.ToLowerInvariant());
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var unique = new List<string>();
            foreach (var tag in tags)
            {
                if (seen.Add(tag))
                    unique.Add(tag);
            }

            if (unique.Count == 0)
                return baseText;

            // Prefix tags before the name, using " - " separator.
            // Example: "[user] - My Custom Launch"
            return string.Join(" ", unique.Select(x => "[" + x + "]")) + " - " + baseText;
        }

        private void OnLaunch_Click(object sender, EventArgs e)
        {
            if (lstLaunchOptions.SelectedItem is LaunchOptionDisplayItem displayItem)
            {
                SelectedOption = displayItem.LaunchOption;
                Result = DialogResult.OK;
                SkipLauncher = false;
                this.DialogResult = DialogResult.OK;
                ServiceLocator.LogService.LogDebug(
                    $"LaunchOptionsForm: launch option '{SelectedOption.Description ?? SelectedOption.Executable ?? "(unnamed)"}'");
                this.Close();
            }
            else
            {
                ServiceLocator.LogService.LogWarning("Launch button clicked but no valid option selected");
            }
        }

        private void OnUseDefaultLaunch_Click(object sender, EventArgs e)
        {
            SelectedOption = null;
            Result = DialogResult.OK;
            SkipLauncher = true;
            this.DialogResult = DialogResult.OK;
            ServiceLocator.LogService.LogDebug("LaunchOptionsForm: skip launcher (default launch)");
            this.Close();
        }

        private void OnCancel_Click(object sender, EventArgs e)
        {
            Result = DialogResult.Cancel;
            SkipLauncher = false;
            this.DialogResult = DialogResult.Cancel;
            ServiceLocator.LogService.LogDebug("LaunchOptionsForm: cancelled");
            this.Close();
        }

        private void OnLaunchOptionsDoubleClick(object sender, EventArgs e)
        {
            if (lstLaunchOptions.SelectedItem != null)
            {
                OnLaunch_Click(sender, e);
            }
        }

        private void OnLaunchOptionsKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && lstLaunchOptions.SelectedItem != null)
            {
                // Prevent immediate triggering if form was just loaded (within 100ms)
                // This prevents accidental auto-launch from queued key events
                if ((DateTime.Now - _formLoadTime).TotalMilliseconds < 100)
                {
                    e.Handled = true;
                    return;
                }
                OnLaunch_Click(sender, e);
            }
        }

        private void OnLaunchOptionsSelectedIndexChanged(object sender, EventArgs e)
        {
            btnLaunch.Enabled = lstLaunchOptions.SelectedIndex >= 0;
            
            if (lstLaunchOptions.SelectedItem is LaunchOptionDisplayItem displayItem)
                UpdateDetailsLabel(displayItem.LaunchOption);
            else if (lblDetails != null)
            {
                lblDetails.Text = "Select an option to see details";
                toolTipLaunchDetails?.SetToolTip(lblDetails, null);
            }
        }

        private void UpdateDetailsLabel(LaunchOption option)
        {
            if (lblDetails == null) return;

            if (_game == null)
            {
                lblDetails.Text = "Select an option to see details";
                toolTipLaunchDetails?.SetToolTip(lblDetails, null);
                return;
            }

            var resolved = ServiceLocator.GameLaunchService.GetResolvedLaunchCommand(_game, option);
            if (resolved == null)
            {
                lblDetails.Text = "Select an option to see details";
                toolTipLaunchDetails?.SetToolTip(lblDetails, null);
                return;
            }

            const int maxWidthPx = 395;
            var font = lblDetails.Font;
            var lines = new List<string>();
            var fullLines = new List<string>();
            if (!string.IsNullOrEmpty(resolved.WorkingDirectory))
            {
                var val = "\"" + resolved.WorkingDirectory + "\"";
                lines.Add("Workingdir:");
                lines.Add(TruncateToFit(val, font, maxWidthPx));
                fullLines.Add("Workingdir:");
                fullLines.Add(val);
            }
            if (!string.IsNullOrEmpty(resolved.ExecutablePath))
            {
                var val = "\"" + resolved.ExecutablePath + "\"";
                lines.Add("Executable:");
                lines.Add(TruncateToFit(val, font, maxWidthPx));
                fullLines.Add("Executable:");
                fullLines.Add(val);
            }
            if (!string.IsNullOrEmpty(resolved.Arguments))
            {
                var val = "\"" + resolved.Arguments + "\"";
                lines.Add("Arguments:");
                lines.Add(TruncateToFit(val, font, maxWidthPx));
                fullLines.Add("Arguments:");
                fullLines.Add(val);
            }
            var displayText = lines.Count > 0 ? string.Join(Environment.NewLine, lines) : "Uses game default";
            var fullText = fullLines.Count > 0 ? string.Join(Environment.NewLine, fullLines) : null;
            lblDetails.Text = displayText;
            toolTipLaunchDetails?.SetToolTip(lblDetails, fullText);
        }

        private static string TruncateToFit(string value, Font font, int maxWidthPx)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (TextRenderer.MeasureText(value, font).Width <= maxWidthPx) return value;
            var ellipsis = "...\"";
            for (int len = value.Length - 1; len > 0; len--)
            {
                var truncated = value.Substring(0, len) + ellipsis;
                if (TextRenderer.MeasureText(truncated, font).Width <= maxWidthPx)
                    return truncated;
            }
            return ellipsis;
        }

        private void UpdateDetailsLabelForCurrentSelection()
        {
            if (lstLaunchOptions.SelectedItem is LaunchOptionDisplayItem displayItem)
                UpdateDetailsLabel(displayItem.LaunchOption);
            else if (lblDetails != null)
            {
                lblDetails.Text = "Select an option to see details";
                toolTipLaunchDetails?.SetToolTip(lblDetails, null);
            }
        }

        /// <summary>
        /// Helper class to display launch options with enhanced descriptions
        /// </summary>
        private class LaunchOptionDisplayItem
        {
            public LaunchOption LaunchOption { get; }
            public string DisplayText { get; }

            public LaunchOptionDisplayItem(LaunchOption launchOption, string displayText)
            {
                LaunchOption = launchOption;
                DisplayText = displayText;
            }

            public override string ToString()
            {
                return DisplayText;
            }
        }

        private void ApplyTheme()
        {
            try
            {
                _themeService.ApplyTheme(this);
                ConfigureLaunchDialogButtons();
                if (lblDetails != null)
                    lblDetails.ForeColor = Color.Gray;
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogWarning("Launch options theme apply failed: " + ex.Message);
            }
        }

        private void ConfigureLaunchDialogButtons()
        {
            ConfigureLaunchDialogButton(btnUseDefaultLaunch);
            ConfigureLaunchDialogButton(btnLaunch);
            ConfigureLaunchDialogButton(btnCancel);
        }

        private void ConfigureLaunchDialogButton(Button button)
        {
            if (button == null)
                return;

            button.TextAlign = ContentAlignment.MiddleCenter;
            button.UseCompatibleTextRendering = false;
            button.Padding = Padding.Empty;
            button.Paint -= LaunchDialogButton_Paint;
            button.Paint += LaunchDialogButton_Paint;
            button.Invalidate();
        }

        private void LaunchDialogButton_Paint(object sender, PaintEventArgs e)
        {
            var button = (Button)sender;
            ThemeColors colors = _themeService.GetThemeColors(_themeService.EffectiveTheme);
            Color backColor = button.Enabled ? colors.ControlBackground : colors.DisabledBackground;
            Color foreColor = button.Enabled ? colors.ControlForeground : colors.DisabledForeground;

            e.Graphics.Clear(backColor);
            var borderRect = new Rectangle(0, 0, button.Width - 1, button.Height - 1);
            ControlPaint.DrawBorder(e.Graphics, borderRect, colors.Border, ButtonBorderStyle.Solid);

            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(e.Graphics, button.Text, button.Font, button.ClientRectangle, foreColor, backColor, flags);
        }

        /// <summary>
        /// Handles theme changes from ThemeService.
        /// </summary>
        public void ThemeService_ThemeChanged(object sender, ThemeChangedEventArgs e)
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
            StopAndDisposeClipboardFeedbackTimer();
            // Unsubscribe from theme changes
            if (_themeService != null)
            {
                _themeService.ThemeChanged -= ThemeService_ThemeChanged;
            }

            Load -= LaunchOptionsForm_Load;
            base.OnFormClosed(e);
        }
    }
}
