using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SmartGoldbergEmu;

namespace SmartGoldbergEmu.Generators
{
    /// <summary>
    /// Renders fallback library art from embedded <c>FallbackTileArt.csv</c> and a fixed vector glyph
    /// (SVG <c>d</c> path compatible with WPF <see cref="Geometry.Parse(string)"/>). Used for in-memory mosaic fallback
    /// mosaic tiles (single in-memory cache per active view in <c>GameImageService</c>).
    /// </summary>
    public static class TileGenerator
    {
        /// <summary>Multiplier applied after margin-aware fit (e.g. 0.85 = 15% smaller glyph).</summary>
        private const double GlyphDisplayScale = 0.75;

        /// <summary>Path <c>d</c> from the SmartGoldberg tile SVG (WPF path mini-language).</summary>
        private const string GlyphPathMarkup =
            "M127.779 0C60.42 0 5.24 52.412 0 119.014l68.724 28.674a35.812 35.812 0 0 1 20.426-6.366c.682 0 1.356.019 2.02.056l30.566-44.71v-.626c0-26.903 21.69-48.796 48.353-48.796 26.662 0 48.352 21.893 48.352 48.796 0 26.902-21.69 48.804-48.352 48.804-.37 0-.73-.009-1.098-.018l-43.593 31.377c.028.582.046 1.163.046 1.735 0 20.204-16.283 36.636-36.294 36.636-17.566 0-32.263-12.658-35.584-29.412L4.41 164.654c15.223 54.313 64.673 94.132 123.369 94.132 70.818 0 128.221-57.938 128.221-129.393C256 57.93 198.597 0 127.779 0zM80.352 196.332l-15.749-6.568c2.787 5.867 7.621 10.775 14.033 13.47 13.857 5.83 29.836-.803 35.612-14.799a27.555 27.555 0 0 0 .046-21.035c-2.768-6.79-7.999-12.086-14.706-14.909-6.67-2.795-13.811-2.694-20.085-.304l16.275 6.79c10.222 4.3 15.056 16.145 10.794 26.46-4.253 10.314-15.998 15.195-26.22 10.895zm121.957-100.29c0-17.925-14.457-32.52-32.217-32.52-17.769 0-32.226 14.595-32.226 32.52 0 17.926 14.457 32.512 32.226 32.512 17.76 0 32.217-14.586 32.217-32.512zm-56.37-.055c0-13.488 10.84-24.42 24.2-24.42 13.368 0 24.208 10.932 24.208 24.42 0 13.488-10.84 24.421-24.209 24.421-13.359 0-24.2-10.933-24.2-24.42z";

