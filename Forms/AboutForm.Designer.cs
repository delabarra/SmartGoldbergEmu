using System.Drawing;
using System.Windows.Forms;
using SmartGoldbergEmu.Properties;

namespace SmartGoldbergEmu.Forms
{
    partial class AboutForm
    {
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.picLogo = new PictureBox();
            this.lblTitle = new Label();
            this.lblDescription = new Label();
            this.lblVersion = new Label();
            this.linkRepository = new LinkLabel();
            this.btnOk = new Button();
            ((System.ComponentModel.ISupportInitialize)(this.picLogo)).BeginInit();
            this.SuspendLayout();
            this.picLogo.Image = Resources.gold_steam_128_logo;
            this.picLogo.Location = new Point(12, 12);
            this.picLogo.Margin = new Padding(2);
            this.picLogo.Name = "picLogo";
            this.picLogo.Size = new Size(110, 110);
            this.picLogo.SizeMode = PictureBoxSizeMode.Zoom;
            this.picLogo.TabIndex = 0;
            this.picLogo.TabStop = false;
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, ((byte)(0)));
            this.lblTitle.Location = new Point(132, 12);
            this.lblTitle.Margin = new Padding(2, 0, 2, 0);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new Size(234, 21);
            this.lblTitle.TabIndex = 1;
            this.lblTitle.TabStop = false;
            this.lblTitle.Text = "SmartGoldbergEmu Launcher";
            this.lblDescription.AutoSize = true;
            this.lblDescription.Font = new Font("Segoe UI", 9F);
            this.lblDescription.Location = new Point(132, 42);
            this.lblDescription.Margin = new Padding(2, 0, 2, 0);
            this.lblDescription.MaximumSize = new Size(250, 0);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Size = new Size(248, 30);
            this.lblDescription.TabIndex = 2;
            this.lblDescription.TabStop = false;
            this.lblDescription.Text = "Streamlines and automates the configuration process for the Goldberg Steam Emulat" +
    "or.";
            this.lblVersion.AutoSize = true;
            this.lblVersion.Font = new Font("Segoe UI", 9F);
            this.lblVersion.Location = new Point(132, 78);
            this.lblVersion.Margin = new Padding(2, 0, 2, 0);
            this.lblVersion.Name = "lblVersion";
            this.lblVersion.Size = new Size(100, 15);
            this.lblVersion.TabIndex = 3;
            this.lblVersion.TabStop = false;
            this.lblVersion.Text = "Version";
            this.linkRepository.AutoSize = true;
            this.linkRepository.Font = new Font("Segoe UI", 8.5F);
            this.linkRepository.LinkArea = new LinkArea(0, 6);
            this.linkRepository.Location = new Point(132, 98);
            this.linkRepository.Margin = new Padding(2, 0, 2, 0);
            this.linkRepository.Name = "linkRepository";
            this.linkRepository.Size = new Size(45, 15);
            this.linkRepository.TabIndex = 4;
            this.linkRepository.TabStop = true;
            this.linkRepository.Text = "GitHub";
            this.btnOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.btnOk.DialogResult = DialogResult.OK;
            this.btnOk.Location = new Point(332, 128);
            this.btnOk.Margin = new Padding(2);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new Size(75, 23);
            this.btnOk.TabIndex = 5;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.AcceptButton = this.btnOk;
            this.CancelButton = this.btnOk;
            this.AutoScaleDimensions = new SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(419, 163);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.linkRepository);
            this.Controls.Add(this.lblVersion);
            this.Controls.Add(this.lblDescription);
            this.Controls.Add(this.lblTitle);
            this.Controls.Add(this.picLogo);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.Margin = new Padding(2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AboutForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "About SmartGoldbergEmu";
            ((System.ComponentModel.ISupportInitialize)(this.picLogo)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private PictureBox picLogo;
        private Label lblTitle;
        private Label lblDescription;
        private Label lblVersion;
        private LinkLabel linkRepository;
        private Button btnOk;
    }
}
