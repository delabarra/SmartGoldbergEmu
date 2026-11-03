namespace SmartGoldbergEmu.Forms
{
    partial class LanChatForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnhookChatEvents();
                DisposeAvatarImage();
                if (components != null)
                    components.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.tlpRoot = new System.Windows.Forms.TableLayoutPanel();
            this.pnlProfile = new System.Windows.Forms.Panel();
            this.picAvatar = new System.Windows.Forms.PictureBox();
            this.lblName = new System.Windows.Forms.Label();
            this.lblSteamId = new System.Windows.Forms.Label();
            this.lblPersona = new System.Windows.Forms.Label();
            this.pnlGate = new System.Windows.Forms.Panel();
            this.lblGateWarning = new System.Windows.Forms.Label();
            this.flpGateButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.btnAccept = new System.Windows.Forms.Button();
            this.btnIgnore = new System.Windows.Forms.Button();
            this.btnIgnoreUser = new System.Windows.Forms.Button();
            this.rtfHistory = new System.Windows.Forms.RichTextBox();
            this.lblStatus = new System.Windows.Forms.Label();
            this.pnlComposer = new System.Windows.Forms.Panel();
            this.txtCompose = new System.Windows.Forms.TextBox();
            this.btnSend = new System.Windows.Forms.Button();
            this.tlpRoot.SuspendLayout();
            this.pnlProfile.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picAvatar)).BeginInit();
            this.pnlGate.SuspendLayout();
            this.flpGateButtons.SuspendLayout();
            this.pnlComposer.SuspendLayout();
            this.SuspendLayout();
            //
            // tlpRoot
            //
            this.tlpRoot.ColumnCount = 1;
            this.tlpRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpRoot.Controls.Add(this.pnlProfile, 0, 0);
            this.tlpRoot.Controls.Add(this.pnlGate, 0, 1);
            this.tlpRoot.Controls.Add(this.rtfHistory, 0, 2);
            this.tlpRoot.Controls.Add(this.lblStatus, 0, 3);
            this.tlpRoot.Controls.Add(this.pnlComposer, 0, 4);
            this.tlpRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpRoot.Location = new System.Drawing.Point(0, 0);
            this.tlpRoot.Name = "tlpRoot";
            this.tlpRoot.Padding = new System.Windows.Forms.Padding(8);
            this.tlpRoot.RowCount = 5;
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 22F));
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 88F));
            this.tlpRoot.Size = new System.Drawing.Size(460, 520);
            this.tlpRoot.TabIndex = 0;
            //
            // pnlProfile
            //
            this.pnlProfile.Controls.Add(this.picAvatar);
            this.pnlProfile.Controls.Add(this.lblName);
            this.pnlProfile.Controls.Add(this.lblPersona);
            this.pnlProfile.Controls.Add(this.lblSteamId);
            this.pnlProfile.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlProfile.Location = new System.Drawing.Point(11, 8);
            this.pnlProfile.Name = "pnlProfile";
            this.pnlProfile.Size = new System.Drawing.Size(438, 74);
            this.pnlProfile.TabIndex = 0;
            //
            // picAvatar
            //
            this.picAvatar.Location = new System.Drawing.Point(0, 5);
            this.picAvatar.Name = "picAvatar";
            this.picAvatar.Size = new System.Drawing.Size(64, 64);
            this.picAvatar.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.picAvatar.TabIndex = 0;
            this.picAvatar.TabStop = false;
            //
            // lblName
            //
            this.lblName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblName.AutoEllipsis = true;
            this.lblName.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.lblName.Location = new System.Drawing.Point(74, 8);
            this.lblName.Name = "lblName";
            this.lblName.Size = new System.Drawing.Size(354, 22);
            this.lblName.TabIndex = 1;
            this.lblName.Text = "Player";
            this.lblName.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // lblPersona
            //
            this.lblPersona.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblPersona.AutoEllipsis = true;
            this.lblPersona.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblPersona.Location = new System.Drawing.Point(74, 30);
            this.lblPersona.Name = "lblPersona";
            this.lblPersona.Size = new System.Drawing.Size(354, 16);
            this.lblPersona.TabIndex = 2;
            this.lblPersona.Text = "Online";
            //
            // lblSteamId
            //
            this.lblSteamId.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblSteamId.AutoEllipsis = true;
            this.lblSteamId.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblSteamId.Location = new System.Drawing.Point(74, 48);
            this.lblSteamId.Name = "lblSteamId";
            this.lblSteamId.Size = new System.Drawing.Size(354, 16);
            this.lblSteamId.TabIndex = 3;
            this.lblSteamId.Text = "Steam64";
            //
            // pnlGate
            //
            this.pnlGate.AutoSize = true;
            this.pnlGate.Controls.Add(this.lblGateWarning);
            this.pnlGate.Controls.Add(this.flpGateButtons);
            this.pnlGate.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlGate.Location = new System.Drawing.Point(11, 88);
            this.pnlGate.Name = "pnlGate";
            this.pnlGate.Padding = new System.Windows.Forms.Padding(0, 0, 0, 6);
            this.pnlGate.Size = new System.Drawing.Size(438, 70);
            this.pnlGate.TabIndex = 1;
            this.pnlGate.Visible = false;
            //
            // lblGateWarning
            //
            this.lblGateWarning.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblGateWarning.Location = new System.Drawing.Point(0, 0);
            this.lblGateWarning.Name = "lblGateWarning";
            this.lblGateWarning.Size = new System.Drawing.Size(438, 36);
            this.lblGateWarning.TabIndex = 0;
            this.lblGateWarning.Text = "Warning";
            //
            // flpGateButtons
            //
            this.flpGateButtons.AutoSize = true;
            this.flpGateButtons.Controls.Add(this.btnAccept);
            this.flpGateButtons.Controls.Add(this.btnIgnore);
            this.flpGateButtons.Controls.Add(this.btnIgnoreUser);
            this.flpGateButtons.Dock = System.Windows.Forms.DockStyle.Top;
            this.flpGateButtons.Location = new System.Drawing.Point(0, 36);
            this.flpGateButtons.Name = "flpGateButtons";
            this.flpGateButtons.Size = new System.Drawing.Size(438, 29);
            this.flpGateButtons.TabIndex = 1;
            //
            // btnAccept
            //
            this.btnAccept.AutoSize = true;
            this.btnAccept.Location = new System.Drawing.Point(3, 3);
            this.btnAccept.Name = "btnAccept";
            this.btnAccept.Size = new System.Drawing.Size(75, 23);
            this.btnAccept.TabIndex = 0;
            this.btnAccept.Text = "Accept";
            this.btnAccept.UseVisualStyleBackColor = true;
            this.btnAccept.Click += new System.EventHandler(this.OnAccept_Click);
            //
            // btnIgnore
            //
            this.btnIgnore.AutoSize = true;
            this.btnIgnore.Location = new System.Drawing.Point(84, 3);
            this.btnIgnore.Name = "btnIgnore";
            this.btnIgnore.Size = new System.Drawing.Size(75, 23);
            this.btnIgnore.TabIndex = 1;
            this.btnIgnore.Text = "Ignore";
            this.btnIgnore.UseVisualStyleBackColor = true;
            this.btnIgnore.Click += new System.EventHandler(this.OnIgnore_Click);
            //
            // btnIgnoreUser
            //
            this.btnIgnoreUser.AutoSize = true;
            this.btnIgnoreUser.Location = new System.Drawing.Point(165, 3);
            this.btnIgnoreUser.Name = "btnIgnoreUser";
            this.btnIgnoreUser.Size = new System.Drawing.Size(90, 23);
            this.btnIgnoreUser.TabIndex = 2;
            this.btnIgnoreUser.Text = "Ignore user";
            this.btnIgnoreUser.UseVisualStyleBackColor = true;
            this.btnIgnoreUser.Click += new System.EventHandler(this.OnIgnoreUser_Click);
            //
            // rtfHistory
            //
            this.rtfHistory.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.rtfHistory.DetectUrls = false;
            this.rtfHistory.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtfHistory.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.rtfHistory.Location = new System.Drawing.Point(11, 148);
            this.rtfHistory.Name = "rtfHistory";
            this.rtfHistory.ReadOnly = true;
            this.rtfHistory.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.rtfHistory.Size = new System.Drawing.Size(438, 254);
            this.rtfHistory.TabIndex = 2;
            this.rtfHistory.TabStop = false;
            //
            // lblStatus
            //
            this.lblStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblStatus.Location = new System.Drawing.Point(11, 405);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(438, 22);
            this.lblStatus.TabIndex = 3;
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // pnlComposer
            //
            this.pnlComposer.Controls.Add(this.txtCompose);
            this.pnlComposer.Controls.Add(this.btnSend);
            this.pnlComposer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlComposer.Location = new System.Drawing.Point(11, 430);
            this.pnlComposer.Name = "pnlComposer";
            this.pnlComposer.Size = new System.Drawing.Size(438, 82);
            this.pnlComposer.TabIndex = 4;
            //
            // txtCompose
            //
            this.txtCompose.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtCompose.Location = new System.Drawing.Point(0, 0);
            this.txtCompose.MaxLength = 500;
            this.txtCompose.Multiline = true;
            this.txtCompose.Name = "txtCompose";
            this.txtCompose.ScrollBars = System.Windows.Forms.ScrollBars.None;
            this.txtCompose.Size = new System.Drawing.Size(350, 82);
            this.txtCompose.TabIndex = 0;
            this.txtCompose.KeyDown += new System.Windows.Forms.KeyEventHandler(this.OnCompose_KeyDown);
            //
            // btnSend
            //
            this.btnSend.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSend.Location = new System.Drawing.Point(356, 0);
            this.btnSend.Name = "btnSend";
            this.btnSend.Size = new System.Drawing.Size(82, 28);
            this.btnSend.TabIndex = 1;
            this.btnSend.Text = "Send";
            this.btnSend.UseVisualStyleBackColor = true;
            this.btnSend.Click += new System.EventHandler(this.OnSend_Click);
            //
            // LanChatForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(460, 520);
            this.Controls.Add(this.tlpRoot);
            this.MinimumSize = new System.Drawing.Size(400, 400);
            this.Name = "LanChatForm";
            this.ShowInTaskbar = true;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "LAN Chat";
            this.tlpRoot.ResumeLayout(false);
            this.tlpRoot.PerformLayout();
            this.pnlProfile.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.picAvatar)).EndInit();
            this.pnlGate.ResumeLayout(false);
            this.pnlGate.PerformLayout();
            this.flpGateButtons.ResumeLayout(false);
            this.flpGateButtons.PerformLayout();
            this.pnlComposer.ResumeLayout(false);
            this.pnlComposer.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tlpRoot;
        private System.Windows.Forms.Panel pnlProfile;
        private System.Windows.Forms.PictureBox picAvatar;
        private System.Windows.Forms.Label lblName;
        private System.Windows.Forms.Label lblSteamId;
        private System.Windows.Forms.Label lblPersona;
        private System.Windows.Forms.Panel pnlGate;
        private System.Windows.Forms.Label lblGateWarning;
        private System.Windows.Forms.FlowLayoutPanel flpGateButtons;
        private System.Windows.Forms.Button btnAccept;
        private System.Windows.Forms.Button btnIgnore;
        private System.Windows.Forms.Button btnIgnoreUser;
        private System.Windows.Forms.RichTextBox rtfHistory;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Panel pnlComposer;
        private System.Windows.Forms.TextBox txtCompose;
        private System.Windows.Forms.Button btnSend;
    }
}
