using Common;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssueQuest;

namespace GameInterface.Services.Issues.Quests.NearbyBanditBase.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class NearbyBanditBaseQuestEventPatches
{
    [ThreadStatic]
    private static Hero warInitiator;

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventEnded))]
    [HarmonyPostfix]
    private static void MapEventEndedPostfix(MapEvent mapEvent)
    {
        RunAsOwners(quest =>
        {
            var owner = Hero.MainHero;

            // an owner without a party would be checked against the party of whoever ended the battle, so their branches are decided here
            if (owner?.PartyBelongedTo == null)
            {
                if (mapEvent.MapEventSettlement != quest._targetHideout)
                {
                    return;
                }

                if (WasCapturedLosingThisAssault(owner, mapEvent))
                {
                    quest.OnQuestFailed(isTimedOut: false);
                }
                else if (mapEvent.BattleState == BattleState.AttackerVictory)
                {
                    quest.OnQuestCanceled();
                }

                return;
            }

            quest.OnMapEventEnded(mapEvent);
        });
    }

    // taken prisoner during this battle by the defenders who beat the assault, not merely held by someone who fought it
    private static bool WasCapturedLosingThisAssault(Hero owner, MapEvent mapEvent)
    {
        var captor = owner?.IsPrisoner == true ? owner.PartyBelongedToAsPrisoner : null;

        return captor?.MapEvent == mapEvent &&
            captor.Side == BattleSideEnum.Defender &&
            mapEvent.DefeatedSide == BattleSideEnum.Attacker &&
            owner.CaptivityStartTime >= mapEvent.BattleStartTime;
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHideoutDeactivated))]
    [HarmonyPostfix]
    private static void HideoutDeactivatedPostfix(Settlement hideout) => RunAsOwners(quest => quest.OnHideoutCleared(hideout));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnWarDeclared))]
    [HarmonyPrefix]
    private static void CaptureWarInitiator(out Hero __state)
    {
        __state = warInitiator;
        warInitiator = ResolvedMainHeroContext.ResolvedMainHero;
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnWarDeclared))]
    [HarmonyFinalizer]
    private static void RestoreWarInitiator(Hero __state) => warInitiator = __state;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnMapEventEnded))]
    [HarmonyPrefix]
    private static bool MapEventEndedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnHideoutCleared))]
    [HarmonyPrefix]
    private static bool HideoutClearedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnMapEventStarted))]
    [HarmonyPrefix]
    private static bool MapEventStartedPrefix() => ModInformation.IsServer;

    // vanilla blames the owner for any hostility war, so only the one the owner started fails the quest
    [HarmonyPatch(typeof(Quest), nameof(Quest.OnWarDeclared))]
    [HarmonyPrefix]
    private static bool WarDeclaredPrefix(DeclareWarAction.DeclareWarDetail detail)
    {
        if (ModInformation.IsClient)
        {
            return false;
        }

        return detail != DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility || warInitiator == Hero.MainHero;
    }

    // the server fails the quest when it times out, a client replaying the timeout only ends its copy
    [HarmonyPatch(typeof(Quest), nameof(Quest.OnTimedOut))]
    [HarmonyPrefix]
    private static bool TimedOutPrefix() => ModInformation.IsServer;

    private static void RunAsOwners(Action<Quest> listener)
    {
        if (ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            dispatcher.RunAsOwners(listener);
        }
    }
}
