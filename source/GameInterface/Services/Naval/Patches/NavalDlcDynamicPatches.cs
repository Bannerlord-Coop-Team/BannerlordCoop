using HarmonyLib;
using System;
using System.Linq;

namespace GameInterface.Services.Naval.Patches
{
    public static class NavalDlcDynamicPatches
    {
        private static bool applied;

        public static void Apply()
        {
            if (applied) return;
            applied = true;

            var harmony = new Harmony("coop.naval.dlc.dynamic");

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var navalAssemblies = assemblies.Where(a =>
            {
                try { return a.GetName().Name.IndexOf("NavalDLC", StringComparison.OrdinalIgnoreCase) >= 0; }
                catch { return false; }
            }).ToArray();

            var visualFactoryType = navalAssemblies
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
                })
                .FirstOrDefault(t => t.Name == "VisualShipFactory");

            if (visualFactoryType != null)
            {
                var createMethod = AccessTools.Method(visualFactoryType, "CreateVisualShip");
                if (createMethod != null)
                {
                    var prefix = new HarmonyMethod(typeof(NavalDlcDynamicPatches).GetMethod(nameof(VisualShipFactory_Create_Prefix), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
                    harmony.Patch(createMethod, prefix: prefix);
                }
            }
        }

        private static bool VisualShipFactory_Create_Prefix()
        {
            if (global::GameInterface.ModInformation.IsServer)
            {
                if (global::GameInterface.Services.Naval.NavalRuntime.ClientCount <= 0)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
