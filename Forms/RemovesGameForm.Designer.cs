namespace SmartGoldbergEmu.Forms
{
    partial class RemovesGameForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (picWarning?.Image != null)
                {
                    picWarning.Image.Dispose();
                    picWarning.Image = null;
                }
                if (components != null)
                    components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.picWarning = new System.Windows.Forms.PictureBox();
            this.lblMessage = new System.Windows.Forms.Label();
            this.lblGameList = new System.Windows.Forms.Label();
            this.chkDeleteFiles = new System.Windows.Forms.CheckBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnRemove = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.picWarning)).BeginInit();
            this.SuspendLayout();
            this.picWarning.Location = new System.Drawing.Point(12, 12);
            this.picWarning.Name = "picWarning";
            this.picWarning.Size = new System.Drawing.Size(32, 32);
            this.picWarning.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.picWarning.TabIndex = 0;
            this.picWarning.TabStop = false;
            this.picWarning.Image = global::System.Drawing.SystemIcons.Warning.ToBitmap();
            this.lblMessage.AutoSize = true;
            this.lblMessage.Location = new System.Drawing.Point(52, 12);
            this.lblMessage.MaximumSize = new System.Drawing.Size(380, 0);
            this.lblMessage.Name = "lblMessage";
            this.lblMessage.Size = new System.Drawing.Size(0, 13);
            this.lblMessage.TabIndex = 1;
            this.lblMessage.TabStop = false;
            this.lblMessage.UseMnemonic = false;
            this.lblGameList.AutoSize = true;
            this.lblGameList.Location = new System.Drawing.Point(52, 36);
            this.lblGameList.Name = "lblGameList";
            this.lblGameList.Size = new System.Drawing.Size(0, 13);
            this.lblGameList.TabIndex = 2;
            this.lblGameList.TabStop = false;
            this.lblGameList.UseMnemonic = false;
            this.chkDeleteFiles.AutoSize = true;
            this.chkDeleteFiles.Location = new System.Drawing.Point(52, 72);
            this.chkDeleteFiles.MaximumSize = new System.Drawing.Size(380, 0);
            this.chkDeleteFiles.Name = "chkDeleteFiles";
            this.chkDeleteFiles.Size = new System.Drawing.Size(139, 17);
            this.chkDeleteFiles.TabIndex = 3;
            this.chkDeleteFiles.Text = "Remove emulator settings";
            this.chkDeleteFiles.UseMnemonic = false;
            this.chkDeleteFiles.UseVisualStyleBackColor = true;
            this.btnCancel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(361, 120);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 5;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnRemove.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            this.btnRemove.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnRemove.Location = new System.Drawing.Point(278, 120);
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Size = new System.Drawing.Size(75, 23);
            this.btnRemove.TabIndex = 4;
            this.btnRemove.Text = "Remove";
            this.btnRemove.UseVisualStyleBackColor = true;
            this.AcceptButton = this.btnRemove;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(500, 160);
            this.Controls.Add(this.btnRemove);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.chkDeleteFiles);
            this.Controls.Add(this.lblGameList);
            this.Controls.Add(this.lblMessage);
            this.Controls.Add(this.picWarning);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(400, 150);
            this.Name = "RemovesGameForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Remove Game";
            ((System.ComponentModel.ISupportInitialize)(this.picWarning)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.PictureBox picWarning;
        private System.Windows.Forms.Label lblMessage;
        private System.Windows.Forms.Label lblGameList;
        private System.Windows.Forms.CheckBox chkDeleteFiles;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnRemove;
    }
}
