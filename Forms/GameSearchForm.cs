using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Forms
{
    public partial class GameSearchForm : ThemedForm
    {
        private const int SearchDebounceMs = 300;
        private const int MaxNameWidthPx = 408;

        private List<AppSearchResult> _searchResults = new List<AppSearchResult>();
        private ulong? _selectedAppId;
        private CancellationTokenSource _searchCancellationTokenSource;
        private CancellationTokenSource _debounceCancellationTokenSource;
        private GameSearchFilterConnection _filterConnection;
        private bool _isSearching;
        private int _lastTooltipIndex = -1;

        public ulong? SelectedAppId => _selectedAppId;

        private bool HasValidSelection =>
            lstResults.SelectedIndex >= 0 && lstResults.SelectedIndex < _searchResults.Count;

        public GameSearchForm() : this(ServiceLocator.ThemeService)
        {
        }

        public GameSearchForm(ThemeService themeService)
            : base(themeService)
        {
            InitializeComponent();
            btnOK.Enabled = false;
            _filterConnection = SteamGameSearchService.CreateFilterConnection();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            txtSearch.Focus();
        }

        private async void OnSearchTextChanged(object sender, EventArgs e)
        {
            CancelAndDispose(ref _searchCancellationTokenSource);
            CancelAndDispose(ref _debounceCancellationTokenSource);

            var searchTerm = txtSearch.Text.Trim();
            if (string.IsNullOrEmpty(searchTerm))
            {
                ClearResults();
                return;
            }

            _debounceCancellationTokenSource = new CancellationTokenSource();
            var debounceToken = _debounceCancellationTokenSource.Token;

            try
            {
                await Task.Delay(SearchDebounceMs, debounceToken).ConfigureAwait(false);
                var stillCurrent = false;
                RunOnUiThread(() =>
                {
                    if (IsDisposed || Disposing)
                        return;
                    stillCurrent = string.Equals(txtSearch.Text.Trim(), searchTerm, StringComparison.Ordinal);
                });
                if (!stillCurrent)
                    return;
                await PerformSearchAsync(searchTerm).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex) when (!IsDisposed && !Disposing)
            {
                UpdateStatus($"Error: {ex.Message}");
                Program.LogService?.LogError("Search error", ex);
            }
        }

        private static void CancelAndDispose(ref CancellationTokenSource cts)
        {
            if (cts == null)
                return;
            cts.Cancel();
            cts.Dispose();
            cts = null;
        }

        private void ClearResults()
        {
            lstResults.Items.Clear();
            _searchResults.Clear();
            _selectedAppId = null;
            _lastTooltipIndex = -1;
            UpdateStatus("");
            ClearSelectedGameInfo();
        }

        private void UpdateStatus(string message)
        {
            if (InvokeRequired)
            {
                if (IsDisposed || Disposing)
                    return;
                Invoke(new Action<string>(UpdateStatus), message);
                return;
            }
            lblStatus.Text = message ?? string.Empty;
        }

        private void ClearSelectedGameInfo()
        {
            if (InvokeRequired)
            {
                if (IsDisposed || Disposing)
                    return;
                Invoke(new Action(ClearSelectedGameInfo));
                return;
            }
            lblSelectedAppId.Text = "";
            lblSelectedName.Text = "";
            toolTip.SetToolTip(lblSelectedAppId, null);
            toolTip.SetToolTip(lblSelectedName, null);
        }

        private void UpdateSelectedGameInfo(ulong appId, string name)
        {
            if (InvokeRequired)
            {
                if (IsDisposed || Disposing)
                    return;
                Invoke(new Action<ulong, string>(UpdateSelectedGameInfo), appId, name);
                return;
            }

            string displayName = TruncateNameToFit(name, lblSelectedName.Font, MaxNameWidthPx);
            lblSelectedAppId.Text = "AppId: " + appId;
            lblSelectedName.Text = displayName;
            toolTip.SetToolTip(lblSelectedAppId, "AppId: " + appId);
            toolTip.SetToolTip(lblSelectedName, "Name: \"" + (name ?? string.Empty) + "\"");
        }

        private void RunOnUiThread(Action action)
        {
            if (IsDisposed || Disposing)
                return;
            if (InvokeRequired)
                Invoke(action);
            else
                action();
        }

        private void SetSearchingState(bool isSearching)
        {
            RunOnUiThread(() =>
            {
                _isSearching = isSearching;
                btnOK.Enabled = HasValidSelection;
                if (isSearching)
                    UpdateStatus("Searching...");
            });
        }

        // Only the active search may leave the Searching state (superseded searches must not clear it).
        private void CompleteSearchingState(CancellationTokenSource searchCts)
        {
            RunOnUiThread(() =>
            {
                if (!ReferenceEquals(_searchCancellationTokenSource, searchCts))
                    return;

                _isSearching = false;
                btnOK.Enabled = HasValidSelection;

                // Keep error text; only replace the Searching placeholder.
                if (!string.Equals(lblStatus.Text, "Searching...", StringComparison.Ordinal))
                    return;

                if (_searchResults.Count == 0 && !string.IsNullOrEmpty(txtSearch.Text.Trim()))
                    UpdateStatus("No results found");
                else
                    UpdateStatus("");
            });
        }

        private async Task PerformSearchAsync(string searchTerm)
        {
            CancelAndDispose(ref _searchCancellationTokenSource);
            _searchCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                ServiceLocator.ApplicationLifetimeToken);
            CancellationTokenSource searchCts = _searchCancellationTokenSource;
            var cancellationToken = searchCts.Token;

            try
            {
                SetSearchingState(true);
                RunOnUiThread(() =>
                {
                    if (IsDisposed || Disposing)
                        return;
                    lstResults.Items.Clear();
                    _searchResults.Clear();
                    _selectedAppId = null;
                    _lastTooltipIndex = -1;
                    btnOK.Enabled = false;
                    ClearSelectedGameInfo();
                });

                Program.LogService?.LogDebug($"Starting search for: {searchTerm}");

                var searchToken = cancellationToken;
                IProgress<IReadOnlyList<AppSearchResult>> progress = null;
                RunOnUiThread(() =>
                {
                    // Capture the WinForms sync context so live hits keep "Searching..." until the await completes.
                    progress = new Progress<IReadOnlyList<AppSearchResult>>(liveResults =>
                    {
                        if (searchToken.IsCancellationRequested || IsDisposed || Disposing)
                            return;
                        ApplySearchResults(liveResults);
                    });
                });

                var results = await SteamGameSearchService
                    .SearchByNameAsync(
                        searchTerm,
                        cancellationToken: cancellationToken,
                        progress: progress,
                        filterConnection: _filterConnection)
                    .ConfigureAwait(false);

                Program.LogService?.LogDebug($"Search returned {results?.Count ?? 0} results");

                if (cancellationToken.IsCancellationRequested || IsDisposed || Disposing)
                    return;

                // Apply final list on the UI thread before clearing Searching.
                RunOnUiThread(() =>
                {
                    if (IsDisposed || Disposing || cancellationToken.IsCancellationRequested)
                        return;
                    if (!ReferenceEquals(_searchCancellationTokenSource, searchCts))
                        return;
                    ApplySearchResults(results);
                });
            }
            catch (OperationCanceledException)
            {
                // Superseded by a newer query while typing — keep status as-is.
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested && !IsDisposed && !Disposing)
                {
                    var errorMsg = $"Search error: {ex.Message}";
                    if (ex.InnerException != null)
                        errorMsg += $" ({ex.InnerException.Message})";
                    UpdateStatus(errorMsg);
                    Program.LogService?.LogError($"Search error: {ex.Message}", ex);
                }
            }
            finally
            {
                if (!IsDisposed && !Disposing)
                    CompleteSearchingState(searchCts);
            }
        }

        private void ApplySearchResults(IReadOnlyList<AppSearchResult> results)
        {
            if (IsDisposed || Disposing)
                return;

            if (InvokeRequired)
            {
                Invoke(new Action(() => ApplySearchResults(results)));
                return;
            }

            var nextResults = results != null
                ? new List<AppSearchResult>(results)
                : new List<AppSearchResult>();

            if (SameResultIds(_searchResults, nextResults))
            {
                if (_isSearching)
                    UpdateStatus("Searching...");
                return;
            }

            ulong? previousAppId = _selectedAppId;
            bool canAddInPlace = _searchResults.Count > 0
                && nextResults.Count >= _searchResults.Count
                && IsSubsetByAppId(nextResults, _searchResults);

            if (!canAddInPlace)
            {
                _searchResults = nextResults;
                _lastTooltipIndex = -1;
                lstResults.BeginUpdate();
                try
                {
                    lstResults.Items.Clear();
                    if (_searchResults.Count > 0)
                        lstResults.Items.AddRange(_searchResults.ToArray());
                }
                finally
                {
                    lstResults.EndUpdate();
                }
            }
            else
            {
                lstResults.BeginUpdate();
                try
                {
                    for (int i = 0; i < nextResults.Count; i++)
                    {
                        if (i < _searchResults.Count && _searchResults[i].AppId == nextResults[i].AppId)
                            continue;

                        int existing = IndexOfAppId(_searchResults, nextResults[i].AppId);
                        if (existing >= 0)
                        {
                            var moved = _searchResults[existing];
                            _searchResults.RemoveAt(existing);
                            lstResults.Items.RemoveAt(existing);
                            _searchResults.Insert(i, moved);
                            lstResults.Items.Insert(i, moved);
                        }
                        else
                        {
                            _searchResults.Insert(i, nextResults[i]);
                            lstResults.Items.Insert(i, nextResults[i]);
                        }
                    }
                }
                finally
                {
                    lstResults.EndUpdate();
                }
            }

            FinishDisplayedResultsUpdate(previousAppId);
        }

        private void FinishDisplayedResultsUpdate(ulong? previousAppId)
        {
            if (_searchResults.Count == 0)
            {
                _selectedAppId = null;
                btnOK.Enabled = false;
                ClearSelectedGameInfo();
                if (_isSearching)
                    UpdateStatus("Searching...");
                else
                    UpdateStatus("No results found");
                return;
            }

            int restoreIndex = -1;
            if (previousAppId.HasValue)
                restoreIndex = IndexOfAppId(_searchResults, previousAppId.Value);

            if (restoreIndex >= 0)
            {
                if (lstResults.SelectedIndex != restoreIndex)
                    lstResults.SelectedIndex = restoreIndex;
            }
            else if (lstResults.SelectedIndex < 0 || lstResults.SelectedIndex >= _searchResults.Count)
            {
                lstResults.SelectedIndex = 0;
            }

            if (_isSearching)
                UpdateStatus("Searching...");
            else
                UpdateStatus("");

            ShowSelectedGameInfo();
        }

        private static bool IsSubsetByAppId(List<AppSearchResult> current, List<AppSearchResult> next)
        {
            if (current == null || next == null)
                return false;
            for (int i = 0; i < next.Count; i++)
            {
                if (IndexOfAppId(current, next[i].AppId) < 0)
                    return false;
            }
            return true;
        }

        private static bool SameResultIds(List<AppSearchResult> current, List<AppSearchResult> next)
        {
            if (current == null || next == null)
                return current == next;
            if (current.Count != next.Count)
                return false;
            for (int i = 0; i < current.Count; i++)
            {
                if (current[i].AppId != next[i].AppId)
                    return false;
            }
            return true;
        }

        private static int IndexOfAppId(List<AppSearchResult> results, ulong appId)
        {
            if (results == null)
                return -1;
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].AppId == appId)
                    return i;
            }
            return -1;
        }

        private void OnSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.SuppressKeyPress = true;
            if (HasValidSelection)
                OnOk_Click(sender, e);
            else if (lstResults.Items.Count > 0)
                lstResults.SelectedIndex = 0;
        }

        private static string TruncateNameToFit(string name, Font font, int maxWidthPx)
        {
            const string prefix = "Name: \"";
            const string suffix = "\"";
            var prefixW = TextRenderer.MeasureText(prefix, font).Width;
            var suffixW = TextRenderer.MeasureText(suffix, font).Width;
            var availableForName = Math.Max(20, maxWidthPx - prefixW - suffixW);

            var displayName = name ?? string.Empty;
            if (string.IsNullOrEmpty(displayName))
                return prefix + suffix;
            if (TextRenderer.MeasureText(displayName, font).Width <= availableForName)
                return prefix + displayName + suffix;

            const string ellipsis = "...";
            for (var len = displayName.Length - 1; len > 0; len--)
            {
                var truncated = displayName.Substring(0, len) + ellipsis;
                if (TextRenderer.MeasureText(truncated, font).Width <= availableForName)
                    return prefix + truncated + suffix;
            }
            return prefix + ellipsis + suffix;
        }

        private void OnResultsSelectedIndexChanged(object sender, EventArgs e)
        {
            if (HasValidSelection)
            {
                ShowSelectedGameInfo();
            }
            else
            {
                _selectedAppId = null;
                btnOK.Enabled = false;
                ClearSelectedGameInfo();
                if (_isSearching)
                    UpdateStatus("Searching...");
                else if (_searchResults.Count == 0 && !string.IsNullOrEmpty(txtSearch.Text.Trim()))
                    UpdateStatus("No results found");
                else
                    UpdateStatus("");
            }
        }

        private void ShowSelectedGameInfo()
        {
            if (!HasValidSelection)
                return;

            var selectedResult = _searchResults[lstResults.SelectedIndex];
            _selectedAppId = selectedResult.AppId;
            btnOK.Enabled = true;
            UpdateSelectedGameInfo(selectedResult.AppId, selectedResult.Name);
        }

        private void AcceptSelection()
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void OnResultsDoubleClick(object sender, EventArgs e)
        {
            if (HasValidSelection)
                AcceptSelection();
        }

        private void OnResultsMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;
            var index = lstResults.IndexFromPoint(e.Location);
            if (index >= 0 && index < _searchResults.Count)
                lstResults.SelectedIndex = index;
        }

        private void OnResultsMouseMove(object sender, MouseEventArgs e)
        {
            var index = lstResults.IndexFromPoint(e.Location);
            if (index == _lastTooltipIndex)
                return;
            _lastTooltipIndex = index;
            if (index >= 0 && index < _searchResults.Count)
            {
                var result = _searchResults[index];
                toolTip.SetToolTip(lstResults, $"AppId: {result.AppId}{Environment.NewLine}Name: \"{result.Name}\"");
            }
            else
            {
                toolTip.SetToolTip(lstResults, "");
            }
        }

        private void OnListContextOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = !HasValidSelection;
        }

        private void CopySelectedResult(Func<AppSearchResult, string> getText)
        {
            if (!HasValidSelection)
                return;
            try
            {
                Clipboard.SetText(getText(_searchResults[lstResults.SelectedIndex]) ?? string.Empty);
            }
            catch
            {
            }
        }

        private void OnCopyAppId_Click(object sender, EventArgs e) =>
            CopySelectedResult(r => r.AppId.ToString());

        private void OnCopyName_Click(object sender, EventArgs e) =>
            CopySelectedResult(r => r.Name ?? string.Empty);

        private void OnOk_Click(object sender, EventArgs e)
        {
            if (HasValidSelection)
                AcceptSelection();
            else
                AppTaskDialogHelper.ShowOk(this, "Please select a game from the list.", MessageBoxIcon.Information);
        }

        private void OnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CancelAndDispose(ref _searchCancellationTokenSource);
                CancelAndDispose(ref _debounceCancellationTokenSource);
                if (_filterConnection != null)
                {
                    _filterConnection.Dispose();
                    _filterConnection = null;
                }
                components?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
