namespace SmartGoldbergEmu.Forms
{
    partial class LaunchOptionsForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblInstruction = new System.Windows.Forms.Label();
            this.lstLaunchOptions = new System.Windows.Forms.ListBox();
            this.lblDetails = new System.Windows.Forms.Label();
            this.chkShowBetaOptions = new System.Windows.Forms.CheckBox();
            this.btnLaunch = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnUseDefaultLaunch = new System.Windows.Forms.Button();
            this.toolTipLaunchDetails = new System.Windows.Forms.ToolTip(this.components);
            this.SuspendLayout();
            //
            // lblTitle
            //
            this.lblTitle.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Location = new System.Drawing.Point(10, 10);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(394, 22);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.TabStop = false;
            this.lblTitle.Text = "Launch Options";
            this.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // lblInstruction
            //
            this.lblInstruction.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblInstruction.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lblInstruction.Location = new System.Drawing.Point(10, 36);
            this.lblInstruction.Name = "lblInstruction";
            this.lblInstruction.Size = new System.Drawing.Size(394, 17);
            this.lblInstruction.TabIndex = 1;
            this.lblInstruction.TabStop = false;
            this.lblInstruction.Text = "Select how you want to launch this game:";
            this.lblInstruction.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // lstLaunchOptions
            //
            this.lstLaunchOptions.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lstLaunchOptions.DisplayMember = "Description";
            this.lstLaunchOptions.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.lstLaunchOptions.FormattingEnabled = true;
            this.lstLaunchOptions.ItemHeight = 15;
            this.lstLaunchOptions.Location = new System.Drawing.Point(10, 61);
            this.lstLaunchOptions.Name = "lstLaunchOptions";
            this.lstLaunchOptions.Size = new System.Drawing.Size(395, 109);
            this.lstLaunchOptions.TabIndex = 2;
            this.lstLaunchOptions.SelectedIndexChanged += new System.EventHandler(this.OnLaunchOptionsSelectedIndexChanged);
            this.lstLaunchOptions.DoubleClick += new System.EventHandler(this.OnLaunchOptionsDoubleClick);
            this.lstLaunchOptions.KeyDown += new System.Windows.Forms.KeyEventHandler(this.OnLaunchOptionsKeyDown);
            //
            // lblDetails
            //
            this.lblDetails.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblDetails.AutoEllipsis = true;
            this.lblDetails.Cursor = System.Windows.Forms.Cursors.Hand;
            this.lblDetails.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.lblDetails.ForeColor = System.Drawing.Color.Gray;
            this.lblDetails.Location = new System.Drawing.Point(10, 181);
            this.lblDetails.Name = "lblDetails";
            this.lblDetails.Size = new System.Drawing.Size(395, 100);
            this.lblDetails.TabIndex = 3;
            this.lblDetails.TabStop = true;
            this.lblDetails.UseMnemonic = false;
            this.lblDetails.Text = "Select an option to see details";
            this.lblDetails.Click += new System.EventHandler(this.OnCopyLaunchDetails_Click);
            //
            // chkShowBetaOptions
            //
            this.chkShowBetaOptions.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.chkShowBetaOptions.AutoSize = true;
            this.chkShowBetaOptions.Location = new System.Drawing.Point(10, 286);
            this.chkShowBetaOptions.Name = "chkShowBetaOptions";
            this.chkShowBetaOptions.Size = new System.Drawing.Size(117, 17);
            this.chkShowBetaOptions.TabIndex = 4;
            this.chkShowBetaOptions.Text = "Show Beta Options";
            this.chkShowBetaOptions.UseVisualStyleBackColor = true;
            this.chkShowBetaOptions.CheckedChanged += new System.EventHandler(this.chkShowBetaOptions_CheckedChanged);
            //
            // btnLaunch
            //
            this.btnLaunch.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnLaunch.Enabled = false;
            this.btnLaunch.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnLaunch.Location = new System.Drawing.Point(247, 313);
            this.btnLaunch.Name = "btnLaunch";
            this.btnLaunch.Size = new System.Drawing.Size(75, 27);
            this.btnLaunch.TabIndex = 6;
            this.btnLaunch.Tag = "LaunchDialogButton";
            this.btnLaunch.Text = "Launch";
            this.btnLaunch.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnLaunch.UseCompatibleTextRendering = false;
            this.btnLaunch.UseVisualStyleBackColor = true;
            this.btnLaunch.Click += new System.EventHandler(this.OnLaunch_Click);
            //
            // btnCancel
            //
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnCancel.Location = new System.Drawing.Point(330, 313);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 27);
            this.btnCancel.TabIndex = 7;
            this.btnCancel.Tag = "LaunchDialogButton";
            this.btnCancel.Text = "Cancel";
            this.btnCancel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnCancel.UseCompatibleTextRendering = false;
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.OnCancel_Click);
            //
            // btnUseDefaultLaunch
            //
            this.btnUseDefaultLaunch.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnUseDefaultLaunch.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnUseDefaultLaunch.Location = new System.Drawing.Point(10, 313);
            this.btnUseDefaultLaunch.Name = "btnUseDefaultLaunch";
            this.btnUseDefaultLaunch.Size = new System.Drawing.Size(75, 27);
            this.btnUseDefaultLaunch.TabIndex = 5;
            this.btnUseDefaultLaunch.Tag = "LaunchDialogButton";
            this.btnUseDefaultLaunch.Text = "Skip";
            this.btnUseDefaultLaunch.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnUseDefaultLaunch.UseCompatibleTextRendering = false;
            this.btnUseDefaultLaunch.UseVisualStyleBackColor = true;
            this.btnUseDefaultLaunch.Click += new System.EventHandler(this.OnUseDefaultLaunch_Click);
            //
            // LaunchOptionsForm
            //
            this.AcceptButton = this.btnLaunch;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(415, 350);
            this.Controls.Add(this.chkShowBetaOptions);
            this.Controls.Add(this.lblDetails);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnUseDefaultLaunch);
            this.Controls.Add(this.btnLaunch);
            this.Controls.Add(this.lstLaunchOptions);
            this.Controls.Add(this.lblInstruction);
            this.Controls.Add(this.lblTitle);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "LaunchOptionsForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Select Launch Option";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblInstruction;
        private System.Windows.Forms.ListBox lstLaunchOptions;
        private System.Windows.Forms.Label lblDetails;
        private System.Windows.Forms.CheckBox chkShowBetaOptions;
        private System.Windows.Forms.ToolTip toolTipLaunchDetails;
        private System.Windows.Forms.Button btnLaunch;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnUseDefaultLaunch;
    }
}
