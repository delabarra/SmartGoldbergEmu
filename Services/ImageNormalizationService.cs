using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;

namespace SmartGoldbergEmu.Services
{
    public class ImageNormalizationService
    {
        private const int HeaderExtraCropHeightPixels = 2;

        private enum ResizeStrategy
        {
            FixWidthCropHeight,
            FixHeightCropWidth
        }

        // Single path for Store Banner / Library Cover / Logos mosaic ImageList bitmaps.
        public Bitmap CreateMosaicDisplayBitmap(Image source, string viewMode, bool logosDropShadow = false)
        {
            return CreateMosaicDisplayBitmap(source, viewMode, logosDropShadow, waitingPlaceholder: false, waitingDropShadow: false);
        }

        public Bitmap CreateMosaicDisplayBitmap(
            Image source,
            string viewMode,
            bool logosDropShadow,
            bool waitingPlaceholder,
            bool waitingDropShadow = false)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (viewMode == ApplicationConstants.ViewModeTile)
                return CreateStoreBannerDisplayBitmap(source, waitingPlaceholder, waitingPlaceholder && waitingDropShadow);
            if (viewMode == ApplicationConstants.ViewModeLogos)
                return MosaicViewHelper.CreateLogoViewDisplayBitmap(source, logosDropShadow);
            return CreateLibraryCoverDisplayBitmap(source, waitingPlaceholder && waitingDropShadow);
        }

        // Store Banner: width-fixed / height-cropped. Waiting art uses a narrower rect (more of the square visible).
        public Bitmap CreateStoreBannerDisplayBitmap(
            Image source,
            bool waitingPlaceholder = false,
            bool dropShadow = false)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            Size artworkSize = waitingPlaceholder
                ? MosaicViewHelper.TileViewWaitingArtworkSize
                : MosaicViewHelper.TileViewImageSize;

