using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.Win32;

namespace BannerlordCoopInstaller
{
    public partial class BannerlordCoopInstallerForm : Form
    {

        private Dictionary<string, ResourceFile> resourceDict;


        /// <summary>
        /// Check if MB is installed (through the registry) - Only works in x64.
        /// </summary>
        /// <returns>Full path where MB is installed</returns>
        public string GetBannerLordInstalledPath()
        {

            string registry_key = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            using (Microsoft.Win32.RegistryKey key = Registry.LocalMachine.OpenSubKey(registry_key))
            {
                foreach (string subkey_name in key.GetSubKeyNames())
                {
                    using (RegistryKey subkey = key.OpenSubKey(subkey_name))
                    {
                        string name = (string)subkey.GetValue("DisplayName");
                        if (!string.IsNullOrEmpty(name) && name.Equals("Mount & Blade II: Bannerlord"))
                        {
                            return subkey.GetValue("installlocation").ToString();
                        }
                    }
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// Check the BannerlordPath and ensure and executable exists. If not, disable the install button
        /// </summary>
        private void CheckBannerLordPath()
        {
            if (File.Exists(this.BannerlordPath.Text + "\\bin\\Win64_Shipping_Client\\Bannerlord.exe"))
            {
                this.Install.Enabled = true;
                this.Status.Text = "Ready to install";
            }
            else
            {
                this.Status.Text = "Select a Mount && Blade II: Bannerlord install path to continue";
                this.Install.Enabled = false;
            }
        }



        /// <summary>
        /// Initialize the form; create a map of the objects stored as resources in the executable; include the path where it should be installed and extension.
        /// </summary>
        public BannerlordCoopInstallerForm()
        {
            InitializeComponent();
            this.BannerlordPath.Text = GetBannerLordInstalledPath();
            CheckBannerLordPath();
            this.resourceDict = new Dictionary<string, ResourceFile> {
                {"SubModule", new ResourceFile("xml", @"\") },
                { "CoopConnectionUIMovie", new ResourceFile( "xml", @"\GUI\Prefabs")},
                { "Launcher", new ResourceFile("exe", @"\bin\Win64_Shipping_Client") },
                { "bannerlord_icon", new ResourceFile("ico", @"\") }
            };


        }



        /// <summary>
        /// Allow user to click the browse button, open a prompt to select a different path.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PathBrowse_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog1 = new FolderBrowserDialog();
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                this.BannerlordPath.Text = folderBrowserDialog1.SelectedPath;
                CheckBannerLordPath();

            }
        }

        /// <summary>
        /// Create necessary directories, retrieve resources, loop through and extract each file. Special case for the Launcer exectuable, CoopConnectionUIMovie, and icon.
        /// </summary>
        private void PerformInstall()
        {
            Directory.CreateDirectory($@"{this.BannerlordPath.Text}\Modules\Coop\bin\Win64_Shipping_Client");
            Directory.CreateDirectory($@"{this.BannerlordPath.Text}\Modules\Coop\GUI\Prefabs");
            Assembly assembly = Assembly.GetExecutingAssembly();
            string[] names = assembly.GetManifestResourceNames();
            ResourceSet set = new ResourceSet(assembly.GetManifestResourceStream(names[1]));
            this.InstallProgress.Value = 0;
            foreach (DictionaryEntry resource in set)
            {
                if (resourceDict.ContainsKey(resource.Key.ToString()))
                {
                    ResourceFile resourceFile = resourceDict[resource.Key.ToString()];
                    string fileName = $"{resource.Key}.{resourceFile.FileExtension}";
                    this.Status.Text = "Copying " + fileName + "...";
                    if (resourceFile.FileExtension.Equals("exe"))
                    {
                        File.WriteAllBytes($@"{this.BannerlordPath.Text}\Modules\Coop{resourceFile.InstallPath}\{fileName}", (byte[])resource.Value);
                    }
                    else if (resourceFile.FileExtension.Equals("ico"))
                    {

                        Icon icon = (Icon)resource.Value;
                        using (FileStream fs = new FileStream($@"{this.BannerlordPath.Text}\Modules\Coop{resourceFile.InstallPath}\{fileName}", FileMode.Create))
                            icon.Save(fs);
                    }
                    else
                    {
                        File.WriteAllText($@"{this.BannerlordPath.Text}\Modules\Coop{resourceFile.InstallPath}\{fileName}", resource.Value.ToString());
                    }

                }
                else
                {
                    string fileName = resource.Key + ".dll";
                    this.Status.Text = "Copying " + fileName + "...";
                    File.WriteAllBytes($@"{this.BannerlordPath.Text}\Modules\Coop\bin\Win64_Shipping_Client\{fileName}", (byte[])resource.Value);
                }

            }
        }

        /// <summary>
        /// Disable controls, check if a version of the mod is installed; ask the user to delete it. Install the mod and change install button to finish
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Install_Click(object sender, EventArgs e)
        {
            this.BannerlordPath.Enabled = false;
            this.ShortcutCheckbox.Enabled = false;
            this.PathBrowse.Enabled = false;

            // version exists, ask to delete it
            if (Directory.Exists($@"{this.BannerlordPath.Text}\Modules\Coop"))
            {
                DialogResult dialogResult = MessageBox.Show("An older version exists and must be removed. Are you sure?", "Older Version Detected", MessageBoxButtons.YesNo);
                if (dialogResult == DialogResult.Yes)
                {
                    try
                    {
                        //recursively delete the mod
                        Directory.Delete($@"{this.BannerlordPath.Text}\Modules\Coop", true);
                    }
                    catch (IOException)
                    {
                        //renable the UI for other controls and notify that mod cannot be fully deleted
                        this.Status.Text = "Ensure all files and processes are closed before reinstalling.";
                        this.BannerlordPath.Enabled = true;
                        this.ShortcutCheckbox.Enabled = true;
                        this.PathBrowse.Enabled = true;
                        this.Install.Enabled = true;
                        return;
                    }

                }
                else
                {
                    // renable the UI and allow changes to selections, return control
                    this.BannerlordPath.Enabled = true;
                    this.ShortcutCheckbox.Enabled = true;
                    this.PathBrowse.Enabled = true;
                    this.Install.Enabled = true;
                    return;
                }
            }

            // perform installation
            PerformInstall();





            // creating a shortcut
            if (this.ShortcutCheckbox.Checked)
            {
                IShellLink link = (IShellLink)new ShellLink();
                link.SetDescription("Mount && Blade II Coop Launcher");
                link.SetPath($@"{this.BannerlordPath.Text}\Modules\Coop\bin\Win64_Shipping_Client\Launcher.exe");
                link.SetIconLocation($@"{this.BannerlordPath.Text}\Modules\Coop\bannerlord_icon.ico", 0);
                IPersistFile file = (IPersistFile)link;
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                file.Save(Path.Combine(desktopPath, "Bannerlord Coop.lnk"), false);
            }
            this.InstallProgress.Value = 100;
            this.Status.Text = "Complete";
            this.Install.Text = "Finish";

            //change install button to finish button and set its click to close the window.
            this.Install.Click -= new EventHandler(Install_Click);
            this.Install.Click += new EventHandler(delegate (Object o, EventArgs a)
            {
                Close();
            });
            this.Install.Enabled = true;


        }


        /// <summary>
        /// With every change in Path, check if bannerlord exists in the path
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BannerLordPath_TextChanged(object sender, EventArgs e)
        {
            CheckBannerLordPath();
        }

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        internal class ShellLink
        {
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        internal interface IShellLink
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

    }
}
