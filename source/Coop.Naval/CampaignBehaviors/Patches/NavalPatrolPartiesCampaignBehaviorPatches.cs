using Common;
using Common.Messaging;
using Coop.Naval.CampaignBehaviors.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Naval.CampaignBehaviors.Patches;

[HarmonyPatch(typeof(NavalPatrolPartiesCampaignBehavior))]
[HarmonyPatchCategory(GameInterface.GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class NavalPatrolPartiesCampaignBehaviorPatches
{
    // The daily tick queues, spawns and removes patrols, so its before and after state is compared instead of
    // patching UpdateSettlementQueue, which is small enough to be inlined
    [HarmonyPatch(nameof(NavalPatrolPartiesCampaignBehavior.DailyTickSettlement))]
    [HarmonyPrefix]
    public static bool DailyTickSettlementPrefix(NavalPatrolPartiesCampaignBehavior __instance, Settlement settlement, out NavalPatrolSettlementChanged __state)
    {
        __state = default;

        if (ModInformation.IsClient) return false;

        __state = GetSettlementState(__instance, settlement);
        return true;
    }

    [HarmonyPatch(nameof(NavalPatrolPartiesCampaignBehavior.DailyTickSettlement))]
    [HarmonyPostfix]
    public static void DailyTickSettlementPostfix(NavalPatrolPartiesCampaignBehavior __instance, Settlement settlement, NavalPatrolSettlementChanged __state)
    {
        if (ModInformation.IsClient) return;

        var state = GetSettlementState(__instance, settlement);
        if (state.IsQueued == __state.IsQueued && state.QueueTime == __state.QueueTime && state.PatrolParty == __state.PatrolParty) return;

        MessageBroker.Instance.Publish(__instance, state);
    }

    [HarmonyPatch(nameof(NavalPatrolPartiesCampaignBehavior.RemoveSettlementParties))]
    [HarmonyPrefix]
    public static bool RemoveSettlementPartiesPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(NavalPatrolPartiesCampaignBehavior.SettlementEntered))]
    [HarmonyPrefix]
    public static bool SettlementEnteredPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(NavalPatrolPartiesCampaignBehavior.AiHourlyTick))]
    [HarmonyPrefix]
    public static bool AiHourlyTickPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(NavalPatrolPartiesCampaignBehavior.OnSettlementLeft))]
    [HarmonyPrefix]
    public static bool OnSettlementLeftPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(NavalPatrolPartiesCampaignBehavior.OnMobilePartyDestroyed))]
    [HarmonyPrefix]
    public static bool OnMobilePartyDestroyedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(nameof(NavalPatrolPartiesCampaignBehavior.OnMobilePartyDestroyed))]
    [HarmonyPostfix]
    public static void OnMobilePartyDestroyedPostfix(NavalPatrolPartiesCampaignBehavior __instance, MobileParty party)
    {
        if (ModInformation.IsClient) return;
        if (!party.IsPatrolParty || !party.PatrolPartyComponent.IsNaval) return;

        MessageBroker.Instance.Publish(__instance, GetSettlementState(__instance, party.HomeSettlement));
    }

    internal static NavalPatrolSettlementChanged GetSettlementState(NavalPatrolPartiesCampaignBehavior behavior, Settlement settlement)
    {
        var isQueued = behavior._partyGenerationQueue.TryGetValue(settlement, out var queueTime);

        return new NavalPatrolSettlementChanged(settlement, isQueued, queueTime, behavior.GetNavalPatrolParty(settlement));
    }
}
