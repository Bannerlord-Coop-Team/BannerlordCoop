using System;
using System.Windows.Forms;

namespace BannerlordCoopInstaller
{
    static class InstallerMain
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new BannerlordCoopInstallerForm());
        }
    }
}
