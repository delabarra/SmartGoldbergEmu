using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Helpers
{
    // Theme borders/text via color table; dark-mode checks keep the stock glyph and only recolor it.
    internal class ThemedToolStripRenderer : ToolStripProfessionalRenderer
    {
        private readonly ThemeColors _colors;
        private readonly ThemeMode _themeMode;

        public ThemedToolStripRenderer(ThemeMode themeMode, ThemeColors colors) : base(new ThemedColorTable(themeMode, colors))
        {
            _themeMode = themeMode;
            _colors = colors;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.ToolStrip is StatusStrip)
                e.TextColor = _colors.StatusStripForeground;
            else
                e.TextColor = _colors.MenuForeground;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = _colors.MenuForeground;
            base.OnRenderArrow(e);
        }

        // Keep the stock check bitmap; tint RGB to HighlightText and preserve alpha (shape/AA).
        // Full invert was wrong when the OS glyph was already light (white became black).
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            if (_themeMode != ThemeMode.Dark)
            {
                base.OnRenderItemCheck(e);
                return;
            }

            Rectangle imageRect = e.ImageRectangle;
            Rectangle bounds = new Rectangle(imageRect.Left - 2, 1, imageRect.Width + 4, e.Item.Height - 2);

            Color fill = e.Item.Selected ? ColorTable.CheckSelectedBackground : ColorTable.CheckBackground;
            if (e.Item.Pressed)
                fill = ColorTable.CheckPressedBackground;

            using (var brush = new SolidBrush(fill))
                e.Graphics.FillRectangle(brush, bounds);

            using (var borderPen = new Pen(_colors.Border))
                e.Graphics.DrawRectangle(borderPen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);

            if (imageRect == Rectangle.Empty || e.Image == null)
                return;

            Image image = e.Image;
            bool disposeImage = false;
            if (!e.Item.Enabled)
            {
                image = CreateDisabledImage(e.Image);
                disposeImage = true;
            }

            Color tint = e.Item.Enabled ? _colors.HighlightText : _colors.DisabledForeground;
            try
            {
                using (ImageAttributes attrs = new ImageAttributes())
                {
                    attrs.SetColorMatrix(CreateAlphaTintMatrix(tint));
                    e.Graphics.DrawImage(
                        image,
                        imageRect,
                        0,
                        0,
                        image.Width,
                        image.Height,
                        GraphicsUnit.Pixel,
                        attrs);
                }
            }
            finally
            {
                if (disposeImage)
                    image.Dispose();
            }
        }

        // Drop source RGB; keep alpha; fill with the target color (same shape, new color only).
        private static ColorMatrix CreateAlphaTintMatrix(Color tint)
        {
            float r = tint.R / 255f;
            float g = tint.G / 255f;
            float b = tint.B / 255f;
            return new ColorMatrix(new float[][]
            {
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 1f, 0f },
                new float[] { r, g, b, 0f, 1f }
            });
        }
    }

    internal class ThemedColorTable : ProfessionalColorTable
    {
        private readonly ThemeMode _themeMode;
        private readonly ThemeColors _colors;

        public ThemedColorTable(ThemeMode themeMode, ThemeColors colors)
        {
            _themeMode = themeMode;
            _colors = colors;
        }

        public override Color MenuBorder => _colors.Border;
        public override Color MenuItemBorder => _colors.Border;
        public override Color MenuItemSelected => _colors.Highlight;
        public override Color MenuItemSelectedGradientBegin => _colors.Highlight;
        public override Color MenuItemSelectedGradientEnd => _colors.Highlight;
        public override Color MenuItemPressedGradientBegin => _colors.Highlight;
        public override Color MenuItemPressedGradientEnd => _colors.Highlight;
        public override Color MenuStripGradientBegin => _colors.MenuBackground;
        public override Color MenuStripGradientEnd => _colors.MenuBackground;
        public override Color ToolStripBorder => _colors.Border;
        public override Color ToolStripDropDownBackground => _colors.MenuBackground;
        public override Color ImageMarginGradientBegin => _colors.ImageMarginBackground;
        public override Color ImageMarginGradientMiddle => _colors.ImageMarginBackground;
        public override Color ImageMarginGradientEnd => _colors.ImageMarginBackground;
        public override Color ImageMarginRevealedGradientBegin => _colors.ImageMarginBackground;
        public override Color ImageMarginRevealedGradientMiddle => _colors.ImageMarginBackground;
        public override Color ImageMarginRevealedGradientEnd => _colors.ImageMarginBackground;
        public override Color SeparatorDark => GetSeparatorDarkColor();
        public override Color SeparatorLight => GetSeparatorLightColor();
        public override Color CheckBackground => _colors.Highlight;
        public override Color CheckSelectedBackground => _colors.Highlight;
        public override Color CheckPressedBackground => _colors.Highlight;
        public override Color StatusStripGradientBegin => _colors.StatusStripBackground;
        public override Color StatusStripGradientEnd => _colors.StatusStripBackground;

        private Color GetSeparatorDarkColor()
        {
            return _colors.Border;
        }

        private Color GetSeparatorLightColor()
        {
            if (_themeMode == ThemeMode.Dark)
                return _colors.Border;

            return Color.FromArgb(
                Math.Min(255, _colors.Border.R + 55),
                Math.Min(255, _colors.Border.G + 55),
                Math.Min(255, _colors.Border.B + 55));
        }
    }
}
