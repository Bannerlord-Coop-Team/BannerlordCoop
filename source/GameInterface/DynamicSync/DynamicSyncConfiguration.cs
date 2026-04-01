using System.IO;
using System.Reflection;

namespace GameInterface.DynamicSync
{
    public static class DynamicSyncConfiguration
    {
        public static string ExportPath => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\DynamicSyncExport";

        public static bool ExportFiles { get; } = true;

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
