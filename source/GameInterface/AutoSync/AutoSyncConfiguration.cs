using System.IO;
using System.Reflection;

namespace GameInterface.AutoSync
{
    public static class AutoSyncConfiguration
    {
        public static string ExportPath => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\AutoSyncExport";

        // Export is a debug inspection aid only, the generated assembly is compiled and loaded in memory.
#if DEBUG
        public static bool ExportFiles { get; set; } = true;
#else
        public static bool ExportFiles { get; set; } = false;
#endif

        public static bool Enabled { get; } = true;


        public static void ExportFile(string targetPath, string content)
        {
            if (!ExportFiles)
                return;

            var fullPath = Path.Combine(ExportPath, targetPath);
            var targetDirectory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(targetDirectory))
                Directory.CreateDirectory(targetDirectory);

            try
            {
                File.WriteAllText(fullPath, content);
            }
            catch (IOException)
            {
                // Another process (e.g. server in DebugAutoConnect) is writing the same file
                // simultaneously. Both processes generate identical content, so it's safe to
                // skip — the file will be written by whichever process wins the race.
            }
        }
    }
}
