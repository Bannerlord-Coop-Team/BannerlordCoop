using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Launcher
{
    public partial class BannerlordCoopLauncher : Form
    {
        private bool mouseDown;
        private Point lastLocation;
        public BannerlordCoopLauncher()
        {
            InitializeComponent();
            Console.WriteLine("MB Path is: " + GetBannerLordInstalledPath());
        }

        private string GetBannerLordInstalledPath()
        {

            string registry_key = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            using (Microsoft.Win32.RegistryKey key = Registry.LocalMachine.OpenSubKey(registry_key))
            {
                foreach (string subkey_name in key.GetSubKeyNames())
                {
                    using (RegistryKey subkey = key.OpenSubKey(subkey_name))
                    {
                        string name = (string)subkey.GetValue("DisplayName");
                        if (name != null && name.Equals("Mount & Blade II: Bannerlord"))
                        {
                            return subkey.GetValue("installlocation").ToString();
                        }
                    }
                }
            }
            return null;
        }


        private void TopBar_MouseUp(object sender, MouseEventArgs e)
        {
            mouseDown = false;
        }
        private void TopBar_MouseDown(object sender, MouseEventArgs e)
        {
            mouseDown = true;
            lastLocation = e.Location;
        }
        private void TopBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (mouseDown)
            {
                this.Location = new Point(
                    (this.Location.X - lastLocation.X) + e.X, (this.Location.Y - lastLocation.Y) + e.Y);

                this.Update();
            }
        }

        private void CloseButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void HostGame_Click(object sender, EventArgs e)
        {
            Process process = new Process();
            string mbPath = GetBannerLordInstalledPath();
            process.StartInfo.WorkingDirectory = mbPath + @"\bin\Win64_Shipping_Client\";
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.CreateNoWindow = false;
            process.StartInfo.FileName = "\"" + mbPath + @"\bin\Win64_Shipping_Client\Bannerlord.exe" + "\"";
            process.StartInfo.Arguments = "/singleplayer /server _MODULES_*Native*SandBoxCore*CustomBattle*SandBox*StoryMode*Coop*_MODULES_";
            process.Start();
            process.Close();
        }

        private void JoinGame_Click(object sender, EventArgs e)
        {
            Process process = new Process();
            string mbPath = GetBannerLordInstalledPath();
            process.StartInfo.WorkingDirectory =   mbPath + @"\bin\Win64_Shipping_Client\";
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.CreateNoWindow = false;
            process.StartInfo.FileName = "\"" + mbPath + @"\bin\Win64_Shipping_Client\Bannerlord.exe" + "\"";
            process.StartInfo.Arguments =  "/singleplayer /client _MODULES_*Native*SandBoxCore*CustomBattle*SandBox*StoryMode*Coop*_MODULES_";
            process.Start();
            process.Close();
        }
    }
}
