using Common;
using GameInterface.Services.Villages.Data;
using GameInterface.Services.Villages.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(ForceVolunteersEventComponent), "OnBeforeFinalize")]
internal class ForceVolunteersEventComponentOnBeforeFinalizePatch
{
    [HarmonyPostfix]
    private static void Postfix(ForceVolunteersEventComponent __instance)
    {
        if (ModInformation.IsClient) return;
        if (!ContainerProvider.TryResolve<IVillageHostileActionInterface>(out var hostileActionInterface)) return;

        hostileActionInterface.ApplyForceActionOutcome(__instance.MapEvent, VillageHostileAction.ForceVolunteers);
    }
}

[HarmonyPatch(typeof(ForceSuppliesEventComponent), "OnBeforeFinalize")]
internal class ForceSuppliesEventComponentOnBeforeFinalizePatch
{
    [HarmonyPostfix]
    private static void Postfix(ForceSuppliesEventComponent __instance)
    {
        if (ModInformation.IsClient) return;
        if (!ContainerProvider.TryResolve<IVillageHostileActionInterface>(out var hostileActionInterface)) return;

        hostileActionInterface.ApplyForceActionOutcome(__instance.MapEvent, VillageHostileAction.ForceSupplies);
    }
}