using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Launcher
{
    public partial class BannerlordCoopLauncher : Form
    {
        private bool mouseDown;
        private Point lastLocation;
        private string mbPath;


        public BannerlordCoopLauncher()
        {
            InitializeComponent();
            this.mbPath = Path.GetFullPath($@"{System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase).Replace(@"file:\", "")}\..\..\..\..\");
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

        private void StartGame(string additionalArgs)
        {
            Process process = new Process();
            process.StartInfo.WorkingDirectory = this.mbPath + @"\bin\Win64_Shipping_Client\";
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.CreateNoWindow = false;
            process.StartInfo.FileName = "\"" + mbPath + @"\bin\Win64_Shipping_Client\Bannerlord.exe" + "\"";
            process.StartInfo.Arguments = $"/singleplayer {additionalArgs} _MODULES_*Native*SandBoxCore*CustomBattle*SandBox*StoryMode*Coop*_MODULES_";
            process.Start();
            process.Close();
        }

        private void CloseButton_Click(object sender, EventArgs e)
        {
            Close();
        }
        /// <summary>
        /// Start a new process to run the application, then close it. Launch the game in client mode
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HostGame_Click(object sender, EventArgs e)
        {
            StartGame("/server");
        }
        /// <summary>
        /// Start a new process to run the application, then close it. Launch the game in sever mode
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void JoinGame_Click(object sender, EventArgs e)
        {
            StartGame("/client");
        }
    }
}
