using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.CultureObjects.Patches;

/// <summary>Repairs unresolved culture policy references before campaign startup can use them (#3337).</summary>
[HarmonyPatch(typeof(MBObjectManager), nameof(MBObjectManager.LoadXML))]
internal class CultureDefaultPolicyRepairPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<CultureDefaultPolicyRepairPatch>();

    [HarmonyPostfix]
    private static void RepairCultureDefaults(MBObjectManager __instance, string id)
    {
        if (id != CultureDefaultPolicyRepairer.CultureXmlId) return;
        if (!ContainerProvider.TryResolve<ICultureDefaultPolicyRepairer>(out var repairer)) return;

        // Vanilla registers the default policies before it loads SPCultures.
        try
        {
            repairer.Repair(__instance.GetObjectTypeList<CultureObject>());
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Could not repair unresolved culture default policies during campaign load");
        }
    }
}
