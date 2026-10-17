using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;
using Issue = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssue;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush.Patches;

[HarmonyPatch]
internal class CaravanAmbushEventPatches
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.HourlyTick))]
    [HarmonyPostfix]
    private static void Hourly() => Run(quest =>
    {
        if (quest.QuestDueTime.IsPast) quest.CompleteQuestWithTimeOut();
        else quest.HourlyTick();
    });

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventStarted))]
    [HarmonyPostfix]
    private static void BattleStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty) =>
        Run(quest => quest.MapEventStarted(mapEvent, attackerParty, defenderParty));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventEnded))]
    [HarmonyPostfix]
    private static void BattleEnded(MapEvent mapEvent)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<ICaravanAmbushEvents>(out var events))
            events.Run(quest => events.BattleEnded(quest, mapEvent));
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnBanditPartyRecruited))]
    [HarmonyPostfix]
    private static void Recruited(MobileParty banditParty) => Run(quest =>
    {
        if (quest._banditParty == banditParty) quest.OnBanditPartyRecruited(banditParty);
    });

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnSettlementEntered))]
    [HarmonyPostfix]
    private static void Arrived(MobileParty party, Settlement settlement, Hero hero) => Run(quest =>
    {
        if (party == null || quest._caravanParty != party || quest._isCaravanSaved) return;
        quest.HandlePartyAiAfterCompletion();
        DestroyPartyAction.Apply(party.Party, party);
        quest._caravanParty = null;
        quest.CompleteQuestWithCancel();
    });

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnWarDeclared))]
    [HarmonyPostfix]
    private static void War(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail declareWarDetail) =>
        Run(quest => quest.OnWarDeclared(faction1, faction2, declareWarDetail));

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnClanChangedKingdom))]
    [HarmonyPostfix]
    private static void Kingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom,
        ChangeKingdomAction.ChangeKingdomActionDetail actionDetail, bool showNotification) =>
        Run(quest => quest.OnClanChangedKingdom(clan, oldKingdom, newKingdom, actionDetail, showNotification));

    // The dedicated server has no player clan; the accept preconditions check war as the requesting player.
    [HarmonyPatch(typeof(Issue), nameof(Issue.IssueStayAliveConditions))]
    [HarmonyPrefix]
    private static bool StayAlive(Issue __instance, ref bool __result)
    {
        __result = __instance.IssueOwner != null && __instance.IssueOwner.OwnedCaravans.Count > 0;
        return false;
    }

    private static void Run(Action<Quest> action)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<ICaravanAmbushEvents>(out var events)) events.Run(action);
    }
}
