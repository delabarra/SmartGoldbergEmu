// Visual styling: ThemeService.ApplyTheme is invoked from EmulatorForkSelectionForm.cs (same pattern as AboutForm).

namespace SmartGoldbergEmu.Forms
{
    partial class ForkSelectForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.grpFork = new System.Windows.Forms.GroupBox();
            this.rbAlex = new System.Windows.Forms.RadioButton();
            this.rbDetanup = new System.Windows.Forms.RadioButton();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.chkUpdateFilesOnOk = new System.Windows.Forms.CheckBox();
            this.grpFork.SuspendLayout();
            this.SuspendLayout();
            // 
            // grpFork
            // 
            this.grpFork.Controls.Add(this.rbAlex);
            this.grpFork.Controls.Add(this.rbDetanup);
            this.grpFork.Location = new System.Drawing.Point(12, 12);
            this.grpFork.Name = "grpFork";
            this.grpFork.Size = new System.Drawing.Size(294, 80);
            this.grpFork.TabIndex = 0;
            this.grpFork.TabStop = false;
            this.grpFork.Text = "Emulator Fork";
            // 
            // rbAlex
            // 
            this.rbAlex.AutoSize = true;
            this.rbAlex.Location = new System.Drawing.Point(16, 47);
            this.rbAlex.Name = "rbAlex";
            this.rbAlex.Size = new System.Drawing.Size(240, 17);
            this.rbAlex.TabIndex = 1;
            this.rbAlex.Text = "Alex47exe - (github.com/alex47exe/gse_fork)";
            this.rbAlex.UseVisualStyleBackColor = true;
            // 
            // rbDetanup
            // 
            this.rbDetanup.AutoSize = true;
            this.rbDetanup.Location = new System.Drawing.Point(16, 22);
            this.rbDetanup.Name = "rbDetanup";
            this.rbDetanup.Size = new System.Drawing.Size(250, 17);
            this.rbDetanup.TabIndex = 0;
            this.rbDetanup.TabStop = true;
            this.rbDetanup.Text = "Detanup01 - (github.com/Detanup01/gbe_fork)";
            this.rbDetanup.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(148, 103);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 3;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.OnOk_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(231, 103);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 4;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // chkUpdateFilesOnOk
            // 
            this.chkUpdateFilesOnOk.AutoSize = true;
            this.chkUpdateFilesOnOk.Checked = true;
            this.chkUpdateFilesOnOk.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkUpdateFilesOnOk.Location = new System.Drawing.Point(12, 107);
            this.chkUpdateFilesOnOk.Name = "chkUpdateFilesOnOk";
            this.chkUpdateFilesOnOk.Size = new System.Drawing.Size(125, 17);
            this.chkUpdateFilesOnOk.TabIndex = 2;
            this.chkUpdateFilesOnOk.Text = "Update emulator files";
            this.chkUpdateFilesOnOk.UseVisualStyleBackColor = true;
            // 
            // ForkSelectForm
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(318, 138);
            this.Controls.Add(this.chkUpdateFilesOnOk);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.grpFork);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ForkSelectForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Emulator Fork";
            this.grpFork.ResumeLayout(false);
            this.grpFork.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox grpFork;
        private System.Windows.Forms.RadioButton rbDetanup;
        private System.Windows.Forms.RadioButton rbAlex;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.CheckBox chkUpdateFilesOnOk;
    }
}
