using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using SmartGoldbergEmu;
using SmartGoldbergEmu.Helpers;

namespace SmartGoldbergEmu.Services
{
    public class ImageNormalizationService
    {
        private const int HeaderExtraCropHeightPixels = 2;
        private const long JpegOutputQuality = 95L;
        private static readonly Size HeaderMaxSize = new Size(460, 215);

        private enum ResizeStrategy
        {
            Contain,
            FixWidthCropHeight,
            FixHeightCropWidth
        }

        public bool EnsureLogoFileNormalizedForLogosView(string logoFilePath)
        {
            return EnsureImageFileNormalized(
                logoFilePath,
                MosaicViewHelper.LogoViewImageSize,
                trimTransparentPadding: true,
                imageKind: "logo",
                resizeStrategy: ResizeStrategy.Contain);
        }

        public bool NormalizeLogoFileForLogosView(string logoFilePath)
        {
            return NormalizeImageFile(
                logoFilePath,
                MosaicViewHelper.LogoViewImageSize,
                trimTransparentPadding: true,
                imageKind: "logo",
                resizeStrategy: ResizeStrategy.Contain);
        }

        public bool EnsureTileFileNormalizedForTileView(string tileFilePath)
        {
            return EnsureImageFileNormalized(
                tileFilePath,
                new Size(MosaicViewHelper.TileViewImageWidth, MosaicViewHelper.TileViewImageHeight),
                trimTransparentPadding: false,
                imageKind: "tile",
                resizeStrategy: ResizeStrategy.FixWidthCropHeight);
        }

        public bool EnsureCompactTileFileNormalizedForCompactTilesView(string compactTileFilePath)
        {
            return EnsureImageFileNormalized(
                compactTileFilePath,
                new Size(MosaicViewHelper.CompactTilesViewImageWidth, MosaicViewHelper.CompactTilesViewImageHeight),
                trimTransparentPadding: false,
                imageKind: "compact tile",
                resizeStrategy: ResizeStrategy.FixHeightCropWidth);
        }

        public Bitmap CreateCompactTileDisplayBitmapFromImage(Image source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return CreateBitmapWithResizeStrategy(
                source,
                new Size(MosaicViewHelper.CompactTilesViewImageWidth, MosaicViewHelper.CompactTilesViewImageHeight),
                ResizeStrategy.FixHeightCropWidth);
        }

        public Bitmap CreateFallbackLogoDisplayBitmapForLogosView(Image source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return CreateBitmapWithResizeStrategy(source, MosaicViewHelper.LogoViewImageSize, ResizeStrategy.FixHeightCropWidth);
        }

        public bool EnsureHeaderFileNormalizedForSteamBounds(string headerFilePath)
        {
            return EnsureImageFitsWithinMaxSize(headerFilePath, HeaderMaxSize, "header");
        }

        private bool EnsureImageFileNormalized(
            string imageFilePath,
            Size targetSize,
            bool trimTransparentPadding,
            string imageKind,
            ResizeStrategy resizeStrategy)
        {
            if (string.IsNullOrWhiteSpace(imageFilePath) || !File.Exists(imageFilePath))
                return false;

            if (HasExpectedDimensions(imageFilePath, targetSize, imageKind))
                return true;

            return NormalizeImageFile(imageFilePath, targetSize, trimTransparentPadding, imageKind, resizeStrategy);
        }

        private bool NormalizeImageFile(
            string imageFilePath,
            Size targetSize,
            bool trimTransparentPadding,
            string imageKind,
            ResizeStrategy resizeStrategy)
        {
            if (string.IsNullOrWhiteSpace(imageFilePath) || !File.Exists(imageFilePath))
                return false;

            try
            {
                using (var normalized = NormalizeByResizing(imageFilePath, targetSize, trimTransparentPadding, resizeStrategy))
                {
                    SaveBitmapAtomic(normalized, imageFilePath);
                }

                return true;
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning($"Failed to normalize {imageKind} image '{imageFilePath}': {ex.Message}");
                return false;
            }
        }

        private static Bitmap NormalizeByResizing(
            string imageFilePath,
            Size targetSize,
            bool trimTransparentPadding,
            ResizeStrategy resizeStrategy)
        {
            using (var source = LoadBitmapWithoutFileLock(imageFilePath))
            {
                if (trimTransparentPadding)
                {
                    using (var trimmed = TrimTransparentPadding(source))
                    {
                        return CreateBitmapWithResizeStrategy(trimmed, targetSize, resizeStrategy);
                    }
                }

                return CreateBitmapWithResizeStrategy(source, targetSize, resizeStrategy);
            }
        }

