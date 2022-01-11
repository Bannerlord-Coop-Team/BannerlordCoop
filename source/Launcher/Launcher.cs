using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Launcher
{



    public partial class BannerlordCoopLauncher : Form
    {
        private bool mouseDown;
        private Point lastLocation;
        private string mbPath;
        private bool serverRunning = false;
        private int bannerlordId = -1;


        [DllImport("User32")]
        private static extern int ShowWindow(IntPtr hwnd, int nCmdShow);
       

        public BannerlordCoopLauncher()
        {
            InitializeComponent();
            // find mb path relative to where this exectuable should be
            this.mbPath = Path.GetFullPath($@"{System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase).Replace(@"file:\", "")}\..\..\..\..\");

            // try to read bannerlord pid started by this tool
            try
            {
                bannerlordId = Int32.Parse(File.ReadAllLines("pid")[0]);
            }
            catch (IndexOutOfRangeException)
            {
                bannerlordId = -1;
            }
            catch (IOException)
            {
                bannerlordId = -1;
            }


            if(bannerlordId != -1)
            {
                HostGame.Text = "STOP SERVER";
                serverRunning = true;
                this.Invalidate();
            }


            this.mbPath = @"E:\Created Content\MB170\";
        }

        private void DisableSafemode()
        {
            string configFile = $@"{System.Environment.GetEnvironmentVariable("USERPROFILE")}\Documents\Mount and Blade II Bannerlord\Configs\engine_config.txt";
            string[] lines = File.ReadAllLines(configFile);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] parts = lines[i].Split('=');
                if (parts[0].Trim().Equals("safely_exited"))
                {
                    if (parts[1].Trim().Equals("1"))
                    {
                        break;
                    }
                    else
                    {
                        lines[i] = $"{parts[0]}= 1";
                        File.WriteAllLines(configFile, lines);
                        break;
                    }
                    
                }
                
            }
            
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
            DisableSafemode();
            Process process = new Process();
            process.StartInfo.WorkingDirectory = this.mbPath + @"\bin\Win64_Shipping_Client\";
            process.StartInfo.CreateNoWindow = false;
            process.StartInfo.FileName = "\"" + mbPath + @"\bin\Win64_Shipping_Client\Bannerlord.exe" + "\"";
            process.StartInfo.Arguments = $"/singleplayer {additionalArgs} _MODULES_*Native*SandBoxCore*CustomBattle*SandBox*StoryMode*_MODULES_";
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            while (process.MainWindowHandle == IntPtr.Zero)
            {
                Thread.Sleep(100);
            }
            ShowWindow(process.MainWindowHandle, 0);
            bannerlordId = process.Id;
            serverRunning = true;
            this.Invalidate();
            File.WriteAllLines("pid", new string[] { bannerlordId.ToString() });
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
            if (!serverRunning)
            {
                StartGame("/server /headless");
                ((Button)sender).Text = "STOP SERVER";
               
            }
            else
            {
                Process.GetProcessById(bannerlordId).Kill();
                ((Button)sender).Text = "HOST GAME";
                serverRunning = false;
                File.Delete("pid");
                this.Invalidate();

            }
            
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

        private void BannerlordCoopLauncher_Load(object sender, EventArgs e)
        {
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.BannerlordCoopLauncher_Paint);
        }

        private void BannerlordCoopLauncher_Paint(object sender, System.Windows.Forms.PaintEventArgs e)
        {
            // Create a local version of the graphics object for the PictureBox.
            Graphics g = e.Graphics;
            System.Drawing.SolidBrush myBrush;
            if (serverRunning)
            {
                myBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Green);
            }
            else
            {
                myBrush = new System.Drawing.SolidBrush(System.Drawing.Color.Red);
            }
            g.FillEllipse(myBrush, new Rectangle(20, 320, 8, 8));
            myBrush.Dispose();
            g.Dispose();

        }
    }
}
