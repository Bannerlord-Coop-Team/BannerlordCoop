﻿
using System.Windows.Forms;

namespace Launcher
{
    partial class BannerlordCoopLauncher
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
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BannerlordCoopLauncher));
            this.CloseButton = new System.Windows.Forms.Button();
            this.JoinGame = new System.Windows.Forms.Button();
            this.HostGame = new System.Windows.Forms.Button();
            this.server_status = new System.Windows.Forms.Label();
            this.TopBar = new System.Windows.Forms.Label();
            this.Logo = new System.Windows.Forms.PictureBox();
            this.BottomBar = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.Logo)).BeginInit();
            this.SuspendLayout();
            // 
            // CloseButton
            // 
            this.CloseButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(24)))), ((int)(((byte)(16)))), ((int)(((byte)(14)))));
            this.CloseButton.FlatAppearance.BorderSize = 0;
            this.CloseButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.CloseButton.ForeColor = System.Drawing.SystemColors.Control;
            this.CloseButton.Location = new System.Drawing.Point(759, 12);
            this.CloseButton.Name = "CloseButton";
            this.CloseButton.Size = new System.Drawing.Size(29, 23);
            this.CloseButton.TabIndex = 1;
            this.CloseButton.Text = "✖";
            this.CloseButton.UseVisualStyleBackColor = false;
            this.CloseButton.Click += new System.EventHandler(this.CloseButton_Click);
            // 
            // JoinGame
            // 
            this.JoinGame.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.JoinGame.Font = new System.Drawing.Font("Franklin Gothic Medium", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.JoinGame.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.JoinGame.Location = new System.Drawing.Point(404, 372);
            this.JoinGame.Name = "JoinGame";
            this.JoinGame.Size = new System.Drawing.Size(182, 55);
            this.JoinGame.TabIndex = 3;
            this.JoinGame.Text = "JOIN GAME";
            this.JoinGame.UseVisualStyleBackColor = true;
            this.JoinGame.Click += new System.EventHandler(this.JoinGame_Click);
            // 
            // HostGame
            // 
            this.HostGame.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.HostGame.Font = new System.Drawing.Font("Franklin Gothic Medium", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.HostGame.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.HostGame.Location = new System.Drawing.Point(214, 372);
            this.HostGame.Name = "HostGame";
            this.HostGame.Size = new System.Drawing.Size(184, 55);
            this.HostGame.TabIndex = 4;
            this.HostGame.Text = "HOST GAME";
            this.HostGame.UseVisualStyleBackColor = true;
            this.HostGame.Click += new System.EventHandler(this.HostGame_Click);
            // 
            // server_status
            // 
            this.server_status.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.server_status.AutoSize = true;
            this.server_status.BackColor = System.Drawing.Color.Transparent;
            this.server_status.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.server_status.ForeColor = System.Drawing.SystemColors.ButtonHighlight;
            this.server_status.Location = new System.Drawing.Point(28, 316);
            this.server_status.Name = "server_status";
            this.server_status.Size = new System.Drawing.Size(105, 15);
            this.server_status.TabIndex = 7;
            this.server_status.Text = "SERVER STATUS";
            // 
            // TopBar
            // 
            this.TopBar.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(24)))), ((int)(((byte)(16)))), ((int)(((byte)(14)))));
            this.TopBar.Location = new System.Drawing.Point(-2, -3);
            this.TopBar.Name = "TopBar";
            this.TopBar.Size = new System.Drawing.Size(802, 47);
            this.TopBar.TabIndex = 0;
            this.TopBar.MouseDown += new System.Windows.Forms.MouseEventHandler(this.TopBar_MouseDown);
            this.TopBar.MouseMove += new System.Windows.Forms.MouseEventHandler(this.TopBar_MouseMove);
            this.TopBar.MouseUp += new System.Windows.Forms.MouseEventHandler(this.TopBar_MouseUp);
            // 
            // Logo
            // 
            this.Logo.BackColor = System.Drawing.Color.Transparent;
            this.Logo.Image = global::Launcher.Properties.Resources.logo;
            this.Logo.InitialImage = null;
            this.Logo.Location = new System.Drawing.Point(1, 47);
            this.Logo.Name = "Logo";
            this.Logo.Size = new System.Drawing.Size(799, 298);
            this.Logo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.CenterImage;
            this.Logo.TabIndex = 6;
            this.Logo.TabStop = false;
            this.Logo.WaitOnLoad = true;
            // 
            // BottomBar
            // 
            this.BottomBar.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(24)))), ((int)(((byte)(16)))), ((int)(((byte)(14)))));
            this.BottomBar.Location = new System.Drawing.Point(-2, 348);
            this.BottomBar.Name = "BottomBar";
            this.BottomBar.Size = new System.Drawing.Size(802, 105);
            this.BottomBar.TabIndex = 2;
            // 
            // BannerlordCoopLauncher
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(31)))), ((int)(((byte)(25)))));
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.server_status);
            this.Controls.Add(this.HostGame);
            this.Controls.Add(this.JoinGame);
            this.Controls.Add(this.BottomBar);
            this.Controls.Add(this.Logo);
            this.Controls.Add(this.CloseButton);
            this.Controls.Add(this.TopBar);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("bannerlord_icon")));
            this.Name = "BannerlordCoopLauncher";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "BannerlordCoopLauncher";
            this.Load += new System.EventHandler(this.BannerlordCoopLauncher_Load);
            ((System.ComponentModel.ISupportInitialize)(this.Logo)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private Button CloseButton;
        private Button JoinGame;
        private Button HostGame;
        private Label server_status;
        private Label TopBar;
        private PictureBox Logo;
        private Label BottomBar;
    }
}

