using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Forms
{
    public partial class RemovesGameForm : Form
    {
        private const int LayoutMargin = 16;
        private const int IconSize = 32;
        private const int MaxListItems = 5;
        private const int ContentWidth = 380;
        private const int Spacing = 8;
        private const int SectionBlankLines = 2;
        private const int GameListIndent = 40;
        private const int ButtonWidth = 75;
        private const int ButtonGap = 8;

        public bool DeleteFiles => chkDeleteFiles.Checked;

        public RemovesGameForm()
        {
            InitializeComponent();
        }

        public static (bool confirmed, bool deleteFiles) Show(IEnumerable<GameConfig> games, IWin32Window owner)
        {
            var list = games?.ToList() ?? new List<GameConfig>();
            if (list.Count == 0)
                return (false, false);

            using (var form = new RemovesGameForm(list))
            {
                var ok = form.ShowDialog(owner) == DialogResult.OK;
                return (ok, ok && form.DeleteFiles);
            }
        }

        private RemovesGameForm(List<GameConfig> games)
        {
            InitializeComponent();

            int count = games.Count;
            Text = count > 1 ? "Remove Games" : "Remove Game";

            lblMessage.Text = count == 1
                ? "Are you sure you want to remove the following game from your library?"
                : "Are you sure you want to remove the following games from your library?";

            var lines = games.Take(MaxListItems).Select(g => g.AppName).ToList();
            if (count > MaxListItems)
                lines.Add($"and {count - MaxListItems} more game(s)");
            lblGameList.Text = string.Join(Environment.NewLine, lines);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            int x = LayoutMargin + IconSize + Spacing;
            int y = LayoutMargin;
            int blankLine = (int)Math.Ceiling(lblMessage.Font.GetHeight());
            int sectionGap = blankLine * SectionBlankLines;
            picWarning.Location = new Point(LayoutMargin, LayoutMargin);

            lblMessage.Location = new Point(x, y);
            y += lblMessage.Height + sectionGap;

            lblGameList.Location = new Point(x + GameListIndent, y);
            y += lblGameList.Height + sectionGap;

            int formWidth = x + ContentWidth + LayoutMargin;
            int cancelLeft = formWidth - LayoutMargin - ButtonWidth;
            int removeLeft = cancelLeft - ButtonGap - ButtonWidth;
            int maxChkWidth = Math.Max(80, removeLeft - x - ButtonGap);

            chkDeleteFiles.MaximumSize = new Size(maxChkWidth, 0);
            Size chkSize = chkDeleteFiles.GetPreferredSize(new Size(maxChkWidth, 0));
            int rowHeight = Math.Max(btnRemove.Height, chkSize.Height);
            int rowTop = y;

            chkDeleteFiles.Location = new Point(x, rowTop + (rowHeight - chkSize.Height) / 2);
            btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            btnRemove.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            btnRemove.Location = new Point(removeLeft, rowTop + (rowHeight - btnRemove.Height) / 2);
            btnCancel.Location = new Point(cancelLeft, rowTop + (rowHeight - btnCancel.Height) / 2);

            ClientSize = new Size(formWidth, rowTop + rowHeight + LayoutMargin);
        }
    }
}
