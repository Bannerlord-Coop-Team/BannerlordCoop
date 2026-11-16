using Common;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest;

namespace GameInterface.Services.Issues.Quests.LandLordCompanyOfTrouble.Patches;

[HarmonyPatch]
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
internal static class LandLordCompanyOfTroubleWorldEventPatches
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventEnded))]
    [HarmonyPostfix]
    private static void MapEventEnded(MapEvent mapEvent) => Run(service => service.OnMapEventEnded(mapEvent));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMobilePartyDestroyed))]
    [HarmonyPostfix]
    private static void MobilePartyDestroyed(MobileParty mobileParty, PartyBase destroyerParty) =>
        Run(service => service.OnMobilePartyDestroyed(mobileParty, destroyerParty));

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnFinalize))]
    [HarmonyPostfix]
    private static void FinalizePostfix(Quest __instance) => Run(service => service.RemoveCompanyOf(__instance));

    // Vanilla's IssueFinalized drops the quest without finalizing it, which would leave the mercenaries and the hostile company behind
    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
    [HarmonyPrefix]
    private static void IssueFinalizedPrefix(IssueBase __instance) => Run(service => service.FinalizeIssue(__instance));

    // The server ends the quest after a battle, a client's copy would pay the owner or end the quest a second time
    [HarmonyPatch(typeof(Quest), nameof(Quest.OnMapEventEnded))]
    [HarmonyPrefix]
    private static bool MapEventEndedPrefix() => ModInformation.IsServer;

    // The quest relay runs the coercion consequences on the server as the owner, a client copy would apply them twice
    [HarmonyPatch(typeof(Quest), nameof(Quest.OnMapEventStarted))]
    [HarmonyPrefix]
    private static bool MapEventStartedPrefix() => ModInformation.IsServer;

    // Vanilla removes the mercenaries from the main party here, the server removes them from the owner's party when it has one
    [HarmonyPatch(typeof(Quest), nameof(Quest.OnFinalize))]
    [HarmonyPrefix]
    private static bool FinalizePrefix() => ModInformation.IsServer && MobileParty.MainParty != null;

    private static void Run(Action<ILandLordCompanyOfTroubleService> action)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<ILandLordCompanyOfTroubleService>(out var service))
        {
            action(service);
        }
    }
}
