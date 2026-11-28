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

    // Vanilla removes the mercenaries from the main party, so it only runs on the server inside the owner's scope while the owner still has a party
    [HarmonyPatch(typeof(Quest), nameof(Quest.OnFinalize))]
    [HarmonyPrefix]
    private static bool FinalizePrefix() => ModInformation.IsServer && MobileParty.MainParty != null;

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

    // A captive owner has no mercenaries left, and vanilla's count check would then reopen the company menu over the prisoner menu every hour
    [HarmonyPatch(typeof(Quest), nameof(Quest.HourlyTick))]
    [HarmonyPrefix]
    private static bool HourlyTickPrefix(Quest __instance)
    {
        if (ModInformation.IsServer)
        {
            return true;
        }

        return !Hero.MainHero.IsPrisoner && MobileParty.MainParty.MemberRoster.Contains(__instance._troubleCharacterObject);
    }

    private static void Run(Action<ILandLordCompanyOfTroubleService> action)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<ILandLordCompanyOfTroubleService>(out var service))
        {
            action(service);
        }
    }
}
