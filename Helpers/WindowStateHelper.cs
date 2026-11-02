using System;
using System.Drawing;
using System.Windows.Forms;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Helpers
{
    public static class WindowStateHelper
    {
        public static void RestoreWindowState(Form form, AppDataService appDataService)
        {
            if (form == null)
                throw new ArgumentNullException(nameof(form));
            if (appDataService == null)
                throw new ArgumentNullException(nameof(appDataService));

            try
            {
                // First run with no saved layout: center once. Otherwise restore from ui_settings.ini (settings.ini fallback).
                if (appDataService.IsFirstRun() && !appDataService.HasPersistedWindowLayout())
                {
                    CenterFormOnScreen(form);
                    return;
                }

                var windowState = appDataService.GetWindowState();
                bool hasValidState = false;

                if (windowState != null)
                {
                    if (windowState.Size.Width > 0 && windowState.Size.Height > 0)
                    {
                        var size = windowState.Size;
                        size.Width = Math.Max(size.Width, form.MinimumSize.Width);
                        size.Height = Math.Max(size.Height, form.MinimumSize.Height);
                        form.Size = size;
                        hasValidState = true;
                    }

                    if (appDataService.HasPersistedWindowLocation() && IsLocationValid(windowState.Location))
                    {
                        form.StartPosition = FormStartPosition.Manual;
                        form.Location = windowState.Location;
                        hasValidState = true;
                    }
                    else if (windowState.Size.Width > 0 && windowState.Size.Height > 0)
                    {
                        CenterFormOnScreen(form);
                        hasValidState = true;
                    }

                    if (windowState.State == FormWindowState.Maximized ||
                        windowState.State == FormWindowState.Minimized ||
                        windowState.State == FormWindowState.Normal)
                    {
                        form.WindowState = windowState.State;
                        hasValidState = true;
                    }
                }

                if (!hasValidState)
                {
                    CenterFormOnScreen(form);
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Failed to restore window state: {ex.Message}");
                CenterFormOnScreen(form);
            }
        }

        public static void SaveWindowState(Form form, AppDataService appDataService)
        {
            if (form == null)
                throw new ArgumentNullException(nameof(form));
            if (appDataService == null)
                throw new ArgumentNullException(nameof(appDataService));

            try
            {
                if (form.WindowState == FormWindowState.Minimized)
                {
                    return;
                }

                var windowState = new WindowState
                {
                    Size = form.Size,
                    Location = form.Location,
                    State = form.WindowState
                };

                appDataService.SetWindowState(windowState);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Failed to save window state: {ex.Message}");
            }
        }

        // Restores LAN list layout from ui_settings.ini. Returns false when caller should use its default placement.
        public static bool TryRestoreLanListWindowState(Form form, AppDataService appDataService)
        {
            if (form == null)
                throw new ArgumentNullException(nameof(form));
            if (appDataService == null)
                throw new ArgumentNullException(nameof(appDataService));

            try
            {
                if (!appDataService.HasPersistedLanListWindowLayout())
                    return false;

                var windowState = appDataService.GetLanListWindowState();
                if (windowState == null)
                    return false;

                bool applied = false;

                if (windowState.Size.Width > 0 && windowState.Size.Height > 0)
                {
                    var size = windowState.Size;
                    size.Width = Math.Max(size.Width, form.MinimumSize.Width);
                    size.Height = Math.Max(size.Height, form.MinimumSize.Height);
                    form.Size = size;
                    applied = true;
                }

                if (appDataService.HasPersistedLanListWindowLocation() && IsLocationValid(windowState.Location))
                {
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = windowState.Location;
                    applied = true;
                }
                else if (applied)
                {
                    return false;
                }

                if (windowState.State == FormWindowState.Maximized ||
                    windowState.State == FormWindowState.Minimized ||
                    windowState.State == FormWindowState.Normal)
                {
                    form.WindowState = windowState.State;
                    applied = true;
                }

                return applied;
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Failed to restore LAN list window state: {ex.Message}");
                return false;
            }
        }

        public static void SaveLanListWindowState(Form form, AppDataService appDataService)
        {
            if (form == null)
                throw new ArgumentNullException(nameof(form));
            if (appDataService == null)
                throw new ArgumentNullException(nameof(appDataService));

            try
            {
                if (form.WindowState == FormWindowState.Minimized)
                    return;

                var windowState = new WindowState
                {
                    Size = form.Size,
                    Location = form.Location,
                    State = form.WindowState
                };

                appDataService.SetLanListWindowState(windowState);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Failed to save LAN list window state: {ex.Message}");
            }
        }

        public static void CenterFormOnScreen(Form form)
        {
            if (form == null)
                throw new ArgumentNullException(nameof(form));

            try
            {
                var screen = Screen.PrimaryScreen;
                var screenBounds = screen.WorkingArea;

                int x = screenBounds.X + (screenBounds.Width - form.Width) / 2;
                int y = screenBounds.Y + (screenBounds.Height - form.Height) / 2;

                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(x, y);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Failed to center form: {ex.Message}");
            }
        }

        public static bool IsLocationValid(Point location)
        {
            try
            {
                foreach (Screen screen in Screen.AllScreens)
                {
                    if (screen.WorkingArea.Contains(location))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }

    internal static class TabFormSizingHelper
    {
        internal const int DefaultContentPadding = 8;

        internal static int MeasureTabPageContentHeight(TabPage tabPage, int contentPadding = DefaultContentPadding)
        {
            if (tabPage == null)
                return 0;

            int contentBottom = tabPage.Padding.Top;
            foreach (Control child in tabPage.Controls)
            {
                if (!child.Visible)
                    continue;

                contentBottom = Math.Max(contentBottom, child.Bottom);
            }

            return contentBottom + tabPage.Padding.Bottom + contentPadding;
        }

        internal static int CalculateDesiredClientHeight(
            TabControl tabControl,
            TabPage selectedTab,
            int tabControlBottomAreaHeight,
            int minClientHeight,
            int contentPadding = DefaultContentPadding)
        {
            if (tabControl == null || selectedTab == null)
                return minClientHeight;

            int desiredTabPageHeight = MeasureTabPageContentHeight(selectedTab, contentPadding);
            int tabChromeHeight = Math.Max(0, tabControl.Height - tabControl.DisplayRectangle.Height);
            int desiredTabControlHeight = desiredTabPageHeight + tabChromeHeight;
            return Math.Max(minClientHeight, desiredTabControlHeight + tabControlBottomAreaHeight);
        }

        internal static bool TryApplyFormHeight(Form form, int desiredClientHeight, int minClientHeight)
        {
            if (form == null)
                return false;

            int nonClientHeight = form.Height - form.ClientSize.Height;
            int minFormHeight = minClientHeight + nonClientHeight;
            if (form.MinimumSize.Height < minFormHeight)
                form.MinimumSize = new Size(form.MinimumSize.Width, minFormHeight);

            int desiredFormHeight = desiredClientHeight + nonClientHeight;
            if (Math.Abs(form.Height - desiredFormHeight) <= 1)
                return false;

            form.Height = desiredFormHeight;
            return true;
        }
    }
}

