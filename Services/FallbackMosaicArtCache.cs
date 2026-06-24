using System;
using System.Drawing;
using System.Threading.Tasks;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Generators;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    public sealed class FallbackMosaicArtCache : IDisposable
    {
        private readonly object _sync = new object();
        private bool _disposed;
        private Bitmap _bitmap;
        private string _cachedFileName;
        private ThemeMode? _cachedEffectiveTheme;

        public async Task EnsureForViewModeAsync(string viewMode, ThemeMode effectiveTheme, Color background, Color foreground)
        {
            if (_disposed)
                return;

            var fileName = MapViewModeToFileName(viewMode);
            if (fileName == null)
                return;

            bool needRender;
            lock (_sync)
            {
                if (_disposed)
                    return;
                needRender = _bitmap == null
                    || !string.Equals(_cachedFileName, fileName, StringComparison.OrdinalIgnoreCase)
                    || !_cachedEffectiveTheme.HasValue
                    || _cachedEffectiveTheme.Value != effectiveTheme;
            }

            if (!needRender)
                return;

            var rendered = await Task.Run(() => TileGenerator.TryRenderAssetByFileName(fileName, background, foreground)).ConfigureAwait(false);

            if (_disposed)
            {
                rendered?.Dispose();
                return;
            }

            lock (_sync)
            {
                if (_disposed)
                {
                    rendered?.Dispose();
                    return;
                }

                if (_bitmap != null
                    && string.Equals(_cachedFileName, fileName, StringComparison.OrdinalIgnoreCase)
                    && _cachedEffectiveTheme.HasValue
                    && _cachedEffectiveTheme.Value == effectiveTheme)
                {
                    rendered?.Dispose();
                    return;
                }

                _bitmap?.Dispose();
                _bitmap = rendered;
                _cachedFileName = rendered != null ? fileName : null;
                _cachedEffectiveTheme = rendered != null ? effectiveTheme : (ThemeMode?)null;
            }
        }

        public Bitmap TryCloneForImageList()
        {
            lock (_sync)
            {
                if (_disposed || _bitmap == null)
                    return null;
                return new Bitmap(_bitmap);
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                    return;
                _disposed = true;
                _bitmap?.Dispose();
                _bitmap = null;
                _cachedFileName = null;
                _cachedEffectiveTheme = null;
            }
        }

        private static string MapViewModeToFileName(string viewMode)
        {
            if (viewMode == ApplicationConstants.ViewModeTile)
                return PathConstants.SteamGameResourcesHeaderImageFileName;
            if (viewMode == ApplicationConstants.ViewModeCompactTiles)
                return PathConstants.SteamGameResourcesCapsuleCoverImageFileName;
            if (viewMode == ApplicationConstants.ViewModeLogos)
                return PathConstants.SteamGameResourcesLibraryLogoImageFileName;
            return null;
        }
    }
}
