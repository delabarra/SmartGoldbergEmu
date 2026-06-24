using System;
using System.Drawing;
using System.Windows.Forms;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Helpers
{
    /// <summary>
    /// Helper class for managing form window state (size, position, window state).
    /// </summary>
    public static class WindowStateHelper
    {
        /// <summary>
        /// Restores the window state from saved settings.
        /// </summary>
        /// <param name="form">The form to restore state for.</param>
        /// <param name="appDataService">The app data service to retrieve saved state.</param>
        public static void RestoreWindowState(Form form, AppDataService appDataService)
        {
            if (form == null)
                throw new ArgumentNullException(nameof(form));
            if (appDataService == null)
                throw new ArgumentNullException(nameof(appDataService));

            try
            {
                // First run with no saved layout: center once. Otherwise restore from ui_settings.ini (or settings.ini fallback).
                if (appDataService.IsFirstRun() && !appDataService.HasPersistedWindowLayout())
                {
                    CenterFormOnScreen(form);
                    return;
                }

                var windowState = appDataService.GetWindowState();
                bool hasValidState = false;

                if (windowState != null)
                {
                    // Validate and restore size
                    if (windowState.Size.Width > 0 && windowState.Size.Height > 0)
                    {
                        // Ensure minimum size
                        var size = windowState.Size;
                        size.Width = Math.Max(size.Width, form.MinimumSize.Width);
                        size.Height = Math.Max(size.Height, form.MinimumSize.Height);
                        form.Size = size;
                        hasValidState = true;
                    }

                    // Validate and restore location; center when missing or off-screen
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

                    // Restore window state (Normal, Maximized, Minimized)
                    if (windowState.State == FormWindowState.Maximized ||
                        windowState.State == FormWindowState.Minimized ||
                        windowState.State == FormWindowState.Normal)
                    {
                        form.WindowState = windowState.State;
                        hasValidState = true;
                    }
                }

                // If no valid saved state, center the form
                if (!hasValidState)
                {
                    CenterFormOnScreen(form);
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Failed to restore window state: {ex.Message}");
                // Fallback: center the form
                CenterFormOnScreen(form);
            }
        }

        /// <summary>
        /// Saves the current window state to settings.
        /// </summary>
        /// <param name="form">The form to save state for.</param>
        /// <param name="appDataService">The app data service to save state to.</param>
        public static void SaveWindowState(Form form, AppDataService appDataService)
        {
            if (form == null)
                throw new ArgumentNullException(nameof(form));
            if (appDataService == null)
                throw new ArgumentNullException(nameof(appDataService));

            try
            {
                // Only save if window is not minimized (to avoid saving minimized state)
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

        /// <summary>
        /// Centers the form on the primary screen.
        /// </summary>
        /// <param name="form">The form to center.</param>
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

        /// <summary>
        /// Checks if a location is valid (within screen bounds).
        /// </summary>
        /// <param name="location">The location to validate.</param>
        /// <returns>True if the location is within any screen bounds.</returns>
        public static bool IsLocationValid(Point location)
        {
            try
            {
                // Check if location is within any screen bounds
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
}

