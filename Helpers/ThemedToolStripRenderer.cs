using System;
using System.Drawing;
using System.Windows.Forms;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Helpers
{
    /// <summary>
    /// Custom renderer for ToolStrip controls that applies theme colors including borders and check mark areas.
    /// </summary>
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
            // Use StatusStripForeground for StatusStrip items, MenuForeground for others
            if (e.ToolStrip is StatusStrip)
            {
                e.TextColor = _colors.StatusStripForeground;
            }
            else
            {
                e.TextColor = _colors.MenuForeground;
            }
            base.OnRenderItemText(e);
        }

        // Don't override OnRenderSeparator - let the base ToolStripProfessionalRenderer handle it
        // using SeparatorDark and SeparatorLight from ThemedColorTable

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = _colors.MenuForeground;
            base.OnRenderArrow(e);
        }
    }

    /// <summary>
    /// Custom color table for Professional renderer that uses theme colors.
    /// </summary>
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
        
        /// <summary>
        /// Gets the dark separator color based on theme - matches deprecated project.
        /// </summary>
        private Color GetSeparatorDarkColor()
        {
            if (_themeMode == ThemeMode.Dark)
            {
                // Dark theme: use border color (60,60,60) - matches deprecated
                return _colors.Border;
            }
            else
            {
                // Light theme: use border color (200,200,200) - matches deprecated SystemColors.ControlDark equivalent
                return _colors.Border;
            }
        }
        
        /// <summary>
        /// Gets the light separator color based on theme - matches deprecated project.
        /// </summary>
        private Color GetSeparatorLightColor()
        {
            if (_themeMode == ThemeMode.Dark)
            {
                // Dark theme: use border color (60,60,60) - matches deprecated
                return _colors.Border;
            }
            else
            {
                // Light theme: use a lighter shade - matches deprecated SystemColors.ControlLight equivalent
                return Color.FromArgb(
                    Math.Min(255, _colors.Border.R + 55),
                    Math.Min(255, _colors.Border.G + 55),
                    Math.Min(255, _colors.Border.B + 55));
            }
        }
    }
}

