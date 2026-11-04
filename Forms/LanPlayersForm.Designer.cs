namespace SmartGoldbergEmu.Forms
{
    partial class LanPlayersForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (flpLanPlayers != null)
                    flpLanPlayers.SizeChanged -= OnFlpLanPlayers_SizeChanged;
                StopAndDisposeLastSeenRefreshTimer();
                UnhookPresenceEvents();
                ClearPlayerRows();
                DisposeRowFontsAndPlaceholder();
                if (_playerMenu != null)
                    _playerMenu.Dispose();
                if (components != null)
                    components.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.tlpRoot = new System.Windows.Forms.TableLayoutPanel();
            this.pnlLanPlayersHost = new System.Windows.Forms.Panel();
            this.flpLanPlayers = new System.Windows.Forms.FlowLayoutPanel();
            this.stripLan = new System.Windows.Forms.StatusStrip();
            this.lblGamesFound = new System.Windows.Forms.ToolStripStatusLabel();
            this.tlpRoot.SuspendLayout();
            this.pnlLanPlayersHost.SuspendLayout();
            this.stripLan.SuspendLayout();
            this.SuspendLayout();
            //
            // tlpRoot
            //
            this.tlpRoot.ColumnCount = 1;
            this.tlpRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpRoot.Controls.Add(this.pnlLanPlayersHost, 0, 0);
            this.tlpRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpRoot.Location = new System.Drawing.Point(0, 0);
            this.tlpRoot.Name = "tlpRoot";
            this.tlpRoot.Padding = new System.Windows.Forms.Padding(0);
            this.tlpRoot.RowCount = 1;
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpRoot.Size = new System.Drawing.Size(300, 538);
            this.tlpRoot.TabIndex = 0;
            //
            // pnlLanPlayersHost
            //
            this.pnlLanPlayersHost.Controls.Add(this.flpLanPlayers);
            this.pnlLanPlayersHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlLanPlayersHost.Location = new System.Drawing.Point(0, 0);
            this.pnlLanPlayersHost.Name = "pnlLanPlayersHost";
            this.pnlLanPlayersHost.Size = new System.Drawing.Size(300, 538);
            this.pnlLanPlayersHost.TabIndex = 0;
            //
            // flpLanPlayers
            //
            this.flpLanPlayers.AutoScroll = true;
            this.flpLanPlayers.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flpLanPlayers.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.flpLanPlayers.Location = new System.Drawing.Point(0, 0);
            this.flpLanPlayers.Name = "flpLanPlayers";
            this.flpLanPlayers.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.flpLanPlayers.Size = new System.Drawing.Size(300, 538);
            this.flpLanPlayers.TabIndex = 0;
            this.flpLanPlayers.WrapContents = false;
            //
            // stripLan
            //
            this.stripLan.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.stripLan.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblGamesFound});
            this.stripLan.Location = new System.Drawing.Point(0, 538);
            this.stripLan.Name = "stripLan";
            this.stripLan.Size = new System.Drawing.Size(300, 22);
            this.stripLan.SizingGrip = true;
            this.stripLan.TabIndex = 1;
            //
            // lblGamesFound
            //
            this.lblGamesFound.Name = "lblGamesFound";
            this.lblGamesFound.Size = new System.Drawing.Size(285, 17);
            this.lblGamesFound.Spring = true;
            this.lblGamesFound.Text = "0 games · 0 players";
            this.lblGamesFound.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // LanPlayersForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(300, 560);
            this.Controls.Add(this.tlpRoot);
            this.Controls.Add(this.stripLan);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.MinimumSize = new System.Drawing.Size(260, 400);
            this.Name = "LanPlayersForm";
            this.ShowInTaskbar = true;
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "LAN Players";
            this.tlpRoot.ResumeLayout(false);
            this.pnlLanPlayersHost.ResumeLayout(false);
            this.stripLan.ResumeLayout(false);
            this.stripLan.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tlpRoot;
        private System.Windows.Forms.Panel pnlLanPlayersHost;
        private System.Windows.Forms.FlowLayoutPanel flpLanPlayers;
        private System.Windows.Forms.StatusStrip stripLan;
        private System.Windows.Forms.ToolStripStatusLabel lblGamesFound;
    }
}
