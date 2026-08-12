using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SmartGoldbergEmu.Helpers
{
    // Shows muted placeholder when empty; theme foreground when the box has a real value.
    public class PlaceholderTextBoxHelper
    {
        private readonly Dictionary<TextBox, string> _placeholderTexts = new Dictionary<TextBox, string>();
        private readonly Func<Color> _getForegroundColor;
        private readonly Func<Color> _getPlaceholderColor;

        public PlaceholderTextBoxHelper(Func<Color> getForegroundColor, Func<Color> getPlaceholderColor = null)
        {
            _getForegroundColor = getForegroundColor ?? (() => SystemColors.ControlText);
            _getPlaceholderColor = getPlaceholderColor ?? (() => SystemColors.GrayText);
        }

        public void SetupPlaceholder(TextBox textBox, string placeholderText)
        {
            if (textBox == null || string.IsNullOrEmpty(placeholderText))
                return;

            _placeholderTexts[textBox] = placeholderText;

            if (string.IsNullOrEmpty(textBox.Text) || textBox.Text == placeholderText)
            {
                textBox.Text = placeholderText;
                textBox.ForeColor = _getPlaceholderColor();
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

        public string GetPlaceholderText(TextBox textBox)
        {
            if (textBox == null || !_placeholderTexts.ContainsKey(textBox))
                return null;
            return _placeholderTexts[textBox];
        }

        public void SetPlaceholderText(TextBox textBox, string placeholderText)
        {
            if (textBox == null || !_placeholderTexts.ContainsKey(textBox))
                return;
            _placeholderTexts[textBox] = placeholderText ?? string.Empty;
        }

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
                textBox.ForeColor = _getPlaceholderColor();
            }
        }

        public bool IsPlaceholderText(TextBox textBox)
        {
            if (textBox == null || !_placeholderTexts.ContainsKey(textBox))
                return false;
            return textBox.Text == _placeholderTexts[textBox];
        }

        public string GetActualText(TextBox textBox)
        {
            if (textBox == null)
                return string.Empty;
            if (IsPlaceholderText(textBox))
                return string.Empty;
            return textBox.Text;
        }

        public void SetTextBoxValue(TextBox textBox, string value)
        {
            if (textBox == null)
                return;

            if (string.IsNullOrEmpty(value))
            {
                if (_placeholderTexts.ContainsKey(textBox))
                {
                    textBox.Text = _placeholderTexts[textBox];
                    textBox.ForeColor = _getPlaceholderColor();
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

        public void UpdatePlaceholderColors()
        {
            var foreground = _getForegroundColor();
            var placeholder = _getPlaceholderColor();
            foreach (var kvp in _placeholderTexts)
            {
                var textBox = kvp.Key;
                textBox.ForeColor = textBox.Text == kvp.Value ? placeholder : foreground;
            }
        }

        private void TextBox_Enter(object sender, EventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null || !_placeholderTexts.ContainsKey(textBox))
                return;

            // Only clear when the control is actively showing placeholder style text.
            // This prevents clearing real user input that happens to match placeholder value.
            if (textBox.Text == _placeholderTexts[textBox] && textBox.ForeColor.ToArgb() == _getPlaceholderColor().ToArgb())
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
                textBox.ForeColor = _getPlaceholderColor();
            }
            else
            {
                textBox.ForeColor = _getForegroundColor();
            }
        }
    }
}
