namespace SmartGoldbergEmu.Forms
{
    partial class SteamlessOptionsForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblExecutable = new System.Windows.Forms.Label();
            this.grpOptions = new System.Windows.Forms.GroupBox();
            this.tlpOptions = new System.Windows.Forms.TableLayoutPanel();
            this.chkKeepBind = new System.Windows.Forms.CheckBox();
            this.chkKeepStub = new System.Windows.Forms.CheckBox();
            this.chkDumpPayload = new System.Windows.Forms.CheckBox();
            this.chkDumpDrmp = new System.Windows.Forms.CheckBox();
            this.chkRealign = new System.Windows.Forms.CheckBox();
            this.chkRecalcChecksum = new System.Windows.Forms.CheckBox();
            this.chkExperimental = new System.Windows.Forms.CheckBox();
            this.btnApply = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.grpOptions.SuspendLayout();
            this.tlpOptions.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblTitle
            // 
            this.lblTitle.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblTitle.AutoEllipsis = true;
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Location = new System.Drawing.Point(12, 12);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(310, 22);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "Steamless";
            this.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblExecutable
            // 
            this.lblExecutable.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblExecutable.AutoEllipsis = true;
            this.lblExecutable.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this.lblExecutable.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblExecutable.Location = new System.Drawing.Point(12, 38);
            this.lblExecutable.Name = "lblExecutable";
            this.lblExecutable.Size = new System.Drawing.Size(310, 16);
            this.lblExecutable.TabIndex = 1;
            this.lblExecutable.Text = "Executable:";
            // 
            // grpOptions
            // 
            this.grpOptions.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpOptions.Controls.Add(this.tlpOptions);
            this.grpOptions.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.grpOptions.Location = new System.Drawing.Point(12, 57);
            this.grpOptions.Name = "grpOptions";
            this.grpOptions.Padding = new System.Windows.Forms.Padding(10, 6, 10, 10);
            this.grpOptions.Size = new System.Drawing.Size(310, 176);
            this.grpOptions.TabIndex = 2;
            this.grpOptions.TabStop = false;
            this.grpOptions.Text = "Options";
            // 
            // tlpOptions
            // 
            this.tlpOptions.ColumnCount = 1;
            this.tlpOptions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpOptions.Controls.Add(this.chkKeepBind, 0, 0);
            this.tlpOptions.Controls.Add(this.chkKeepStub, 0, 1);
            this.tlpOptions.Controls.Add(this.chkDumpPayload, 0, 2);
            this.tlpOptions.Controls.Add(this.chkDumpDrmp, 0, 3);
            this.tlpOptions.Controls.Add(this.chkRealign, 0, 4);
            this.tlpOptions.Controls.Add(this.chkRecalcChecksum, 0, 5);
            this.tlpOptions.Controls.Add(this.chkExperimental, 0, 6);
            this.tlpOptions.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpOptions.Location = new System.Drawing.Point(10, 22);
            this.tlpOptions.Margin = new System.Windows.Forms.Padding(0);
            this.tlpOptions.Name = "tlpOptions";
            this.tlpOptions.Padding = new System.Windows.Forms.Padding(4, 2, 4, 4);
            this.tlpOptions.RowCount = 7;
            this.tlpOptions.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpOptions.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpOptions.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpOptions.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpOptions.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpOptions.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpOptions.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpOptions.Size = new System.Drawing.Size(290, 144);
            this.tlpOptions.TabIndex = 0;
            // 
            // chkKeepBind
            // 
            this.chkKeepBind.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.chkKeepBind.AutoSize = true;
            this.chkKeepBind.Location = new System.Drawing.Point(7, 5);
            this.chkKeepBind.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
            this.chkKeepBind.Name = "chkKeepBind";
            this.chkKeepBind.Size = new System.Drawing.Size(276, 19);
            this.chkKeepBind.TabIndex = 0;
            this.chkKeepBind.Text = "Keep the .bind section in the unpacked file";
            this.chkKeepBind.UseVisualStyleBackColor = true;
            // 
            // chkKeepStub
            // 
            this.chkKeepStub.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.chkKeepStub.AutoSize = true;
            this.chkKeepStub.Location = new System.Drawing.Point(7, 24);
            this.chkKeepStub.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
            this.chkKeepStub.Name = "chkKeepStub";
            this.chkKeepStub.Size = new System.Drawing.Size(276, 19);
            this.chkKeepStub.TabIndex = 1;
            this.chkKeepStub.Text = "Keep the DOS stub in the unpacked file";
            this.chkKeepStub.UseVisualStyleBackColor = true;
            // 
            // chkDumpPayload
            // 
            this.chkDumpPayload.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.chkDumpPayload.AutoSize = true;
            this.chkDumpPayload.Location = new System.Drawing.Point(7, 43);
            this.chkDumpPayload.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
            this.chkDumpPayload.Name = "chkDumpPayload";
            this.chkDumpPayload.Size = new System.Drawing.Size(276, 19);
            this.chkDumpPayload.TabIndex = 2;
            this.chkDumpPayload.Text = "Dump the stub payload to disk";
            this.chkDumpPayload.UseVisualStyleBackColor = true;
            // 
            // chkDumpDrmp
            // 
            this.chkDumpDrmp.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.chkDumpDrmp.AutoSize = true;
            this.chkDumpDrmp.Location = new System.Drawing.Point(7, 62);
            this.chkDumpDrmp.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
            this.chkDumpDrmp.Name = "chkDumpDrmp";
            this.chkDumpDrmp.Size = new System.Drawing.Size(276, 19);
            this.chkDumpDrmp.TabIndex = 3;
            this.chkDumpDrmp.Text = "Dump SteamDRMP.dll to disk";
            this.chkDumpDrmp.UseVisualStyleBackColor = true;
            // 
            // chkRealign
            // 
            this.chkRealign.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.chkRealign.AutoSize = true;
            this.chkRealign.Checked = true;
            this.chkRealign.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkRealign.Location = new System.Drawing.Point(7, 81);
            this.chkRealign.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
            this.chkRealign.Name = "chkRealign";
            this.chkRealign.Size = new System.Drawing.Size(276, 19);
            this.chkRealign.TabIndex = 4;
            this.chkRealign.Text = "Realign PE sections in the unpacked file";
            this.chkRealign.UseVisualStyleBackColor = true;
            // 
            // chkRecalcChecksum
            // 
            this.chkRecalcChecksum.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.chkRecalcChecksum.AutoSize = true;
            this.chkRecalcChecksum.Checked = true;
            this.chkRecalcChecksum.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkRecalcChecksum.Location = new System.Drawing.Point(7, 100);
            this.chkRecalcChecksum.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);
            this.chkRecalcChecksum.Name = "chkRecalcChecksum";
            this.chkRecalcChecksum.Size = new System.Drawing.Size(276, 19);
            this.chkRecalcChecksum.TabIndex = 5;
            this.chkRecalcChecksum.Text = "Recalculate the PE checksum after unpacking";
            this.chkRecalcChecksum.UseVisualStyleBackColor = true;
            // 
            // chkExperimental
            // 
            this.chkExperimental.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.chkExperimental.AutoSize = true;
            this.chkExperimental.Location = new System.Drawing.Point(7, 119);
            this.chkExperimental.Margin = new System.Windows.Forms.Padding(3, 0, 3, 3);
            this.chkExperimental.Name = "chkExperimental";
            this.chkExperimental.Size = new System.Drawing.Size(276, 19);
            this.chkExperimental.TabIndex = 6;
            this.chkExperimental.Text = "Use experimental unpacker features";
            this.chkExperimental.UseVisualStyleBackColor = true;
            // 
            // btnApply
            // 
            this.btnApply.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnApply.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnApply.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnApply.Location = new System.Drawing.Point(166, 239);
            this.btnApply.Name = "btnApply";
            this.btnApply.Size = new System.Drawing.Size(75, 27);
            this.btnApply.TabIndex = 3;
            this.btnApply.Text = "Run";
            this.btnApply.UseVisualStyleBackColor = true;
            this.btnApply.Click += new System.EventHandler(this.btnApply_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnCancel.Location = new System.Drawing.Point(247, 239);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 27);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // SteamlessOptionsForm
            // 
            this.AcceptButton = this.btnApply;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(334, 278);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnApply);
            this.Controls.Add(this.grpOptions);
            this.Controls.Add(this.lblExecutable);
            this.Controls.Add(this.lblTitle);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SteamlessOptionsForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Steamless";
            this.grpOptions.ResumeLayout(false);
            this.tlpOptions.ResumeLayout(false);
            this.tlpOptions.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblExecutable;
        private System.Windows.Forms.GroupBox grpOptions;
        private System.Windows.Forms.TableLayoutPanel tlpOptions;
        private System.Windows.Forms.CheckBox chkKeepBind;
        private System.Windows.Forms.CheckBox chkKeepStub;
        private System.Windows.Forms.CheckBox chkDumpPayload;
        private System.Windows.Forms.CheckBox chkDumpDrmp;
        private System.Windows.Forms.CheckBox chkRealign;
        private System.Windows.Forms.CheckBox chkRecalcChecksum;
        private System.Windows.Forms.CheckBox chkExperimental;
        private System.Windows.Forms.Button btnApply;
        private System.Windows.Forms.Button btnCancel;
    }
}
