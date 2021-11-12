namespace BannerlordCoopInstaller
{
    partial class BannerlordCoopInstallerForm
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
            if (disposing && (components != null))
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
        /// 
        private void InitializeComponent()
        {
            this.Title = new System.Windows.Forms.Label();
            this.BannerlordPath = new System.Windows.Forms.TextBox();
            this.Install = new System.Windows.Forms.Button();
            this.PathLabel = new System.Windows.Forms.Label();
            this.PathBrowse = new System.Windows.Forms.Button();
            this.InstallProgress = new System.Windows.Forms.ProgressBar();
            this.Status = new System.Windows.Forms.Label();
            this.ShortcutCheckbox = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // Title
            // 
            this.Title.AutoSize = true;
            this.Title.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Title.Location = new System.Drawing.Point(12, 9);
            this.Title.Name = "Title";
            this.Title.Size = new System.Drawing.Size(232, 24);
            this.Title.TabIndex = 0;
            this.Title.Text = "Bannerlord Coop Installer";
            // 
            // BannerlordPath
            // 
            this.BannerlordPath.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.BannerlordPath.Font = new System.Drawing.Font("Arial", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.BannerlordPath.Location = new System.Drawing.Point(15, 151);
            this.BannerlordPath.Name = "BannerlordPath";
            this.BannerlordPath.Size = new System.Drawing.Size(532, 22);
            this.BannerlordPath.TabIndex = 1;
            this.BannerlordPath.TextChanged += new System.EventHandler(this.BannerLordPath_TextChanged);
            // 
            // Install
            // 
            this.Install.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.Install.Enabled = false;
            this.Install.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Install.Location = new System.Drawing.Point(527, 480);
            this.Install.Name = "Install";
            this.Install.Size = new System.Drawing.Size(95, 29);
            this.Install.TabIndex = 2;
            this.Install.Text = "Install";
            this.Install.UseVisualStyleBackColor = true;
            this.Install.Click += new System.EventHandler(this.Install_Click);
            // 
            // PathLabel
            // 
            this.PathLabel.AutoSize = true;
            this.PathLabel.Location = new System.Drawing.Point(13, 135);
            this.PathLabel.Name = "PathLabel";
            this.PathLabel.Size = new System.Drawing.Size(170, 13);
            this.PathLabel.TabIndex = 4;
            this.PathLabel.Text = "Mount && Blade II: Bannerlord Path:";
            this.PathLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // PathBrowse
            // 
            this.PathBrowse.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.PathBrowse.AutoSize = true;
            this.PathBrowse.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.PathBrowse.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.PathBrowse.Location = new System.Drawing.Point(553, 149);
            this.PathBrowse.Name = "PathBrowse";
            this.PathBrowse.Size = new System.Drawing.Size(63, 26);
            this.PathBrowse.TabIndex = 3;
            this.PathBrowse.Text = "Browse";
            this.PathBrowse.UseVisualStyleBackColor = true;
            this.PathBrowse.Click += new System.EventHandler(this.PathBrowse_Click);
            // 
            // InstallProgress
            // 
            this.InstallProgress.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.InstallProgress.Location = new System.Drawing.Point(12, 480);
            this.InstallProgress.Name = "InstallProgress";
            this.InstallProgress.Size = new System.Drawing.Size(509, 29);
            this.InstallProgress.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.InstallProgress.TabIndex = 5;
            // 
            // Status
            // 
            this.Status.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.Status.AutoSize = true;
            this.Status.Location = new System.Drawing.Point(12, 461);
            this.Status.Name = "Status";
            this.Status.Size = new System.Drawing.Size(63, 13);
            this.Status.TabIndex = 6;
            this.Status.Text = "Placeholder";
            // 
            // ShortcutCheckbox
            // 
            this.ShortcutCheckbox.AutoSize = true;
            this.ShortcutCheckbox.Checked = true;
            this.ShortcutCheckbox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ShortcutCheckbox.Location = new System.Drawing.Point(16, 217);
            this.ShortcutCheckbox.Name = "ShortcutCheckbox";
            this.ShortcutCheckbox.Size = new System.Drawing.Size(100, 17);
            this.ShortcutCheckbox.TabIndex = 7;
            this.ShortcutCheckbox.Text = "Create Shortcut";
            this.ShortcutCheckbox.UseVisualStyleBackColor = true;
            // 
            // BannerlordCoopInstallerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(634, 521);
            this.Controls.Add(this.ShortcutCheckbox);
            this.Controls.Add(this.Status);
            this.Controls.Add(this.InstallProgress);
            this.Controls.Add(this.PathLabel);
            this.Controls.Add(this.PathBrowse);
            this.Controls.Add(this.Install);
            this.Controls.Add(this.BannerlordPath);
            this.Controls.Add(this.Title);
            this.MinimumSize = new System.Drawing.Size(650, 560);
            this.Name = "BannerlordCoopInstallerForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.Text = "Bannerlord Coop Installer";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label Title;
        private System.Windows.Forms.TextBox BannerlordPath;
        private System.Windows.Forms.Button Install;
        private System.Windows.Forms.Label PathLabel;
        private System.Windows.Forms.Button PathBrowse;
        private System.Windows.Forms.ProgressBar InstallProgress;
        private System.Windows.Forms.Label Status;
        private System.Windows.Forms.CheckBox ShortcutCheckbox;
    }
}