        /// <summary>
        /// Renders one asset from <c>FallbackTileArt.csv</c> (e.g. <c>header.jpg</c>, <c>cover.jpg</c>, <c>logo.png</c>).
        /// CSV supplies dimensions and margins; <paramref name="backgroundColor"/> and <paramref name="foregroundColor"/> come from the app theme.
        /// WPF rasterization runs on a dedicated STA thread.
        /// </summary>
        /// <returns>Bitmap for the row, or <c>null</c> if the file name is not in the CSV or rendering failed.</returns>
        public static Bitmap TryRenderAssetByFileName(string fileName, System.Drawing.Color backgroundColor, System.Drawing.Color foregroundColor)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            Bitmap result = null;
            ExceptionDispatchInfo captured = null;
            var trimmed = fileName.Trim();
            var thread = new Thread(() =>
            {
                try
                {
                    result = TryRenderAssetByFileNameCore(trimmed, backgroundColor, foregroundColor);
                }
                catch (Exception ex)
                {
                    captured = ExceptionDispatchInfo.Capture(ex);
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            thread.Join();
            captured?.Throw();
            return result;
        }

        private static Bitmap TryRenderAssetByFileNameCore(string fileName, System.Drawing.Color backgroundColor, System.Drawing.Color foregroundColor)
        {
            Geometry geometry;
            try
            {
                geometry = Geometry.Parse(GlyphPathMarkup);
                geometry.Freeze();
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError("Fallback tile glyph path could not be parsed.", ex);
                return null;
            }

            IReadOnlyList<FallbackTileArtRow> rows;
            try
            {
                rows = LoadSpecRows();
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError("Fallback tile CSV could not be loaded.", ex);
                return null;
            }

            foreach (var row in rows)
            {
                if (!string.Equals(row.FileName, fileName, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    return RenderRow(geometry, row, backgroundColor, foregroundColor);
                }
                catch (Exception ex)
                {
                    Program.LogService?.LogWarning($"Failed to render fallback tile asset '{row.FileName}': {ex.Message}");
                    return null;
                }
            }

            Program.LogService?.LogWarning($"Fallback tile CSV has no row for '{fileName}'.");
            return null;
        }

        private static IReadOnlyList<FallbackTileArtRow> LoadSpecRows()
        {
            var asm = typeof(TileGenerator).Assembly;
            var resourceName = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("FallbackTileArt.csv", StringComparison.OrdinalIgnoreCase));
            if (resourceName == null)
                throw new InvalidOperationException("Embedded resource FallbackTileArt.csv was not found.");

            using (var stream = asm.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException("Could not open embedded FallbackTileArt.csv stream.");

                using (var reader = new StreamReader(stream))
                {
                    var list = new List<FallbackTileArtRow>();
                    string line;
                    var isHeader = true;
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (line.Length == 0 || line[0] == '#')
                            continue;

                        var parts = line.Split(',');
                        if (parts.Length < 6)
                            continue;

                        if (isHeader)
                        {
                            isHeader = false;
                            continue;
                        }

                        list.Add(new FallbackTileArtRow(
                            parts[0].Trim(),
                            int.Parse(parts[1].Trim(), CultureInfo.InvariantCulture),
                            int.Parse(parts[2].Trim(), CultureInfo.InvariantCulture),
                            parts[3].Trim(),
                            parts[4].Trim(),
                            double.Parse(parts[5].Trim(), CultureInfo.InvariantCulture)));
                    }

                    if (list.Count == 0)
                        throw new InvalidOperationException("FallbackTileArt.csv contained no data rows.");

                    return list;
                }
            }
        }

        private static Bitmap RenderRow(Geometry geometry, FallbackTileArtRow row, System.Drawing.Color backgroundColor, System.Drawing.Color foregroundColor)
        {
            var backWpf = new SolidColorBrush(ToWpfColor(backgroundColor));
            var foreWpf = new SolidColorBrush(ToWpfColor(foregroundColor));
            backWpf.Freeze();
            foreWpf.Freeze();

            var bounds = geometry.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                throw new InvalidOperationException("Glyph geometry has empty bounds.");

            var margin = Math.Max(0, Math.Min(0.45, row.MarginFraction));
            var availW = row.Width * (1 - 2 * margin);
            var availH = row.Height * (1 - 2 * margin);
            var scale = Math.Min(availW / bounds.Width, availH / bounds.Height) * GlyphDisplayScale;
            var drawW = bounds.Width * scale;
            var drawH = bounds.Height * scale;
            var offsetX = (row.Width - drawW) / 2;
            var offsetY = (row.Height - drawH) / 2;

            var transform = new TransformGroup();
            transform.Children.Add(new TranslateTransform(-bounds.Left, -bounds.Top));
            transform.Children.Add(new ScaleTransform(scale, scale));
            transform.Children.Add(new TranslateTransform(offsetX, offsetY));
            transform.Freeze();

            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                // logo.png: transparent background for library logos view; JPEG assets keep solid CSV background.
                if (!string.Equals(Path.GetExtension(row.FileName), ".png", StringComparison.OrdinalIgnoreCase))
                    dc.DrawRectangle(backWpf, null, new System.Windows.Rect(0, 0, row.Width, row.Height));
                dc.PushTransform(transform);
                dc.DrawGeometry(foreWpf, null, geometry);
                dc.Pop();
            }

            var rtb = new RenderTargetBitmap(row.Width, row.Height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);

            return CopyPbgra32ToBitmap(rtb);
        }

        private static System.Windows.Media.Color ToWpfColor(System.Drawing.Color c)
        {
            return System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
        }

        private static Bitmap CopyPbgra32ToBitmap(RenderTargetBitmap rtb)
        {
            int w = rtb.PixelWidth;
            int h = rtb.PixelHeight;
            int stride = w * 4;
            var pixels = new byte[stride * h];
            rtb.CopyPixels(pixels, stride, 0);

            var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var bounds = new Rectangle(0, 0, w, h);
            var data = bmp.LockBits(bounds, ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < h; y++)
                {
                    System.Runtime.InteropServices.Marshal.Copy(
                        pixels,
                        y * stride,
                        System.IntPtr.Add(data.Scan0, y * data.Stride),
                        stride);
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            return bmp;
        }

        private readonly struct FallbackTileArtRow
        {
            public FallbackTileArtRow(string fileName, int width, int height, string backgroundHtml, string foregroundHtml, double marginFraction)
            {
                FileName = fileName;
                Width = width;
                Height = height;
                BackgroundHtml = backgroundHtml;
                ForegroundHtml = foregroundHtml;
                MarginFraction = marginFraction;
            }

            public string FileName { get; }

            public int Width { get; }

            public int Height { get; }

            public string BackgroundHtml { get; }

            public string ForegroundHtml { get; }

            public double MarginFraction { get; }
        }
    }
}
