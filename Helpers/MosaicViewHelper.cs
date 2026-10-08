using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SmartGoldbergEmu.Helpers
{
    public static class MosaicViewHelper
    {
        // Store Banner view cell and artwork.
        public const int TileViewImageWidth = 256;
        public const int TileViewImageHeight = 120;

        // Waiting mosaic spinner shadow (light mode): silhouette follows opaque pixels per frame.
        public const int WaitingMosaicShadowOffsetX = 2;
        public const int WaitingMosaicShadowOffsetY = 2;
        private const float WaitingMosaicShadowAlpha = 0.38f;

        // Waiting spinner only: narrower than the cell so FixWidthCropHeight keeps more of the square; centered,
        // so game art spacing is unaffected. Height leaves room for the light-mode shadow offset.
        public const int TileViewWaitingArtworkWidth = 200;
        public const int TileViewWaitingArtworkHeight = TileViewImageHeight - WaitingMosaicShadowOffsetY;

        // Library Cover view cell and artwork.
        public const int CompactTilesViewImageWidth = 171;
        public const int CompactTilesViewImageHeight = 256;

        public static Size TileViewImageSize => new Size(TileViewImageWidth, TileViewImageHeight);
        public static Size TileViewWaitingArtworkSize => new Size(TileViewWaitingArtworkWidth, TileViewWaitingArtworkHeight);
        public static Size CompactTilesViewImageSize => new Size(CompactTilesViewImageWidth, CompactTilesViewImageHeight);
        // Library Cover fills the ImageList cell.
        public static Size CompactTilesArtworkSize => CompactTilesViewImageSize;

        public const int LogoViewImageWidth = 200;
        public const int LogoViewImageHeight = 170;

        public static Size LogoViewImageSize => new Size(LogoViewImageWidth, LogoViewImageHeight);

        public const int TileViewPadding = 2;

        public const int CompactTilesViewPadding = 2;

        public const int LogoViewPadding = 4;

        public const int LogoViewShadowOffsetX = 2;

        public const int LogoViewShadowOffsetY = 2;

        private const float LogoViewShadowAlpha = 0.38f;

        // ListView.TileSize includes the ListView's internal padding on each side so the clickable area matches the image.
        public const int TileViewWidth = TileViewImageWidth + (TileViewPadding * 2);
        public const int TileViewHeight = TileViewImageHeight + (TileViewPadding * 2);

        public const int CompactTilesViewWidth = CompactTilesViewImageWidth + (CompactTilesViewPadding * 2);
        public const int CompactTilesViewHeight = CompactTilesViewImageHeight + (CompactTilesViewPadding * 2);

        public const int LogoViewWidth = LogoViewImageWidth + (LogoViewPadding * 2);
        public const int LogoViewHeight = LogoViewImageHeight + (LogoViewPadding * 2);

        // Display-only ImageList bitmap with a light drop shadow for pale backgrounds; on-disk logo.png is unchanged.
        public static Bitmap CreateLogoViewDisplayBitmap(Image source, bool dropShadow = true)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var targetSize = LogoViewImageSize;
            var destinationRect = GetContainDestinationRect(source.Size, targetSize);

            // Build the logo alone first so file colors are not SourceOver-blended onto the black shadow.
            var logoLayer = new Bitmap(targetSize.Width, targetSize.Height, PixelFormat.Format32bppArgb);
            try
            {
                using (var logoGraphics = Graphics.FromImage(logoLayer))
                {
                    ConfigureLogoDrawGraphics(logoGraphics);
                    logoGraphics.Clear(Color.Transparent);
                    logoGraphics.DrawImage(source, destinationRect);
                }

                var output = new Bitmap(targetSize.Width, targetSize.Height, PixelFormat.Format32bppArgb);
                using (var graphics = Graphics.FromImage(output))
                {
                    ConfigureLogoDrawGraphics(graphics);
                    graphics.Clear(Color.Transparent);

                    if (dropShadow)
                    {
                        var shadowRect = new Rectangle(
                            LogoViewShadowOffsetX,
                            LogoViewShadowOffsetY,
                            targetSize.Width,
                            targetSize.Height);

                        using (var shadowAttributes = CreateSilhouetteShadowImageAttributes(LogoViewShadowAlpha))
                        {
                            graphics.DrawImage(
                                logoLayer,
                                shadowRect,
                                0,
                                0,
                                logoLayer.Width,
                                logoLayer.Height,
                                GraphicsUnit.Pixel,
                                shadowAttributes);
                        }

                        // Logo pixels replace shadow (coverage AA must not pick up black underneath).
                        CopyCoveragePixelsOverShadow(output, logoLayer);
                    }
                    else
                    {
                        graphics.DrawImageUnscaled(logoLayer, 0, 0);
                    }
                }

                // Depth32Bit ImageList / AlphaBlend expects premultiplied alpha; straight alpha looks too dark.
                PremultiplyAlphaInPlace(output);
                return output;
            }
            finally
            {
                logoLayer.Dispose();
            }
        }

        // Cell-sized waiting art: silhouette shadow follows opaque pixels (fits animated APNG frames).
        public static Bitmap CompositeWaitingMosaicSilhouetteDropShadow(Bitmap content)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            var output = new Bitmap(content.Width, content.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(output))
            {
                ConfigureLogoDrawGraphics(graphics);
                graphics.Clear(Color.Transparent);

                var shadowRect = new Rectangle(
                    WaitingMosaicShadowOffsetX,
                    WaitingMosaicShadowOffsetY,
                    content.Width,
                    content.Height);

                using (var shadowAttributes = CreateSilhouetteShadowImageAttributes(WaitingMosaicShadowAlpha))
                {
                    graphics.DrawImage(
                        content,
                        shadowRect,
                        0,
                        0,
                        content.Width,
                        content.Height,
                        GraphicsUnit.Pixel,
                        shadowAttributes);
                }

                CopyCoveragePixelsOverShadow(output, content);
            }

            PremultiplyAlphaInPlace(output);
            return output;
        }

        private static void ConfigureLogoDrawGraphics(Graphics graphics)
        {
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            // Bicubic samples transparent black neighbors and darkens soft PNG edges; bilinear is safer for logos.
            graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        private static ImageAttributes CreateSilhouetteShadowImageAttributes(float shadowAlpha)
        {
            var matrix = new ColorMatrix(new[]
            {
                new float[] { 0, 0, 0, 0, 0 },
                new float[] { 0, 0, 0, 0, 0 },
                new float[] { 0, 0, 0, 0, 0 },
                new float[] { 0, 0, 0, shadowAlpha, 0 },
                new float[] { 0, 0, 0, 0, 1 }
            });

            var attributes = new ImageAttributes();
            attributes.SetColorMatrix(matrix);
            return attributes;
        }

        // Where content has coverage, keep the content pixel as-is so colors match the file.
        private static void CopyCoveragePixelsOverShadow(Bitmap destination, Bitmap contentLayer)
        {
            var bounds = new Rectangle(0, 0, destination.Width, destination.Height);
            var destData = destination.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            var contentData = contentLayer.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int height = bounds.Height;
                int width = bounds.Width;
                int destStride = destData.Stride;
                int contentStride = contentData.Stride;
                var destBuffer = new byte[destStride * height];
                var contentBuffer = new byte[contentStride * height];
                Marshal.Copy(destData.Scan0, destBuffer, 0, destBuffer.Length);
                Marshal.Copy(contentData.Scan0, contentBuffer, 0, contentBuffer.Length);

                for (int y = 0; y < height; y++)
                {
                    int destRow = y * destStride;
                    int contentRow = y * contentStride;
                    for (int x = 0; x < width; x++)
                    {
                        int contentIndex = contentRow + (x * 4);
                        byte alpha = contentBuffer[contentIndex + 3];
                        if (alpha == 0)
                            continue;

                        int destIndex = destRow + (x * 4);
                        destBuffer[destIndex] = contentBuffer[contentIndex];
                        destBuffer[destIndex + 1] = contentBuffer[contentIndex + 1];
                        destBuffer[destIndex + 2] = contentBuffer[contentIndex + 2];
                        destBuffer[destIndex + 3] = alpha;
                    }
                }

                Marshal.Copy(destBuffer, 0, destData.Scan0, destBuffer.Length);
            }
            finally
            {
                destination.UnlockBits(destData);
                contentLayer.UnlockBits(contentData);
            }
        }

        private static void PremultiplyAlphaInPlace(Bitmap bitmap)
        {
            var bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                int height = bounds.Height;
                int width = bounds.Width;
                int stride = data.Stride;
                var buffer = new byte[stride * height];
                Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

                for (int y = 0; y < height; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        int index = row + (x * 4);
                        byte alpha = buffer[index + 3];
                        if (alpha == 255)
                            continue;
                        if (alpha == 0)
                        {
                            buffer[index] = 0;
                            buffer[index + 1] = 0;
                            buffer[index + 2] = 0;
                            continue;
                        }

                        buffer[index] = (byte)(buffer[index] * alpha / 255);
                        buffer[index + 1] = (byte)(buffer[index + 1] * alpha / 255);
                        buffer[index + 2] = (byte)(buffer[index + 2] * alpha / 255);
                    }
                }

                Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private static Rectangle GetContainDestinationRect(Size sourceSize, Size targetSize)
        {
            if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
                return new Rectangle(0, 0, targetSize.Width, targetSize.Height);

            float scale = Math.Min(
                targetSize.Width / (float)sourceSize.Width,
                targetSize.Height / (float)sourceSize.Height);

            int drawWidth = Math.Max(1, (int)Math.Round(sourceSize.Width * scale));
            int drawHeight = Math.Max(1, (int)Math.Round(sourceSize.Height * scale));
            int x = (targetSize.Width - drawWidth) / 2;
            int y = (targetSize.Height - drawHeight) / 2;

            return new Rectangle(x, y, drawWidth, drawHeight);
        }
    }
}
