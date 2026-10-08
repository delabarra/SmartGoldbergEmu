using System.Drawing;

namespace SmartGoldbergEmu.Models
{
    public class ThemeColors
    {
        public Color Background { get; set; }
        public Color Foreground { get; set; }
        // Text boxes, combo boxes, numeric inputs, and list boxes (classic light UI: window white on gray dialogs).
        public Color FieldBackground { get; set; }
        // Text on FieldBackground (WinForms WindowText on Window).
        public Color FieldForeground { get; set; }
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
        // Owner-drawn ListView column headers (main game list, mods, achievements preview).
        public Color ListViewColumnHeaderBackground { get; set; }
        public Color Border { get; set; }
        public Color Highlight { get; set; }
        public Color HighlightText { get; set; }
        public Color LinkColor { get; set; }
        public Color ImageMarginBackground { get; set; }

        public Color DisabledBackground { get; set; }
        public Color DisabledForeground { get; set; }

        public Color SuccessColor { get; set; }
        public Color ErrorColor { get; set; }
        public Color WarningColor { get; set; }
        public Color InfoColor { get; set; }
        public Color VisitedLinkColor { get; set; }
    }
}

