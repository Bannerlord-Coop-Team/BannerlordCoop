using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;
using State = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest.ExtortionByDesertersQuestState;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters.Patches;

/// <summary>The server's unstarted quest listens through the dispatcher, in its owner's context.</summary>
[HarmonyPatch]
internal class ExtortionByDesertersWorldPatches
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.HourlyTick))]
    [HarmonyPostfix]
    private static void HourlyTickPostfix() => Run(quest => quest.HourlyTick());

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnWarDeclared))]
    [HarmonyPostfix]
    private static void WarPostfix(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail declareWarDetail) =>
        Run(quest => quest.OnWarDeclared(faction1, faction2, declareWarDetail));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnClanChangedKingdom))]
    [HarmonyPostfix]
    private static void ClanPostfix(Clan clan, Kingdom oldKingdom, Kingdom newKingdom,
        ChangeKingdomAction.ChangeKingdomActionDetail actionDetail, bool showNotification) =>
        Run(quest => quest.OnClanChangedKingdom(clan, oldKingdom, newKingdom, actionDetail, showNotification));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventStarted))]
    [HarmonyPostfix]
    private static void MapStartedPostfix(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty) =>
        Run(quest => quest.OnMapEventStarted(mapEvent, attackerParty, defenderParty));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnSettlementLeft))]
    [HarmonyPostfix]
    private static void LeftPostfix(MobileParty party, Settlement settlement) => Run(quest => quest.OnSettlementLeft(party, settlement));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnVillageBeingRaided))]
    [HarmonyPostfix]
    private static void RaidPostfix(Village village) => Run(quest => quest.OnVillageBeingRaided(village));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventEnded))]
    [HarmonyPostfix]
    private static void MapEndedPostfix(MapEvent mapEvent)
    {
        if (!mapEvent.InvolvedParties.Any(party => party.MobileParty?.IsCurrentlyUsedByAQuest == true)) return;
        Run(quest => quest.MapEventEnded(mapEvent));
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.MapEventEnded))]
    [HarmonyPrefix]
    private static bool MapEventEndedPrefix(Quest __instance, MapEvent mapEvent)
    {
        var deserters = __instance._deserterMobileParty;
        if (ModInformation.IsClient || deserters == null || !mapEvent.InvolvedParties.Contains(deserters.Party)) return false;

        var owner = MobileParty.MainParty.Party;
        if (!mapEvent.InvolvedParties.Contains(owner))
        {
            // Another player's victory must not leave a quest pointing at a destroyed party.
            if (mapEvent.WinningSide != BattleSideEnum.None && mapEvent.WinningSide != deserters.Party.Side)
                __instance.CompleteQuestWithCancel();
            return false;
        }

        __instance._deserterBattleFinalizedForTheFirstTime = true;
        if (mapEvent.WinningSide == owner.Side)
        {
            __instance._currentState = State.DesertersAreDefeated;
            __instance.AddLog(__instance.OnDeserterPartyDefeatedLogText);
        }
        else
        {
            SetPartyAiAction.GetActionForRaidingSettlement(deserters, __instance.QuestSettlement,
                MobileParty.NavigationType.Default, false, false);
            __instance._currentState = State.DesertersDefeatedPlayer;
        }
        Sync(__instance);
        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnVillageBeingRaided))]
    [HarmonyPrefix]
    private static bool RaidPrefix(Quest __instance, Village village)
    {
        if (ModInformation.IsClient || village.Settlement != __instance.QuestSettlement) return false;
        var attacker = village.Settlement.Party.MapEvent?.AttackerSide.LeaderParty;
        if (attacker == null) return false;
        if (attacker == __instance._deserterMobileParty?.Party)
        {
            if (MobileParty.MainParty.CurrentSettlement == village.Settlement) return false;
            __instance.ApplyQuestResult(__instance._questResultFail2);
            __instance.CompleteQuestWithFail(__instance.OnQuestFailed2LogText);
        }
        else if (attacker == MobileParty.MainParty.Party)
        {
            __instance.ApplyQuestResult(__instance._questResultFail3);
            __instance.CompleteQuestWithFail(__instance.OnQuestFailed3LogText);
        }
        else
        {
            __instance.CompleteQuestWithCancel(__instance.OnQuestCancel2LogText);
        }
        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.TickDesertersPartyLogic))]
    [HarmonyPrefix]
    private static bool TickPrefix(Quest __instance)
    {
        if (ModInformation.IsClient || !__instance.IsOngoing) return false;
        if (__instance._currentState == State.DesertersAreDefeated) return false;
        var deserters = __instance._deserterMobileParty;
        if (deserters == null || !deserters.IsActive)
        {
            __instance.CompleteQuestWithCancel();
            return false;
        }
        if (deserters.MapEvent != null)
        {
            // The server's unstarted quest cannot depend on a client BattleStarted listener.
            if (ContainerProvider.TryResolve<ExtortionByDesertersFinalizationProofStrategy>(out var proof))
                proof.TryRunBranch(__instance, (byte)ExtortionByDesertersBranch.Ambush);
            return false;
        }

        switch (__instance._currentState)
        {
            case State.DesertersMovingToSettlement:
                if (MobileParty.MainParty.CurrentSettlement == __instance.QuestSettlement)
                {
                    SetPartyAiAction.GetActionForRaidingSettlement(deserters, __instance.QuestSettlement,
                        MobileParty.NavigationType.Default, false, false);
                }
                else if (deserters.Position.Distance(MobileParty.MainParty.Position) <= deserters.SeeingRange * 0.8f)
                {
                    __instance.HandleDesertersRunningAway();
                    __instance._currentState = State.DesertersRunningAwayFromPlayer;
                    __instance._desertersRunAwayTimeoutTime = CampaignTime.HoursFromNow(10f);
                    Sync(__instance, __instance.OnDesertersNoticedPlayerNotificationText.ToString());
                }
                break;
            case State.DesertersRunningAwayFromPlayer:
                if (deserters.Position.Distance(MobileParty.MainParty.Position) > MobileParty.MainParty.SeeingRange + 3f ||
                    __instance._desertersRunAwayTimeoutTime.IsPast)
                {
                    if (__instance._desertersRunAwayTimeoutTime.IsPast) __instance.DestroyDeserterParty();
                    __instance.ApplyQuestResult(__instance._questResultFail1);
                    __instance.CompleteQuestWithFail(__instance.OnQuestFailed1LogText);
                }
                else __instance.HandleDesertersRunningAway();
                break;
            case State.DesertersDefeatedPlayer:
                SetPartyAiAction.GetActionForRaidingSettlement(deserters, __instance.QuestSettlement,
                    MobileParty.NavigationType.Default, false, false);
                break;
        }
        return false;
    }

    private static void Run(Action<Quest> listener)
    {
        if (ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher)) dispatcher.RunAsOwners(listener);
    }

    private static void Sync(Quest quest, string log = "")
    {
        if (ContainerProvider.TryResolve<ExtortionByDesertersSync>(out var sync)) sync.Send(quest, log);
    }
}