            return CreateCenteredArtworkBitmap(
                source,
                artworkSize,
                MosaicViewHelper.TileViewImageSize,
                ResizeStrategy.FixWidthCropHeight,
                dropShadow);
        }

        // Library Cover: height-fixed / width-cropped into the ImageList cell.
        public Bitmap CreateLibraryCoverDisplayBitmap(Image source, bool dropShadow = false)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return CreateCenteredArtworkBitmap(
                source,
                MosaicViewHelper.CompactTilesArtworkSize,
                MosaicViewHelper.CompactTilesViewImageSize,
                ResizeStrategy.FixHeightCropWidth,
                dropShadow);
        }

        private static Bitmap CreateCenteredArtworkBitmap(
            Image source,
            Size artworkSize,
            Size cellSize,
            ResizeStrategy resizeStrategy,
            bool dropShadow = false)
        {
            if (artworkSize.Width <= 0 || artworkSize.Height <= 0)
                throw new ArgumentOutOfRangeException(nameof(artworkSize));
            if (cellSize.Width <= 0 || cellSize.Height <= 0)
                throw new ArgumentOutOfRangeException(nameof(cellSize));

            var artwork = CreateBitmapWithResizeStrategy(source, artworkSize, resizeStrategy);
            Bitmap cellBitmap = null;
            try
            {
                if (artwork.Width == cellSize.Width && artwork.Height == cellSize.Height)
                {
                    cellBitmap = artwork;
                    artwork = null;
                }
                else
                {
                    cellBitmap = new Bitmap(cellSize.Width, cellSize.Height, PixelFormat.Format32bppArgb);
                    using (var graphics = Graphics.FromImage(cellBitmap))
                    {
                        graphics.Clear(Color.Transparent);
                        graphics.CompositingQuality = CompositingQuality.HighQuality;
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.SmoothingMode = SmoothingMode.HighQuality;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        int x = (cellSize.Width - artwork.Width) / 2;
                        int y = (cellSize.Height - artwork.Height) / 2;
                        // Keep silhouette shadow inside the cell (offset is bottom-right).
                        if (dropShadow)
                        {
                            x = Math.Max(0, Math.Min(x, cellSize.Width - artwork.Width - MosaicViewHelper.WaitingMosaicShadowOffsetX));
                            y = Math.Max(0, Math.Min(y, cellSize.Height - artwork.Height - MosaicViewHelper.WaitingMosaicShadowOffsetY));
                        }
                        graphics.DrawImageUnscaled(artwork, x, y);
                    }
                }

                if (!dropShadow)
                {
                    var result = cellBitmap;
                    cellBitmap = null;
                    return result;
                }

                Bitmap shadowed = MosaicViewHelper.CompositeWaitingMosaicSilhouetteDropShadow(cellBitmap);
                return shadowed;
            }
            finally
            {
                artwork?.Dispose();
                cellBitmap?.Dispose();
            }
        }

        private static Bitmap CreateBitmapWithResizeStrategy(Image source, Size targetSize, ResizeStrategy resizeStrategy)
        {
            var output = new Bitmap(targetSize.Width, targetSize.Height, PixelFormat.Format32bppArgb);

            using (var graphics = Graphics.FromImage(output))
            {
                graphics.Clear(Color.Transparent);
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                Rectangle destinationRect = resizeStrategy == ResizeStrategy.FixWidthCropHeight
                    ? GetFixedWidthDestinationRect(source.Size, targetSize, HeaderExtraCropHeightPixels)
                    : GetFixedHeightDestinationRect(source.Size, targetSize);

                graphics.DrawImage(source, destinationRect);
            }

            return output;
        }

        private static void EnsureCoverDrawFillsTarget(ref int drawWidth, ref int drawHeight, Size targetSize)
        {
            if (drawWidth < targetSize.Width)
                drawWidth = targetSize.Width;
            if (drawHeight < targetSize.Height)
                drawHeight = targetSize.Height;
        }

        private static Rectangle GetFixedWidthDestinationRect(Size sourceSize, Size targetSize, int extraHeightPixelsToCrop = 0)
        {
            float scale = targetSize.Width / (float)sourceSize.Width;
            int drawWidth = targetSize.Width;
            int drawHeight = Math.Max(1, (int)Math.Round(sourceSize.Height * scale));
            int minimumHeight = targetSize.Height + Math.Max(0, extraHeightPixelsToCrop);

            // If height is still too small, switch to cover scaling to avoid empty bars
            // and optionally force a tiny additional vertical crop.
            if (drawHeight < minimumHeight)
            {
                float coverScale = Math.Max(
                    targetSize.Width / (float)sourceSize.Width,
                    minimumHeight / (float)sourceSize.Height);
                drawWidth = Math.Max(1, (int)Math.Round(sourceSize.Width * coverScale));
                drawHeight = Math.Max(1, (int)Math.Round(sourceSize.Height * coverScale));
            }

            EnsureCoverDrawFillsTarget(ref drawWidth, ref drawHeight, targetSize);

            int x = (targetSize.Width - drawWidth) / 2;
            int y = (targetSize.Height - drawHeight) / 2;
            return new Rectangle(x, y, drawWidth, drawHeight);
        }

        private static Rectangle GetFixedHeightDestinationRect(Size sourceSize, Size targetSize)
        {
            float scale = targetSize.Height / (float)sourceSize.Height;
            int drawHeight = targetSize.Height;
            int drawWidth = Math.Max(1, (int)Math.Round(sourceSize.Width * scale));

            // If width is still too small, switch to cover scaling to avoid empty bars.
            if (drawWidth < targetSize.Width)
            {
                float coverScale = Math.Max(
                    targetSize.Width / (float)sourceSize.Width,
                    targetSize.Height / (float)sourceSize.Height);
                drawWidth = Math.Max(1, (int)Math.Round(sourceSize.Width * coverScale));
                drawHeight = Math.Max(1, (int)Math.Round(sourceSize.Height * coverScale));
            }

            EnsureCoverDrawFillsTarget(ref drawWidth, ref drawHeight, targetSize);

            int x = (targetSize.Width - drawWidth) / 2;
            int y = (targetSize.Height - drawHeight) / 2;
            return new Rectangle(x, y, drawWidth, drawHeight);
        }
    }
}
