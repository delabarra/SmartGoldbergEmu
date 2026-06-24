using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SmartGoldbergEmu.Helpers
{
    /// <summary>
    /// Helper for TextBox controls with placeholder text support.
    /// Shows gray placeholder when empty, theme foreground when has value.
    /// </summary>
    public class PlaceholderTextBoxHelper
    {
        private readonly Dictionary<TextBox, string> _placeholderTexts = new Dictionary<TextBox, string>();
        private readonly Func<Color> _getForegroundColor;

        /// <summary>
        /// Initializes a new instance with a function to get the theme foreground color.
        /// </summary>
        /// <param name="getForegroundColor">Returns the foreground color for actual text (e.g. from ThemeService).</param>
        public PlaceholderTextBoxHelper(Func<Color> getForegroundColor)
        {
            _getForegroundColor = getForegroundColor ?? (() => SystemColors.ControlText);
        }

        /// <summary>
        /// Sets up placeholder text for a TextBox control.
        /// </summary>
        public void SetupPlaceholder(TextBox textBox, string placeholderText)
        {
            if (textBox == null || string.IsNullOrEmpty(placeholderText))
                return;

            _placeholderTexts[textBox] = placeholderText;

            if (string.IsNullOrEmpty(textBox.Text) || textBox.Text == placeholderText)
            {
                textBox.Text = placeholderText;
                textBox.ForeColor = Color.Gray;
            }
            else
            {
                textBox.ForeColor = _getForegroundColor();
            }

            textBox.Enter -= TextBox_Enter;
            textBox.Leave -= TextBox_Leave;
            textBox.Enter += TextBox_Enter;
            textBox.Leave += TextBox_Leave;
        }

        /// <summary>
        /// Gets the placeholder text for a TextBox, or null if not set up.
        /// </summary>
        public string GetPlaceholderText(TextBox textBox)
        {
            if (textBox == null || !_placeholderTexts.ContainsKey(textBox))
                return null;
            return _placeholderTexts[textBox];
        }

        /// <summary>
        /// Updates the placeholder text for a TextBox (must already be set up).
        /// </summary>
        public void SetPlaceholderText(TextBox textBox, string placeholderText)
        {
            if (textBox == null || !_placeholderTexts.ContainsKey(textBox))
                return;
            _placeholderTexts[textBox] = placeholderText ?? string.Empty;
        }

        /// <summary>
        /// Updates placeholder text and display. If textbox is empty or showing placeholder, shows the new placeholder.
        /// </summary>
        public void UpdatePlaceholderAndDisplay(TextBox textBox, string newPlaceholderText)
        {
            if (textBox == null || !_placeholderTexts.ContainsKey(textBox))
                return;
            string priorPlaceholder = _placeholderTexts[textBox];
            _placeholderTexts[textBox] = newPlaceholderText ?? string.Empty;
            // Keep display in sync when the placeholder string changes but the box still shows the old hint;
            // otherwise focus/click (Enter) will not clear because Text no longer matches the dictionary.
            if (string.IsNullOrWhiteSpace(textBox.Text) || textBox.Text == priorPlaceholder ||
                textBox.Text == _placeholderTexts[textBox])
            {
                textBox.Text = _placeholderTexts[textBox];
                textBox.ForeColor = Color.Gray;
            }
        }

        /// <summary>
        /// Checks if a TextBox contains placeholder text.
        /// </summary>
        public bool IsPlaceholderText(TextBox textBox)
        {
            if (textBox == null || !_placeholderTexts.ContainsKey(textBox))
                return false;
            return textBox.Text == _placeholderTexts[textBox];
        }

        /// <summary>
        /// Gets the actual text value from a TextBox, ignoring placeholder text.
        /// </summary>
        public string GetActualText(TextBox textBox)
        {
            if (textBox == null)
                return string.Empty;
            if (IsPlaceholderText(textBox))
                return string.Empty;
            return textBox.Text;
        }

        /// <summary>
        /// Sets the value of a TextBox, showing placeholder if empty.
        /// </summary>
        public void SetTextBoxValue(TextBox textBox, string value)
        {
            if (textBox == null)
                return;

            if (string.IsNullOrEmpty(value))
            {
                if (_placeholderTexts.ContainsKey(textBox))
                {
                    textBox.Text = _placeholderTexts[textBox];
                    textBox.ForeColor = Color.Gray;
                }
                else
                {
                    textBox.Text = string.Empty;
                }
            }
            else
            {
                textBox.Text = value;
                textBox.ForeColor = _getForegroundColor();
            }
        }

        /// <summary>
        /// Updates placeholder text colors (e.g. when theme changes).
        /// </summary>
        public void UpdatePlaceholderColors()
        {
            var foreground = _getForegroundColor();
            foreach (var kvp in _placeholderTexts)
            {
                var textBox = kvp.Key;
                if (textBox.Text != kvp.Value)
                {
                    textBox.ForeColor = foreground;
                }
            }
        }

        private void TextBox_Enter(object sender, EventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null || !_placeholderTexts.ContainsKey(textBox))
                return;

            // Only clear when the control is actively showing placeholder style text.
            // This prevents clearing real user input that happens to match placeholder value.
            if (textBox.Text == _placeholderTexts[textBox] && textBox.ForeColor == Color.Gray)
            {
                textBox.Text = string.Empty;
                textBox.ForeColor = _getForegroundColor();
            }
        }

        private void TextBox_Leave(object sender, EventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null || !_placeholderTexts.ContainsKey(textBox))
                return;

            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Text = _placeholderTexts[textBox];
                textBox.ForeColor = Color.Gray;
            }
            else
            {
                textBox.ForeColor = _getForegroundColor();
            }
        }
    }
}
