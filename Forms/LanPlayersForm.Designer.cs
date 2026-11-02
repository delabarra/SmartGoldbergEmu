namespace SmartGoldbergEmu.Forms
{
    partial class LanPlayersForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnhookPresenceEvents();
                ClearPlayerRows();
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
            this.lblEmpty = new System.Windows.Forms.Label();
            this.pnlFooter = new System.Windows.Forms.Panel();
            this.btnOnlineToggle = new System.Windows.Forms.Button();
            this.stripLan = new System.Windows.Forms.StatusStrip();
            this.lblGamesFound = new System.Windows.Forms.ToolStripStatusLabel();
            this.tlpRoot.SuspendLayout();
            this.pnlLanPlayersHost.SuspendLayout();
            this.pnlFooter.SuspendLayout();
            this.stripLan.SuspendLayout();
            this.SuspendLayout();
            //
            // tlpRoot
            //
            this.tlpRoot.ColumnCount = 1;
            this.tlpRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpRoot.Controls.Add(this.pnlLanPlayersHost, 0, 0);
            this.tlpRoot.Controls.Add(this.pnlFooter, 0, 1);
            this.tlpRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpRoot.Location = new System.Drawing.Point(0, 0);
            this.tlpRoot.Name = "tlpRoot";
            this.tlpRoot.Padding = new System.Windows.Forms.Padding(0);
            this.tlpRoot.RowCount = 2;
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            this.tlpRoot.Size = new System.Drawing.Size(300, 538);
            this.tlpRoot.TabIndex = 0;
            //
            // pnlLanPlayersHost
            //
            this.pnlLanPlayersHost.Controls.Add(this.flpLanPlayers);
            this.pnlLanPlayersHost.Controls.Add(this.lblEmpty);
            this.pnlLanPlayersHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlLanPlayersHost.Location = new System.Drawing.Point(0, 0);
            this.pnlLanPlayersHost.Name = "pnlLanPlayersHost";
            this.pnlLanPlayersHost.Size = new System.Drawing.Size(300, 516);
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
            this.flpLanPlayers.Size = new System.Drawing.Size(300, 516);
            this.flpLanPlayers.TabIndex = 0;
            this.flpLanPlayers.WrapContents = false;
            //
            // lblEmpty
            //
            this.lblEmpty.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblEmpty.Location = new System.Drawing.Point(0, 0);
            this.lblEmpty.Name = "lblEmpty";
            this.lblEmpty.Padding = new System.Windows.Forms.Padding(16);
            this.lblEmpty.Size = new System.Drawing.Size(300, 516);
            this.lblEmpty.TabIndex = 1;
            this.lblEmpty.Text = "No other players on this LAN yet.";
            this.lblEmpty.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            this.lblEmpty.Visible = false;
            //
            // pnlFooter
            //
            this.pnlFooter.Controls.Add(this.btnOnlineToggle);
            this.pnlFooter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlFooter.Location = new System.Drawing.Point(0, 516);
            this.pnlFooter.Name = "pnlFooter";
            this.pnlFooter.Padding = new System.Windows.Forms.Padding(8, 8, 8, 8);
            this.pnlFooter.Size = new System.Drawing.Size(300, 44);
            this.pnlFooter.TabIndex = 1;
            //
            // btnOnlineToggle
            //
            this.btnOnlineToggle.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOnlineToggle.Location = new System.Drawing.Point(202, 9);
            this.btnOnlineToggle.Name = "btnOnlineToggle";
            this.btnOnlineToggle.Size = new System.Drawing.Size(90, 26);
            this.btnOnlineToggle.TabIndex = 0;
            this.btnOnlineToggle.Text = "Go Offline";
            this.btnOnlineToggle.UseVisualStyleBackColor = true;
            this.btnOnlineToggle.Click += new System.EventHandler(this.OnOnlineToggle_Click);
            //
            // stripLan
            //
            this.stripLan.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblGamesFound});
            this.stripLan.Location = new System.Drawing.Point(0, 538);
            this.stripLan.Name = "stripLan";
            this.stripLan.Size = new System.Drawing.Size(300, 22);
            this.stripLan.SizingGrip = false;
            this.stripLan.TabIndex = 1;
            //
            // lblGamesFound
            //
            this.lblGamesFound.Name = "lblGamesFound";
            this.lblGamesFound.Size = new System.Drawing.Size(285, 17);
            this.lblGamesFound.Spring = true;
            this.lblGamesFound.Text = "0 games found";
            this.lblGamesFound.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // LanPlayersForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(300, 560);
            this.Controls.Add(this.tlpRoot);
            this.Controls.Add(this.stripLan);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.MinimumSize = new System.Drawing.Size(260, 400);
            this.Name = "LanPlayersForm";
            this.ShowInTaskbar = true;
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "LAN Players";
            this.tlpRoot.ResumeLayout(false);
            this.pnlLanPlayersHost.ResumeLayout(false);
            this.pnlFooter.ResumeLayout(false);
            this.stripLan.ResumeLayout(false);
            this.stripLan.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tlpRoot;
        private System.Windows.Forms.Panel pnlFooter;
        private System.Windows.Forms.Button btnOnlineToggle;
        private System.Windows.Forms.Panel pnlLanPlayersHost;
        private System.Windows.Forms.FlowLayoutPanel flpLanPlayers;
        private System.Windows.Forms.Label lblEmpty;
        private System.Windows.Forms.StatusStrip stripLan;
        private System.Windows.Forms.ToolStripStatusLabel lblGamesFound;
    }
}
