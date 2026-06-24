using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Helpers
{
    /// <summary>
    /// Helper class for managing ListView column width and themed header drawing.
    /// </summary>
    public static class ListViewColumnHelper
    {
        private const int LvmGetOrigin = 0x1000 + 41;
        private const int LvmSetExtendedListViewStyle = 0x1000 + 54;
        private const int LvsExDoubleBuffer = 0x00010000;

        /// <summary>Require content this many pixels past the client before subtracting vertical scrollbar width (reduces flip-flop).</summary>
        private const int VerticalScrollbarLayoutHysteresisPx = 8;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref NativePoint lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, EntryPoint = "SendMessage")]
        private static extern IntPtr SendMessageIntPtr(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        // WinForms double-buffer plus LVS_EX_DOUBLEBUFFER to cut selection flicker when focus leaves the list (menus, dialogs).
        public static void ReducePaintFlicker(ListView listView)
        {
            if (listView == null)
                return;

            var doubleBuffered = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            doubleBuffered?.SetValue(listView, true, null);

            void ApplyNativeDoubleBuffer()
            {
                if (!listView.IsHandleCreated)
                    return;
                SendMessageIntPtr(listView.Handle, LvmSetExtendedListViewStyle, (IntPtr)LvsExDoubleBuffer, (IntPtr)LvsExDoubleBuffer);
            }

            if (listView.IsHandleCreated)
                ApplyNativeDoubleBuffer();
            else
                listView.HandleCreated += (sender, e) => ApplyNativeDoubleBuffer();
        }

        public static int GetListViewOriginX(ListView listView)
        {
            if (listView == null || !listView.IsHandleCreated)
                return 0;
            var pt = new NativePoint();
            SendMessage(listView.Handle, LvmGetOrigin, IntPtr.Zero, ref pt);
            return pt.X;
        }

        public static int GetColumnIndexFromClientX(ListView listView, int clientX)
        {
            if (listView == null || listView.Columns.Count == 0)
                return -1;

            int originX = GetListViewOriginX(listView);
            int logicalX = clientX - originX;

            var ordered = listView.Columns.Cast<ColumnHeader>().OrderBy(c => c.DisplayIndex).ToList();
            int x = 0;
            foreach (ColumnHeader ch in ordered)
            {
                if (logicalX < x + ch.Width)
                    return ch.Index;
                x += ch.Width;
            }

            return ordered[ordered.Count - 1].Index;
        }

        public static bool TryGetSubItemCellBoundsByLayout(ListView listView, int itemIndex, int columnIndex, out Rectangle bounds)
        {
            bounds = Rectangle.Empty;
            if (listView == null || !listView.IsHandleCreated || itemIndex < 0)
                return false;
            if (columnIndex < 0 || columnIndex >= listView.Columns.Count)
                return false;

            Rectangle rowRect;
            try
            {
                rowRect = listView.GetItemRect(itemIndex, ItemBoundsPortion.Entire);
            }
            catch (ArgumentException)
            {
                return false;
            }

            var ordered = listView.Columns.Cast<ColumnHeader>().OrderBy(c => c.DisplayIndex).ToList();
            int logicalLeft = 0;
            ColumnHeader target = null;
            foreach (ColumnHeader ch in ordered)
            {
                if (ch.Index == columnIndex)
                {
                    target = ch;
                    break;
                }
                logicalLeft += ch.Width;
            }

            if (target == null)
                return false;

            int originX = GetListViewOriginX(listView);
            int cellLeft = logicalLeft + originX;
            bounds = new Rectangle(cellLeft, rowRect.Top, target.Width, rowRect.Height);
            return bounds.Width > 0 && bounds.Height > 0;
        }

        /// <summary>
        /// Clamps proposed width during user drag on Name, App ID, or Path.
        /// </summary>
        public static void ClampDetailsDataColumnWidthChanging(ColumnWidthChangingEventArgs e)
        {
            if (e == null)
                return;
            if (e.NewWidth < ApplicationConstants.DetailsColumnWidthMin)
                e.NewWidth = ApplicationConstants.DetailsColumnWidthMin;
            else if (e.NewWidth > ApplicationConstants.DetailsColumnWidthMax)
                e.NewWidth = ApplicationConstants.DetailsColumnWidthMax;
        }

        /// <summary>
        /// Sizes the Path column to the client width left after Name and App ID (main game Details list).
        /// </summary>
        public static void UpdateDetailsGameListColumnLayout(ListView listView)
        {
            if (listView == null)
                return;

            ColumnHeader pathColumn = null;
            int fixedWidth = 0;
            foreach (ColumnHeader col in listView.Columns)
            {
                if (col.Text == ApplicationConstants.ColumnPath)
                    pathColumn = col;
                else
                    fixedWidth += col.Width;
            }

            if (pathColumn == null)
                return;

            int availableWidth = listView.ClientSize.Width - fixedWidth;
            availableWidth -= GetVerticalScrollbarWidthIfNeeded(listView);

            int pathWidth = Math.Max(ApplicationConstants.DetailsColumnWidthMin, availableWidth);

            listView.BeginUpdate();
            try
            {
                pathColumn.Width = pathWidth;
            }
            finally
            {
                listView.EndUpdate();
            }
        }

        /// <summary>
        /// Updates the last column width to fill remaining client width (lists with a trailing stretch column).
        /// </summary>
        public static void UpdateLastColumnWidth(ListView listView)
        {
            if (listView == null || listView.Columns.Count == 0)
                return;

            int fillerIndex = -1;
            for (int i = 0; i < listView.Columns.Count; i++)
            {
                if (string.IsNullOrEmpty(listView.Columns[i].Text))
                {
                    fillerIndex = i;
                    break;
                }
            }

            if (fillerIndex < 0)
                fillerIndex = listView.Columns.Count - 1;

            listView.BeginUpdate();
            try
            {
                int totalWidth = 0;
                for (int i = 0; i < listView.Columns.Count; i++)
                {
                    if (i != fillerIndex)
                        totalWidth += listView.Columns[i].Width;
                }

                int availableWidth = listView.ClientSize.Width - totalWidth;
                availableWidth -= GetVerticalScrollbarWidthIfNeeded(listView);

                if (availableWidth > 0)
                    listView.Columns[fillerIndex].Width = availableWidth;
            }
            finally
            {
                listView.EndUpdate();
            }
        }

        private static int GetVerticalScrollbarWidthIfNeeded(ListView listView)
        {
            if (listView == null || !listView.IsHandleCreated || listView.Items.Count == 0)
                return 0;

            try
            {
                int hItem = listView.GetItemRect(0).Height;
                if (hItem > 0 && listView.ClientSize.Height + VerticalScrollbarLayoutHysteresisPx < hItem * listView.Items.Count)
                    return SystemInformation.VerticalScrollBarWidth;
            }
            catch (ArgumentException)
            {
            }

            return 0;
        }

        /// <summary>
        /// Draws a themed column header with sort indicators.
        /// </summary>
        /// <param name="e">The draw list view column header event arguments.</param>
        /// <param name="themeService">The theme service to get colors.</param>
        /// <param name="appDataService">The app data service for sort indicators; pass null to draw headers without sort arrows.</param>
        public static void DrawThemedColumnHeader(
            DrawListViewColumnHeaderEventArgs e,
            ThemeService themeService,
            AppDataService appDataService)
        {
            if (e == null || themeService == null)
                return;

            // Get theme colors
            var effectiveTheme = themeService.EffectiveTheme;
            var colors = themeService.GetThemeColors(effectiveTheme);

            using (var brush = new SolidBrush(colors.ListViewColumnHeaderBackground))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            string sortIndicator = string.Empty;
            if (appDataService != null)
            {
                // Determine if this column is currently sorted (check by column text, not index, since columns can be reordered)
                var sortBy = appDataService.GetSortBy();
                var sortDirection = appDataService.GetSortDirection();

                if (e.Header.Text == ApplicationConstants.ColumnName && sortBy == ApplicationConstants.SortByName)
                {
                    sortIndicator = sortDirection == ApplicationConstants.SortDirectionAsc ? " ▲" : " ▼";
                }
                else if (e.Header.Text == ApplicationConstants.ColumnAppId && sortBy == ApplicationConstants.SortByAppId)
                {
                    sortIndicator = sortDirection == ApplicationConstants.SortDirectionAsc ? " ▲" : " ▼";
                }
            }

            // Prepare text with sort indicator
            string headerText = e.Header.Text + sortIndicator;

            // Calculate text bounds (leave space for sort indicator)
            Rectangle textBounds = e.Bounds;
            textBounds.X += 4; // Left padding
            textBounds.Width -= 8; // Right padding

            // Draw column header text
            TextRenderer.DrawText(e.Graphics, headerText, e.Font, textBounds,
                colors.ControlForeground, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine);

            // Draw border
            using (var pen = new Pen(colors.Border))
            {
                e.Graphics.DrawRectangle(pen, e.Bounds);
            }
        }

        /// <summary>
        /// Handles column reordering and saves the column order.
        /// </summary>
        /// <param name="listView">The ListView that was reordered.</param>
        /// <param name="appDataService">The app data service to save column order.</param>
        /// <param name="viewMode">The current view mode (only saves if "Details").</param>
        public static void HandleColumnReordered(ListView listView, AppDataService appDataService, string viewMode)
        {
            if (listView == null || appDataService == null)
                return;

            // Only save column order in Details view
            if (viewMode != ApplicationConstants.ViewModeDetails)
                return;

            // Build column order string from current display indices
            var columns = listView.Columns.Cast<ColumnHeader>()
                .Where(c => !string.IsNullOrEmpty(c.Text)) // Exclude filler column
                .OrderBy(c => c.DisplayIndex)
                .Select(c => c.Text)
                .ToArray();

            if (columns.Length > 0)
            {
                var columnOrder = string.Join(",", columns);
                appDataService.SetDetailsColumnOrder(columnOrder);
            }
        }

        /// <summary>
        /// Builds canonical Name,App ID,Path width CSV from the list (excludes filler / empty header).
        /// </summary>
        public static bool TryFormatDetailsDataColumnWidthsCsv(ListView listView, out string normalizedCsv)
        {
            normalizedCsv = null;
            if (listView == null)
                return false;

            int? wName = null, wAppId = null, wPath = null;
            foreach (ColumnHeader col in listView.Columns)
            {
                if (string.IsNullOrEmpty(col.Text))
                    continue;
                if (col.Text == ApplicationConstants.ColumnName)
                    wName = col.Width;
                else if (col.Text == ApplicationConstants.ColumnAppId)
                    wAppId = col.Width;
                else if (col.Text == ApplicationConstants.ColumnPath)
                    wPath = col.Width;
            }

            if (wName == null || wAppId == null || wPath == null)
                return false;

            var raw = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}", wName.Value, wAppId.Value, wPath.Value);
            if (!ApplicationConstants.TryParseDetailsColumnWidths(raw, out _, out _, out _))
                return false;

            normalizedCsv = ApplicationConstants.NormalizeDetailsColumnWidths(raw);
            return true;
        }

        /// <summary>
        /// Persists Details data column widths if they differ from <paramref name="lastPersistedWidthCsv"/>.
        /// Updates the snapshot when save succeeds (avoids reading INI on each resize).
        /// </summary>
        public static void PersistDetailsColumnWidthsIfChanged(ListView listView, AppDataService appDataService, string viewMode, ref string lastPersistedWidthCsv)
        {
            if (listView == null || appDataService == null)
                return;
            if (viewMode != ApplicationConstants.ViewModeDetails)
                return;
            if (!TryFormatDetailsDataColumnWidthsCsv(listView, out var csv))
                return;
            if (lastPersistedWidthCsv != null && string.Equals(lastPersistedWidthCsv, csv, StringComparison.Ordinal))
                return;

            var result = appDataService.SetDetailsColumnWidths(csv);
            if (result.IsSuccess)
                lastPersistedWidthCsv = csv;
        }
    }
}

