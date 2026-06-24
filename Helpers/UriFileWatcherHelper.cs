using System;
using System.IO;
using System.Windows.Forms;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Helpers
{
    /// <summary>
    /// Helper class for managing URI file watcher for inter-instance communication.
    /// </summary>
    public class UriFileWatcherHelper : IDisposable
    {
        private FileSystemWatcher _fileWatcher;
        private readonly Control _control; // For BeginInvoke
        private Action<ulong> _onUriProcessed;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the UriFileWatcherHelper.
        /// </summary>
        /// <param name="control">The control to use for thread marshalling (BeginInvoke).</param>
        /// <param name="onUriProcessed">Callback when a URI is processed successfully. Parameter is the AppId.</param>
        public UriFileWatcherHelper(Control control, Action<ulong> onUriProcessed)
        {
            _control = control ?? throw new ArgumentNullException(nameof(control));
            _onUriProcessed = onUriProcessed ?? throw new ArgumentNullException(nameof(onUriProcessed));
        }

        /// <summary>
        /// Sets up the file system watcher to monitor for URI protocol files.
        /// </summary>
        public void Setup()
        {
            if (_disposed)
                return;
            try
            {
                if (_fileWatcher != null)
                {
                    _fileWatcher.EnableRaisingEvents = false;
                    _fileWatcher.Created -= FileWatcher_Created;
                    _fileWatcher.Dispose();
                    _fileWatcher = null;
                }

                string handoffDir = PathConstants.LocalAppDataPerUserDirectory;
                Directory.CreateDirectory(handoffDir);

                _fileWatcher = new FileSystemWatcher(handoffDir)
                {
                    Filter = PathConstants.LauncherUriProtocolPendingFileSearchPattern,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };

                _fileWatcher.Created += FileWatcher_Created;

                CheckExistingUriFiles(handoffDir);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning($"Failed to setup URI file watcher: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks for existing URI files (in case files were created before watcher started).
        /// </summary>
        private void CheckExistingUriFiles(string handoffDir)
        {
            try
            {
                if (string.IsNullOrEmpty(handoffDir) || !Directory.Exists(handoffDir))
                    return;

                var uriFiles = Directory.GetFiles(handoffDir, PathConstants.LauncherUriProtocolPendingFileSearchPattern);
                foreach (var file in uriFiles)
                {
                    ProcessUriFile(file);
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning($"Error checking existing URI files: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles the creation of a new URI file from another instance.
        /// </summary>
        private void FileWatcher_Created(object sender, FileSystemEventArgs e)
        {
            if (_disposed)
                return;
            // Use BeginInvoke to process on UI thread
            if (_control != null && !_control.IsDisposed && !_control.Disposing)
            {
                string fullPath = e.FullPath;
                _control.BeginInvoke(new Action(() =>
                {
                    if (_disposed || _control == null || _control.IsDisposed || _control.Disposing)
                        return;
                    ProcessUriFile(fullPath);
                }));
            }
        }

        /// <summary>
        /// Processes a URI file by reading it and invoking the callback if valid.
        /// </summary>
        private void ProcessUriFile(string filePath)
        {
            try
            {
                if (_disposed || _control == null || _control.IsDisposed || _control.Disposing)
                    return;
                if (!File.Exists(filePath))
                    return;

                // Read the URI from the file
                string uri = File.ReadAllText(filePath, System.Text.Encoding.UTF8).Trim();

                // Delete the file after reading
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    Program.LogService?.LogWarning($"Failed to delete URI temp file: {ex.Message}");
                }

                // Parse and invoke callback
                var parseResult = UriProtocolService.ParseRunCommand(uri);
                if (parseResult.Success)
                {
                    Program.LogService?.LogMessage($"URI protocol launch from another instance: AppId {parseResult.AppId}");
                    if (!_disposed && _onUriProcessed != null && _control != null && !_control.IsDisposed && !_control.Disposing)
                        _onUriProcessed(parseResult.AppId);
                }
                else
                {
                    Program.LogService?.LogWarning($"Invalid URI from file: {uri} - {parseResult.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Error processing URI file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Disposes of the file watcher resources.
        /// </summary>
        public void Dispose()
        {
            _disposed = true;
            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Created -= FileWatcher_Created;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }
            _onUriProcessed = null;
        }
    }
}

