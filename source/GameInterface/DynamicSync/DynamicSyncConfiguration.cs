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

            var targetDirectory = Path.GetDirectoryName(Path.Combine(ExportPath, targetPath));
            if (!Directory.Exists(targetDirectory))
                Directory.CreateDirectory(targetDirectory);

            File.WriteAllText(Path.Combine(ExportPath, targetPath), content);
        }
    }
}
