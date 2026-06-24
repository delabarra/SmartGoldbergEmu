using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace SmartGoldbergEmu.Helpers
{
    /// <summary>
    /// ImageList tile sizing and display bitmap helpers for the main game list views.
    /// </summary>
    public static class MosaicViewHelper
    {
        /// <summary>
        /// Tile view image dimensions: 256×120 pixels (actual image size from header.jpg).
        /// This is what ImageList.ImageSize should be set to.
        /// </summary>
        public const int TileViewImageWidth = 256;
        public const int TileViewImageHeight = 120;

        /// <summary>
        /// Compact tiles view display dimensions: 171x256 pixels.
        /// Source image is cover.jpg (2:3 ratio), scaled down for ImageList compatibility.
        /// </summary>
        public const int CompactTilesViewImageWidth = 171;
        public const int CompactTilesViewImageHeight = 256;

        /// <summary>
        /// Logos view image dimensions: 200×170 pixels.
        /// This is what ImageList.ImageSize should be set to and the on-disk logo.png target after normalization.
        /// </summary>
        public const int LogoViewImageWidth = 200;
        public const int LogoViewImageHeight = 170;

        /// <summary>
        /// Logo mosaic cell image size (same as <see cref="LogoViewImageWidth"/> × <see cref="LogoViewImageHeight"/>).
        /// </summary>
        public static Size LogoViewImageSize => new Size(LogoViewImageWidth, LogoViewImageHeight);

        /// <summary>
        /// ListView internal padding for Tile view.
        /// </summary>
        public const int TileViewPadding = 2;

        /// <summary>
        /// ListView internal padding for Library Cover (portrait capsule) view.
        /// </summary>
        public const int CompactTilesViewPadding = 2;

        /// <summary>
        /// ListView internal padding for Logos view.
        /// </summary>
        public const int LogoViewPadding = 4;

        /// <summary>
        /// Subtle drop-shadow offset for logos view ImageList tiles (display only).
        /// </summary>
        public const int LogoViewShadowOffsetX = 2;

        /// <summary>
        /// Subtle drop-shadow offset for logos view ImageList tiles (display only).
        /// </summary>
        public const int LogoViewShadowOffsetY = 2;

        private const float LogoViewShadowAlpha = 0.38f;

        /// <summary>
        /// Tile view total dimensions for ListView.TileSize.
        /// Includes ListView's internal padding (2px on each side = 4px total per dimension).
        /// This ensures the clickable area matches the image display area.
        /// ImageList size = 256x120, TileSize = 260x124 (image + 4px padding)
        /// </summary>
        public const int TileViewWidth = TileViewImageWidth + (TileViewPadding * 2);   // 256 + 4 = 260
        public const int TileViewHeight = TileViewImageHeight + (TileViewPadding * 2); // 120 + 4 = 124

        /// <summary>
        /// Compact tiles view total dimensions for ListView.TileSize.
        /// Includes ListView's internal padding (2px on each side = 4px total per dimension).
        /// This ensures the clickable area matches the image display area.
        /// ImageList size = 171x256, TileSize = 175x260 (image + 4px padding)
        /// </summary>
        public const int CompactTilesViewWidth = CompactTilesViewImageWidth + (CompactTilesViewPadding * 2);   // 171 + 4 = 175
        public const int CompactTilesViewHeight = CompactTilesViewImageHeight + (CompactTilesViewPadding * 2); // 256 + 4 = 260

        /// <summary>
        /// Logos view total dimensions for ListView.TileSize.
        /// Includes ListView's internal padding (4px on each side = 8px total per dimension).
        /// ImageList = 200×170, TileSize = 208×178 (image + padding).
        /// </summary>
        public const int LogoViewWidth = LogoViewImageWidth + (LogoViewPadding * 2);   // 200 + 8 = 208
        public const int LogoViewHeight = LogoViewImageHeight + (LogoViewPadding * 2); // 170 + 8 = 178

        /// <summary>
        /// Scales an image proportionally into the tile view cell (256×120), same as the main list ImageList path.
        /// </summary>
        public static Bitmap CreateTileViewDisplayBitmap(Image source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var targetSize = new Size(TileViewImageWidth, TileViewImageHeight);
            var output = new Bitmap(targetSize.Width, targetSize.Height);

            using (var graphics = Graphics.FromImage(output))
            {
                graphics.Clear(Color.Transparent);
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                var destinationRect = GetContainDestinationRectForTileList(source.Size, targetSize);
                graphics.DrawImage(source, destinationRect);
            }

            return output;
        }

        /// <summary>
        /// Composites a logos-view cell with a light drop shadow so logos read clearly on pale ListView backgrounds.
        /// On-disk <c>logo.png</c> files are unchanged; this is only for the ImageList bitmap.
        /// </summary>
        public static Bitmap CreateLogoViewDisplayBitmap(Image source, bool dropShadow = true)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var targetSize = LogoViewImageSize;
            var output = new Bitmap(targetSize.Width, targetSize.Height, PixelFormat.Format32bppArgb);
            var imageRect = new Rectangle(0, 0, targetSize.Width, targetSize.Height);

            using (var graphics = Graphics.FromImage(output))
            {
                graphics.Clear(Color.Transparent);
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                if (dropShadow)
                {
                    var shadowRect = new Rectangle(
                        LogoViewShadowOffsetX,
                        LogoViewShadowOffsetY,
                        targetSize.Width,
                        targetSize.Height);

                    using (var shadowAttributes = CreateLogoViewShadowImageAttributes())
                    {
                        graphics.DrawImage(
                            source,
                            shadowRect,
                            0,
                            0,
                            source.Width,
                            source.Height,
                            GraphicsUnit.Pixel,
                            shadowAttributes);
                    }
                }

                graphics.DrawImage(source, imageRect);
            }

            return output;
        }

        private static ImageAttributes CreateLogoViewShadowImageAttributes()
        {
            var matrix = new ColorMatrix(new[]
            {
                new float[] { 0, 0, 0, 0, 0 },
                new float[] { 0, 0, 0, 0, 0 },
                new float[] { 0, 0, 0, 0, 0 },
                new float[] { 0, 0, 0, LogoViewShadowAlpha, 0 },
                new float[] { 0, 0, 0, 0, 1 }
            });

            var attributes = new ImageAttributes();
            attributes.SetColorMatrix(matrix);
            return attributes;
        }

        private static Rectangle GetContainDestinationRectForTileList(Size sourceSize, Size targetSize)
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
