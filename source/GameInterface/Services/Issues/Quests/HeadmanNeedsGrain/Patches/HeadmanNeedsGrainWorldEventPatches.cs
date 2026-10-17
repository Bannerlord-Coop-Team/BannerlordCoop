using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using Helpers;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssueQuest;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain.Patches;

[HarmonyPatch]
internal class HeadmanNeedsGrainWorldEventPatches
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnWarDeclared))]
    [HarmonyPostfix]
    private static void WarPostfix(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail declareWarDetail)
    {
        RunAsOwners(quest => quest.OnWarDeclared(faction1, faction2, declareWarDetail));
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnClanChangedKingdom))]
    [HarmonyPostfix]
    private static void KingdomPostfix(Clan clan, Kingdom oldKingdom, Kingdom newKingdom,
        ChangeKingdomAction.ChangeKingdomActionDetail actionDetail, bool showNotification)
    {
        RunAsOwners(quest => quest.OnClanChangedKingdom(clan, oldKingdom, newKingdom, actionDetail, showNotification));
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnVillageBeingRaided))]
    [HarmonyPostfix]
    private static void RaidPostfix(Village village) => RunAsOwners(quest => quest.OnVillageBeingRaided(village));

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnWarDeclared))]
    [HarmonyPrefix]
    private static bool WarPrefix(Quest __instance, IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
    {
        if (ModInformation.IsClient) return false;
        var mapEvent = Hero.MainHero?.PartyBelongedTo?.MapEvent;
        if (detail == DeclareWarAction.DeclareWarDetail.CausedByCrimeRatingChange ||
            (detail == DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility && mapEvent?.IsRaid == true &&
            mapEvent.MapEventSettlement.MapFaction == __instance.QuestGiver.MapFaction &&
            __instance.QuestGiver.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction)))
        {
            __instance.CriminalRatingFail();
        }
        else
        {
            QuestHelper.CheckWarDeclarationAndFailOrCancelTheQuest(__instance, faction1, faction2, detail,
                __instance.PlayerDeclaredWarQuestLogText, __instance.CancelLogOnWarDeclared, forceCancel: true);
        }
        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnClanChangedKingdom))]
    [HarmonyPrefix]
    private static bool KingdomPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnVillageBeingRaided))]
    [HarmonyPrefix]
    private static bool RaidPrefix(Quest __instance, Village village)
    {
        if (ModInformation.IsClient) return false;
        var party = Hero.MainHero?.PartyBelongedTo;
        var ownerIsRaiding = party?.MapEvent?.IsRaid == true && party.CurrentSettlement == village.Settlement;
        if (village == __instance.QuestGiver.CurrentSettlement.Village && !ownerIsRaiding)
            __instance.CompleteQuestWithCancel(__instance.CancelLogOnVillageRaided);
        return false;
    }

    private static void RunAsOwners(Action<Quest> listener)
    {
        if (ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher)) dispatcher.RunAsOwners(listener);
    }
}