        private static bool HasExpectedDimensions(string imageFilePath, Size targetSize, string imageKind)
        {
            try
            {
                using (var image = LoadBitmapWithoutFileLock(imageFilePath))
                {
                    return image.Width == targetSize.Width
                        && image.Height == targetSize.Height;
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning($"Failed to inspect {imageKind} image '{imageFilePath}': {ex.Message}");
                return false;
            }
        }

        private bool EnsureImageFitsWithinMaxSize(string imageFilePath, Size maxSize, string imageKind)
        {
            if (string.IsNullOrWhiteSpace(imageFilePath) || !File.Exists(imageFilePath))
                return false;

            try
            {
                using (var source = LoadBitmapWithoutFileLock(imageFilePath))
                {
                    if (source.Width <= maxSize.Width && source.Height <= maxSize.Height)
                        return true;

                    var scale = Math.Min(
                        maxSize.Width / (float)source.Width,
                        maxSize.Height / (float)source.Height);

                    var targetWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
                    var targetHeight = Math.Max(1, (int)Math.Round(source.Height * scale));

                    using (var resized = CreateResizedBitmap(source, new Size(targetWidth, targetHeight)))
                    {
                        SaveBitmapAtomic(resized, imageFilePath);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning($"Failed to normalize {imageKind} image '{imageFilePath}': {ex.Message}");
                return false;
            }
        }

        private static Bitmap LoadBitmapWithoutFileLock(string imagePath)
        {
            using (var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var image = Image.FromStream(stream))
            {
                return new Bitmap(image);
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

                Rectangle destinationRect;
                switch (resizeStrategy)
                {
                    case ResizeStrategy.FixWidthCropHeight:
                        destinationRect = GetFixedWidthDestinationRect(source.Size, targetSize, HeaderExtraCropHeightPixels);
                        break;
                    case ResizeStrategy.FixHeightCropWidth:
                        destinationRect = GetFixedHeightDestinationRect(source.Size, targetSize);
                        break;
                    default:
                        destinationRect = GetContainDestinationRect(source.Size, targetSize);
                        break;
                }

                graphics.DrawImage(source, destinationRect);
            }

            return output;
        }

        private static Bitmap CreateResizedBitmap(Image source, Size targetSize)
        {
            var output = new Bitmap(targetSize.Width, targetSize.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(output))
            {
                graphics.Clear(Color.Transparent);
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(source, 0, 0, targetSize.Width, targetSize.Height);
            }

            return output;
        }

        private static Rectangle GetContainDestinationRect(Size sourceSize, Size targetSize)
        {
            float scale = Math.Min(
                targetSize.Width / (float)sourceSize.Width,
                targetSize.Height / (float)sourceSize.Height);

            int drawWidth = Math.Max(1, (int)Math.Round(sourceSize.Width * scale));
            int drawHeight = Math.Max(1, (int)Math.Round(sourceSize.Height * scale));
            int x = (targetSize.Width - drawWidth) / 2;
            int y = (targetSize.Height - drawHeight) / 2;
            return new Rectangle(x, y, drawWidth, drawHeight);
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

        private static Bitmap TrimTransparentPadding(Image source)
        {
            using (var sourceArgb = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(sourceArgb))
                {
                    graphics.DrawImage(source, 0, 0, source.Width, source.Height);
                }

                int minX = sourceArgb.Width;
                int minY = sourceArgb.Height;
                int maxX = -1;
                int maxY = -1;
                const byte alphaThreshold = 8;

                var bounds = new Rectangle(0, 0, sourceArgb.Width, sourceArgb.Height);
                var bitmapData = sourceArgb.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int stride = bitmapData.Stride;
                    int byteCount = stride * sourceArgb.Height;
                    var buffer = new byte[byteCount];
                    Marshal.Copy(bitmapData.Scan0, buffer, 0, byteCount);

                    for (int y = 0; y < sourceArgb.Height; y++)
                    {
                        int rowStart = y * stride;
                        for (int x = 0; x < sourceArgb.Width; x++)
                        {
                            int alphaIndex = rowStart + (x * 4) + 3;
                            if (buffer[alphaIndex] <= alphaThreshold)
                                continue;

                            if (x < minX) minX = x;
                            if (y < minY) minY = y;
                            if (x > maxX) maxX = x;
                            if (y > maxY) maxY = y;
                        }
                    }
                }
                finally
                {
                    sourceArgb.UnlockBits(bitmapData);
                }

                if (maxX < minX || maxY < minY)
                    return new Bitmap(source);

                if (minX == 0 && minY == 0 && maxX == sourceArgb.Width - 1 && maxY == sourceArgb.Height - 1)
                    return new Bitmap(source);

                var cropRect = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
                var trimmed = new Bitmap(cropRect.Width, cropRect.Height, PixelFormat.Format32bppArgb);
                using (var graphics = Graphics.FromImage(trimmed))
                {
                    graphics.DrawImage(sourceArgb, new Rectangle(0, 0, trimmed.Width, trimmed.Height), cropRect, GraphicsUnit.Pixel);
                }

                return trimmed;
            }
        }

        private static void SaveBitmapAtomic(Bitmap bitmap, string destinationPath)
        {
            string tempFilePath = destinationPath + ".tmp";
            var extension = Path.GetExtension(destinationPath);
            if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
            {
                bitmap.Save(tempFilePath, ImageFormat.Png);
            }
            else
            {
                SaveJpegWithQuality(bitmap, tempFilePath, JpegOutputQuality);
            }

            if (File.Exists(destinationPath))
                File.Delete(destinationPath);

            File.Move(tempFilePath, destinationPath);
        }

        private static void SaveJpegWithQuality(Bitmap bitmap, string path, long quality)
        {
            var jpegCodec = GetEncoder(ImageFormat.Jpeg);
            if (jpegCodec == null)
            {
                bitmap.Save(path, ImageFormat.Jpeg);
                return;
            }

            using (var encoderParameters = new EncoderParameters(1))
            {
                encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                bitmap.Save(path, jpegCodec, encoderParameters);
            }
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            var codecs = ImageCodecInfo.GetImageDecoders();
            foreach (var codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                    return codec;
            }

            return null;
        }
    }
}
