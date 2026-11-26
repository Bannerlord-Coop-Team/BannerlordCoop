using HarmonyLib;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.CultureObjects.Patches;

/// <summary>Repairs culture defaults before campaign startup can use them to initialize kingdoms.</summary>
[HarmonyPatch(typeof(MBObjectManager), nameof(MBObjectManager.LoadXML))]
internal class CultureDefaultPolicyRepairPatch
{
    [HarmonyPostfix]
    private static void RepairCultureDefaults(
        MBObjectManager __instance, string id, bool isDevelopment, string gameType, bool skipXmlFilterForEditor)
    {
        if (id != CultureDefaultPolicyRepairer.CultureXmlId) return;
        if (!ContainerProvider.TryResolve<ICultureDefaultPolicyRepairer>(out var repairer)) return;

        repairer.Repair(__instance, isDevelopment || skipXmlFilterForEditor, gameType);
    }
}
