using HarmonyLib;
using System.IO;
using System.Xml;
using TaleWorlds.Library;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// <see cref="ApplicationVersion.FromParametersFile"/> reads Parameters/Version.xml through the
    /// native virtual file system, which returns empty headless — so <c>MBSaveLoad.CurrentVersion</c>
    /// becomes <c>ApplicationVersion.Empty</c>. That taints saves we produce: on load
    /// <c>MBSaveLoad.LastLoadedGameVersion</c> is read back as Empty, the loader treats the save as
    /// pre-1.3.0, and per-object legacy migration (e.g. MobileParty.OnLateLoad) NREs reading members
    /// that don't exist in a current save.
    ///
    /// Resolve the real game version from the Native module's SubModule.xml instead. This runs before
    /// MBSaveLoad's static initialiser (patches are applied before the save driver is installed), so
    /// CurrentVersion picks up the correct value.
    /// </summary>
    [HarmonyPatch(typeof(ApplicationVersion), nameof(ApplicationVersion.FromParametersFile))]
    internal static class ApplicationVersionPatches
    {
        private static bool _resolved;
        private static ApplicationVersion _version;

        static bool Prefix(ref ApplicationVersion __result)
        {
            if (!_resolved)
            {
                _version = ReadNativeModuleVersion();
                _resolved = true;
            }
            __result = _version;
            return false;
        }

        private static ApplicationVersion ReadNativeModuleVersion()
        {
            try
            {
                string path = Path.Combine(BasePathPatches.GameRootPath.Replace('/', Path.DirectorySeparatorChar),
                    "Modules", "Native", "SubModule.xml");
                if (File.Exists(path))
                {
                    var doc = new XmlDocument();
                    doc.Load(path);
                    string value = doc.SelectSingleNode("//Version")?.Attributes?["value"]?.Value;
                    if (!string.IsNullOrEmpty(value))
                    {
                        return ApplicationVersion.FromString(value);
                    }
                }
            }
            catch
            {
                // fall through to a safe non-empty default
            }

            // Any version >= v1.3.0 avoids the legacy migration path.
            return ApplicationVersion.FromString("v1.4.5");
        }
    }
}
