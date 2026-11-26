using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.CultureObjects.Patches;

/// <summary>Repairs unresolved culture policy references before campaign startup can use them (#3337).</summary>
[HarmonyPatch(typeof(CultureObject), nameof(CultureObject.Deserialize))]
internal class CultureDefaultPolicyRepairPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<CultureDefaultPolicyRepairPatch>();

    // Run after deserialization because vanilla appends null for unregistered policy IDs.
    [HarmonyPostfix]
    private static void RepairCultureDefaults(CultureObject __instance)
    {
        if (!ContainerProvider.TryResolve<ICultureDefaultPolicyRepairer>(out var repairer)) return;

        try
        {
            repairer.Repair(__instance);
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Could not repair unresolved default policies for culture {CultureId}", __instance.StringId);
        }
    }
}
