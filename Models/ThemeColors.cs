using System.Drawing;

namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// Represents a color scheme for a theme.
    /// </summary>
    public class ThemeColors
    {
        public Color Background { get; set; }
        public Color Foreground { get; set; }
        /// <summary>
        /// Background for text boxes, combo boxes, numeric inputs, and list boxes (classic light UI: window white on gray dialogs).
        /// </summary>
        public Color FieldBackground { get; set; }
        public Color ControlBackground { get; set; }
        public Color ControlForeground { get; set; }
        public Color MenuBackground { get; set; }
        public Color MenuForeground { get; set; }
        public Color StatusStripBackground { get; set; }
        public Color StatusStripForeground { get; set; }
        public Color StatusTextSecondary { get; set; }
        public Color StatusTextAccent { get; set; }
        public Color ListViewBackground { get; set; }
        public Color ListViewForeground { get; set; }
        public Color ListViewAlternate { get; set; }
        /// <summary>
        /// Background for owner-drawn ListView column headers (main game list, mods, achievements preview).
        /// </summary>
        public Color ListViewColumnHeaderBackground { get; set; }
        public Color Border { get; set; }
        public Color Highlight { get; set; }
        public Color HighlightText { get; set; }
        public Color LinkColor { get; set; }
        public Color ImageMarginBackground { get; set; }
        
        // Disabled control colors
        public Color DisabledBackground { get; set; }
        public Color DisabledForeground { get; set; }
        
        // Semantic colors (success, error, warning, info)
        public Color SuccessColor { get; set; }
        public Color ErrorColor { get; set; }
        public Color WarningColor { get; set; }
        public Color InfoColor { get; set; }
        public Color VisitedLinkColor { get; set; }
    }
}

