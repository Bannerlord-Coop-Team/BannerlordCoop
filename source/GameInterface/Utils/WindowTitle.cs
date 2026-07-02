using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using TaleWorlds.Engine;

namespace GameInterface.Utils
{
    /// <summary>
    /// Sets the game process's OS window title by co-op role so the server and each client are
    /// distinguishable on the Windows taskbar when several instances are launched together for testing.
    /// </summary>
    public static class WindowTitle
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SetWindowText(IntPtr hWnd, string lpString);

        private const string TestClientPrefix = "testclient";

        public static string LabelFor(bool isServer, string platformId)
        {
            if (isServer) return "Coop Server";

            if (string.IsNullOrEmpty(platformId)) return "Coop Client";

            // Launch configs pass "/platformId testclient1|testclient2"; show just the trailing number.
            // DebugAutoConnect passes a bare "testclient" for the first client, which strips to empty.
            if (platformId.StartsWith(TestClientPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string number = platformId.Substring(TestClientPrefix.Length);
                return string.IsNullOrEmpty(number) ? "Coop Client" : "Coop Client " + number;
            }

            return "Coop Client " + platformId;
        }

        public static void Apply(bool isServer)
        {
            try
            {
                using (Process process = Process.GetCurrentProcess())
                {
                    IntPtr handle = process.MainWindowHandle;
                    if (handle == IntPtr.Zero) return;

                    SetWindowText(handle, LabelFor(isServer, ReadPlatformId()));
                }
            }
            catch (Exception)
            {
                // Best effort — the window title is a cosmetic testing aid.
            }
        }

        private static string ReadPlatformId()
        {
            string[] args = Utilities.GetFullCommandLineString().Split(' ');

            int index = Array.FindIndex(args, a => a.Equals("/platformId", StringComparison.OrdinalIgnoreCase));

            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
