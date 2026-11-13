using System.Windows.Forms;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class ThemeServiceFieldChromeTests
    {
        [Fact]
        public void ApplyTheme_readonly_and_disabled_textboxes_use_gray_face()
        {
            using (var theme = new ThemeService())
            using (var form = new Form())
            {
                var editable = new TextBox { Parent = form, ReadOnly = false };
                var readOnly = new TextBox { Parent = form, ReadOnly = true };
                var disabled = new TextBox { Parent = form, Enabled = false };
                var combo = new ComboBox { Parent = form };
                theme.ApplyTheme(form);

                ThemeColors colors = theme.GetThemeColors(theme.EffectiveTheme);
                Assert.Equal(colors.FieldBackground, editable.BackColor);
                Assert.Equal(colors.FieldForeground, editable.ForeColor);
                Assert.Equal(colors.DisabledBackground, readOnly.BackColor);
                Assert.Equal(colors.FieldForeground, readOnly.ForeColor);
                Assert.Equal(colors.DisabledBackground, disabled.BackColor);
                Assert.Equal(colors.DisabledForeground, disabled.ForeColor);
                Assert.Equal(colors.FieldBackground, combo.BackColor);
                Assert.Equal(FlatStyle.Flat, combo.FlatStyle);
            }
        }

        [Fact]
        public void ApplyTheme_readonly_changed_at_runtime_updates_textbox_face()
        {
            using (var theme = new ThemeService())
            using (var form = new Form())
            {
                var box = new TextBox { Parent = form, ReadOnly = false };
                theme.ApplyTheme(form);
                ThemeColors colors = theme.GetThemeColors(theme.EffectiveTheme);
                Assert.Equal(colors.FieldBackground, box.BackColor);

                box.ReadOnly = true;
                Assert.Equal(colors.DisabledBackground, box.BackColor);
                Assert.Equal(colors.FieldForeground, box.ForeColor);

                box.ReadOnly = false;
                Assert.Equal(colors.FieldBackground, box.BackColor);
            }
        }
    }
}
