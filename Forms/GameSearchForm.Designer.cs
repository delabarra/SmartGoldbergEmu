namespace SmartGoldbergEmu.Forms
{
    partial class GameSearchForm
    {
        private System.ComponentModel.IContainer components = null;

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.lblSearch = new System.Windows.Forms.Label();
            this.txtSearch = new System.Windows.Forms.TextBox();
            this.lstResults = new System.Windows.Forms.ListBox();
            this.ctxList = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.miCopyAppId = new System.Windows.Forms.ToolStripMenuItem();
            this.miCopyName = new System.Windows.Forms.ToolStripMenuItem();
            this.lblSelectedAppId = new System.Windows.Forms.Label();
            this.lblStatus = new System.Windows.Forms.Label();
            this.lblSelectedName = new System.Windows.Forms.Label();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.ctxList.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblSearch
            // 
            this.lblSearch.AutoSize = true;
            this.lblSearch.Location = new System.Drawing.Point(12, 15);
            this.lblSearch.Name = "lblSearch";
            this.lblSearch.Size = new System.Drawing.Size(86, 13);
            this.lblSearch.TabIndex = 0;
            this.lblSearch.TabStop = false;
            this.lblSearch.Text = "Name or App ID:";
            // 
            // txtSearch
            // 
            this.txtSearch.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtSearch.Location = new System.Drawing.Point(113, 12);
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.Size = new System.Drawing.Size(307, 20);
            this.txtSearch.TabIndex = 1;
            this.toolTip.SetToolTip(this.txtSearch, "Search by game name or App ID");
            this.txtSearch.TextChanged += new System.EventHandler(this.OnSearchTextChanged);
            this.txtSearch.KeyDown += new System.Windows.Forms.KeyEventHandler(this.OnSearchKeyDown);
            // 
            // lstResults
            // 
            this.lstResults.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lstResults.ContextMenuStrip = this.ctxList;
            this.lstResults.FormattingEnabled = true;
            this.lstResults.Location = new System.Drawing.Point(12, 38);
            this.lstResults.Name = "lstResults";
            this.lstResults.Size = new System.Drawing.Size(408, 186);
            this.lstResults.TabIndex = 2;
            this.lstResults.SelectedIndexChanged += new System.EventHandler(this.OnResultsSelectedIndexChanged);
            this.lstResults.DoubleClick += new System.EventHandler(this.OnResultsDoubleClick);
            this.lstResults.MouseDown += new System.Windows.Forms.MouseEventHandler(this.OnResultsMouseDown);
            this.lstResults.MouseMove += new System.Windows.Forms.MouseEventHandler(this.OnResultsMouseMove);
            // 
            // ctxList
            // 
            this.ctxList.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miCopyAppId,
            this.miCopyName});
            this.ctxList.Name = "ctxList";
            this.ctxList.Size = new System.Drawing.Size(139, 48);
            this.ctxList.Opening += new System.ComponentModel.CancelEventHandler(this.OnListContextOpening);
            // 
            // miCopyAppId
            // 
            this.miCopyAppId.Name = "miCopyAppId";
            this.miCopyAppId.Size = new System.Drawing.Size(138, 22);
            this.miCopyAppId.Text = "Copy App ID";
            this.miCopyAppId.Click += new System.EventHandler(this.OnCopyAppId_Click);
            // 
            // miCopyName
            // 
            this.miCopyName.Name = "miCopyName";
            this.miCopyName.Size = new System.Drawing.Size(138, 22);
            this.miCopyName.Text = "Copy name";
            this.miCopyName.Click += new System.EventHandler(this.OnCopyName_Click);
            // 
            // lblSelectedAppId
            // 
            this.lblSelectedAppId.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblSelectedAppId.AutoEllipsis = true;
            this.lblSelectedAppId.Location = new System.Drawing.Point(12, 232);
            this.lblSelectedAppId.Name = "lblSelectedAppId";
            this.lblSelectedAppId.Size = new System.Drawing.Size(260, 13);
            this.lblSelectedAppId.TabIndex = 3;
            this.lblSelectedAppId.TabStop = false;
            this.lblSelectedAppId.Text = "";
            // 
            // lblStatus
            // 
            this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.lblStatus.AutoEllipsis = true;
            this.lblStatus.Location = new System.Drawing.Point(278, 232);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(142, 13);
            this.lblStatus.TabIndex = 4;
            this.lblStatus.TabStop = false;
            this.lblStatus.Text = "";
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // lblSelectedName
            // 
            this.lblSelectedName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblSelectedName.AutoEllipsis = true;
            this.lblSelectedName.Location = new System.Drawing.Point(12, 247);
            this.lblSelectedName.Name = "lblSelectedName";
            this.lblSelectedName.Size = new System.Drawing.Size(408, 13);
            this.lblSelectedName.TabIndex = 5;
            this.lblSelectedName.TabStop = false;
            this.lblSelectedName.Text = "";
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Location = new System.Drawing.Point(261, 276);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 6;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.OnOk_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(345, 276);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 7;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.OnCancel_Click);
            // 
            // GameSearchForm
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(432, 311);
            this.Controls.Add(this.lblSearch);
            this.Controls.Add(this.txtSearch);
            this.Controls.Add(this.lstResults);
            this.Controls.Add(this.lblSelectedAppId);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.lblSelectedName);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "GameSearchForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Search Game";
            this.ctxList.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.ListBox lstResults;
        private System.Windows.Forms.Label lblSearch;
        private System.Windows.Forms.Label lblSelectedAppId;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Label lblSelectedName;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.ToolTip toolTip;
        private System.Windows.Forms.ContextMenuStrip ctxList;
        private System.Windows.Forms.ToolStripMenuItem miCopyAppId;
        private System.Windows.Forms.ToolStripMenuItem miCopyName;
    }
}
