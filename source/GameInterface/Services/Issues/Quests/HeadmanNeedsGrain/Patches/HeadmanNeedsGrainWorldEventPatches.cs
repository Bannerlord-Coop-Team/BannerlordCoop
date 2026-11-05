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
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
internal class HeadmanNeedsGrainWorldEventPatches
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnWarDeclared))]
    [HarmonyPostfix, HarmonyPriority(Priority.First)]
    private static void CrimeWarPostfix(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail declareWarDetail)
    {
        if (declareWarDetail != DeclareWarAction.DeclareWarDetail.CausedByCrimeRatingChange) return;

        RunAsOwners(quest =>
        {
            if (declareWarDetail == DeclareWarAction.DeclareWarDetail.CausedByCrimeRatingChange &&
                ((faction1 == Hero.MainHero.MapFaction && faction2 == quest.QuestGiver.MapFaction) ||
                 (faction2 == Hero.MainHero.MapFaction && faction1 == quest.QuestGiver.MapFaction))) return;
            QuestHelper.CheckWarDeclarationAndFailOrCancelTheQuest(quest, faction1, faction2, declareWarDetail,
                quest.PlayerDeclaredWarQuestLogText, quest.CancelLogOnWarDeclared, forceCancel: true);
        });
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnVillageBeingRaided))]
    [HarmonyPostfix]
    private static void RaidPostfix(Village village) => RunAsOwners(quest => quest.OnVillageBeingRaided(village));

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnHeroPrisonerTaken))]
    [HarmonyPrefix]
    private static bool PrisonerTakenPrefix() => ModInformation.IsClient;
    [HarmonyPatch(typeof(Quest), nameof(Quest.OnWarDeclared))]
    [HarmonyPrefix]
    private static bool WarPrefix(Quest __instance, IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
    {
        if (ModInformation.IsClient) return false;
        // The dedicated host has no player hero. The framework relay invokes this
        // listener again inside each owner's substitution scope.
        if (Hero.MainHero == null) return false;
        if (declareWarDetail == DeclareWarAction.DeclareWarDetail.CausedByCrimeRatingChange &&
            !((faction1 == Hero.MainHero.MapFaction && faction2 == __instance.QuestGiver.MapFaction) ||
            (faction2 == Hero.MainHero.MapFaction && faction1 == __instance.QuestGiver.MapFaction))) return false;
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
    private static bool KingdomPrefix() => ModInformation.IsServer && Hero.MainHero != null;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnVillageBeingRaided))]
    [HarmonyPrefix]
    private static bool RaidPrefix(Quest __instance, Village village)
    {
        if (ModInformation.IsClient) return false;
        if (Hero.MainHero == null) return false;
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
