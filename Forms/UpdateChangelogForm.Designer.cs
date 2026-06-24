namespace SmartGoldbergEmu.Forms

{

    partial class UpdateChangelogForm

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

            this.flpHeader = new System.Windows.Forms.FlowLayoutPanel();

            this.lblHeadline = new System.Windows.Forms.Label();

            this.lblReleaseNotesCaption = new System.Windows.Forms.Label();

            this.pnlNotes = new System.Windows.Forms.Panel();

            this.rtbReleaseNotes = new System.Windows.Forms.RichTextBox();

            this.tlpFooter = new System.Windows.Forms.TableLayoutPanel();

            this.lblAdditionalInfo = new System.Windows.Forms.Label();

            this.lblProceedQuestion = new System.Windows.Forms.Label();

            this.lblManualDownloadCaption = new System.Windows.Forms.Label();

            this.flpManualLinks = new System.Windows.Forms.FlowLayoutPanel();

            this.flpButtons = new System.Windows.Forms.FlowLayoutPanel();

            this.btnCancel = new System.Windows.Forms.Button();

            this.btnOk = new System.Windows.Forms.Button();

            this.flpHeader.SuspendLayout();

            this.pnlNotes.SuspendLayout();

            this.tlpFooter.SuspendLayout();

            this.flpButtons.SuspendLayout();

            this.SuspendLayout();

            // 

            // flpHeader

            // 

            this.flpHeader.AutoSize = true;

            this.flpHeader.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;

            this.flpHeader.Controls.Add(this.lblHeadline);

            this.flpHeader.Controls.Add(this.lblReleaseNotesCaption);

            this.flpHeader.Dock = System.Windows.Forms.DockStyle.Top;

            this.flpHeader.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;

            this.flpHeader.Location = new System.Drawing.Point(0, 0);

            this.flpHeader.Name = "flpHeader";

            this.flpHeader.Padding = new System.Windows.Forms.Padding(12, 12, 12, 4);

            this.flpHeader.Size = new System.Drawing.Size(580, 52);

            this.flpHeader.TabIndex = 0;

            this.flpHeader.WrapContents = false;

            // 

            // lblHeadline

            // 

            this.lblHeadline.AutoSize = true;

            this.lblHeadline.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));

            this.lblHeadline.Location = new System.Drawing.Point(15, 12);

            this.lblHeadline.Margin = new System.Windows.Forms.Padding(3, 0, 3, 6);

            this.lblHeadline.MaximumSize = new System.Drawing.Size(556, 0);

            this.lblHeadline.Name = "lblHeadline";

            this.lblHeadline.Size = new System.Drawing.Size(63, 17);

            this.lblHeadline.TabIndex = 0;

            this.lblHeadline.Text = "Headline";

            this.lblHeadline.UseMnemonic = false;

            // 

            // lblReleaseNotesCaption

            // 

            this.lblReleaseNotesCaption.AutoSize = true;

            this.lblReleaseNotesCaption.Location = new System.Drawing.Point(15, 35);

            this.lblReleaseNotesCaption.Name = "lblReleaseNotesCaption";

            this.lblReleaseNotesCaption.Size = new System.Drawing.Size(78, 13);

            this.lblReleaseNotesCaption.TabIndex = 1;

            this.lblReleaseNotesCaption.Text = "Release notes:";

            // 

            // pnlNotes

            // 

            this.pnlNotes.Controls.Add(this.rtbReleaseNotes);

            this.pnlNotes.Dock = System.Windows.Forms.DockStyle.Fill;

            this.pnlNotes.Location = new System.Drawing.Point(0, 52);

            this.pnlNotes.Name = "pnlNotes";

            this.pnlNotes.Padding = new System.Windows.Forms.Padding(12, 0, 12, 8);

            this.pnlNotes.Size = new System.Drawing.Size(580, 355);

            this.pnlNotes.TabIndex = 1;

            // 

            // rtbReleaseNotes

            // 

            this.rtbReleaseNotes.Dock = System.Windows.Forms.DockStyle.Fill;

            this.rtbReleaseNotes.Location = new System.Drawing.Point(12, 0);

            this.rtbReleaseNotes.Name = "rtbReleaseNotes";

            this.rtbReleaseNotes.Size = new System.Drawing.Size(556, 349);

            this.rtbReleaseNotes.TabIndex = 0;

            this.rtbReleaseNotes.Text = "";

            // 

            // tlpFooter

            // 

            this.tlpFooter.AutoSize = true;

            this.tlpFooter.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;

            this.tlpFooter.ColumnCount = 1;

            this.tlpFooter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));

            this.tlpFooter.Controls.Add(this.lblAdditionalInfo, 0, 0);

            this.tlpFooter.Controls.Add(this.lblProceedQuestion, 0, 1);

            this.tlpFooter.Controls.Add(this.lblManualDownloadCaption, 0, 2);

            this.tlpFooter.Controls.Add(this.flpManualLinks, 0, 3);

            this.tlpFooter.Controls.Add(this.flpButtons, 0, 4);

            this.tlpFooter.Dock = System.Windows.Forms.DockStyle.Bottom;

            this.tlpFooter.Location = new System.Drawing.Point(0, 407);

            this.tlpFooter.Name = "tlpFooter";

            this.tlpFooter.Padding = new System.Windows.Forms.Padding(12, 8, 12, 12);

            this.tlpFooter.RowCount = 5;

            this.tlpFooter.RowStyles.Add(new System.Windows.Forms.RowStyle());

            this.tlpFooter.RowStyles.Add(new System.Windows.Forms.RowStyle());

            this.tlpFooter.RowStyles.Add(new System.Windows.Forms.RowStyle());

            this.tlpFooter.RowStyles.Add(new System.Windows.Forms.RowStyle());

            this.tlpFooter.RowStyles.Add(new System.Windows.Forms.RowStyle());

            this.tlpFooter.Size = new System.Drawing.Size(580, 113);

            this.tlpFooter.TabIndex = 2;

            // 

            // lblAdditionalInfo

            // 

            this.lblAdditionalInfo.AutoSize = true;

            this.lblAdditionalInfo.Dock = System.Windows.Forms.DockStyle.Fill;

            this.lblAdditionalInfo.Location = new System.Drawing.Point(15, 8);

            this.lblAdditionalInfo.Margin = new System.Windows.Forms.Padding(3, 0, 3, 8);

            this.lblAdditionalInfo.MaximumSize = new System.Drawing.Size(556, 0);

            this.lblAdditionalInfo.Name = "lblAdditionalInfo";

            this.lblAdditionalInfo.Size = new System.Drawing.Size(550, 13);

            this.lblAdditionalInfo.TabIndex = 0;

            // 

            // lblProceedQuestion

            // 

            this.lblProceedQuestion.AutoSize = true;

            this.lblProceedQuestion.Dock = System.Windows.Forms.DockStyle.Fill;

            this.lblProceedQuestion.Location = new System.Drawing.Point(15, 29);

            this.lblProceedQuestion.Margin = new System.Windows.Forms.Padding(3, 0, 3, 8);

            this.lblProceedQuestion.MaximumSize = new System.Drawing.Size(556, 0);

            this.lblProceedQuestion.Name = "lblProceedQuestion";

            this.lblProceedQuestion.Size = new System.Drawing.Size(550, 13);

            this.lblProceedQuestion.TabIndex = 1;

            this.lblProceedQuestion.Text = "Do you want to proceed with the installation?";

            // 

            // lblManualDownloadCaption

            // 

            this.lblManualDownloadCaption.AutoSize = true;

            this.lblManualDownloadCaption.Dock = System.Windows.Forms.DockStyle.Fill;

            this.lblManualDownloadCaption.Location = new System.Drawing.Point(15, 50);

            this.lblManualDownloadCaption.Margin = new System.Windows.Forms.Padding(3, 0, 3, 4);

            this.lblManualDownloadCaption.MaximumSize = new System.Drawing.Size(556, 0);

            this.lblManualDownloadCaption.Name = "lblManualDownloadCaption";

            this.lblManualDownloadCaption.Size = new System.Drawing.Size(550, 13);

            this.lblManualDownloadCaption.TabIndex = 2;

            this.lblManualDownloadCaption.Text = "You can also manually download and setup from:";

            // 

            // flpManualLinks

            // 

            this.flpManualLinks.AutoSize = true;

            this.flpManualLinks.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;

            this.flpManualLinks.Dock = System.Windows.Forms.DockStyle.Fill;

            this.flpManualLinks.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;

            this.flpManualLinks.Location = new System.Drawing.Point(15, 67);

            this.flpManualLinks.Margin = new System.Windows.Forms.Padding(3, 0, 3, 8);

            this.flpManualLinks.Name = "flpManualLinks";

            this.flpManualLinks.Size = new System.Drawing.Size(550, 1);

            this.flpManualLinks.TabIndex = 3;

            this.flpManualLinks.WrapContents = false;

            // 

            // flpButtons

            // 

            this.flpButtons.AutoSize = true;

            this.flpButtons.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;

            this.flpButtons.Controls.Add(this.btnCancel);

            this.flpButtons.Controls.Add(this.btnOk);

            this.flpButtons.Dock = System.Windows.Forms.DockStyle.Fill;

            this.flpButtons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;

            this.flpButtons.Location = new System.Drawing.Point(15, 75);

            this.flpButtons.Margin = new System.Windows.Forms.Padding(3, 0, 3, 0);

            this.flpButtons.Name = "flpButtons";

            this.flpButtons.Size = new System.Drawing.Size(550, 26);

            this.flpButtons.TabIndex = 4;

            this.flpButtons.WrapContents = false;

            // 

            // btnCancel

            // 

            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;

            this.btnCancel.Location = new System.Drawing.Point(475, 3);

            this.btnCancel.Margin = new System.Windows.Forms.Padding(6, 3, 0, 0);

            this.btnCancel.Name = "btnCancel";

            this.btnCancel.Size = new System.Drawing.Size(75, 23);

            this.btnCancel.TabIndex = 1;

            this.btnCancel.Text = "Cancel";

            this.btnCancel.UseVisualStyleBackColor = true;

            // 

            // btnOk

            // 

            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;

            this.btnOk.Location = new System.Drawing.Point(394, 3);

            this.btnOk.Margin = new System.Windows.Forms.Padding(3, 3, 0, 0);

            this.btnOk.Name = "btnOk";

            this.btnOk.Size = new System.Drawing.Size(75, 23);

            this.btnOk.TabIndex = 0;

            this.btnOk.Text = "OK";

            this.btnOk.UseVisualStyleBackColor = true;

            // 

            // UpdateChangelogForm

            // 

            this.AcceptButton = this.btnOk;

            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);

            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;

            this.CancelButton = this.btnCancel;

            this.ClientSize = new System.Drawing.Size(580, 520);

            this.Controls.Add(this.pnlNotes);

            this.Controls.Add(this.tlpFooter);

            this.Controls.Add(this.flpHeader);

            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;

            this.MaximizeBox = false;

            this.MinimizeBox = false;

            this.Name = "UpdateChangelogForm";

            this.ShowIcon = false;

            this.ShowInTaskbar = true;

            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;

            this.Text = "Update Available";

            this.flpHeader.ResumeLayout(false);

            this.flpHeader.PerformLayout();

            this.pnlNotes.ResumeLayout(false);

            this.tlpFooter.ResumeLayout(false);

            this.tlpFooter.PerformLayout();

            this.flpButtons.ResumeLayout(false);

            this.ResumeLayout(false);

            this.PerformLayout();



        }



        #endregion



        private System.Windows.Forms.FlowLayoutPanel flpHeader;

        private System.Windows.Forms.Label lblHeadline;

        private System.Windows.Forms.Label lblReleaseNotesCaption;

        private System.Windows.Forms.Panel pnlNotes;

        private System.Windows.Forms.RichTextBox rtbReleaseNotes;

        private System.Windows.Forms.TableLayoutPanel tlpFooter;

        private System.Windows.Forms.Label lblAdditionalInfo;

        private System.Windows.Forms.Label lblProceedQuestion;

        private System.Windows.Forms.Label lblManualDownloadCaption;

        private System.Windows.Forms.FlowLayoutPanel flpManualLinks;

        private System.Windows.Forms.FlowLayoutPanel flpButtons;

        private System.Windows.Forms.Button btnOk;

        private System.Windows.Forms.Button btnCancel;

    }

}


