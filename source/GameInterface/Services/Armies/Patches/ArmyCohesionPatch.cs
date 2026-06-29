using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Clans.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Armies.Patches;

[HarmonyPatch(typeof(Army))]
internal class ArmyCohesionPatch
{
    [HarmonyPatch(nameof(Army.HourlyTick))]
    [HarmonyPostfix]
    private static void Postfix_HourlyTick(Army __instance) => PublishCohesion(__instance);
    [HarmonyPatch(nameof(Army.BoostCohesionWithInfluence))]
    [HarmonyPostfix]
    private static void Postfix_BoostCohesionWithInfluence(Army __instance) => PublishCohesion(__instance);
    private static void PublishCohesion(Army army)
    {
        if (!ModInformation.IsServer) return;

        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (army == null) return;

        MessageBroker.Instance.Publish(army, new ArmyCohesionChanged(army, army.Cohesion));
    }
}
