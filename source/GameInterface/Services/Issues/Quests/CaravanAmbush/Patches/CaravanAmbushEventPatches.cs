using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class CaravanAmbushEventPatches
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.HourlyTick))]
    [HarmonyPostfix]
    private static void Hourly() => Run(quest =>
    {
        if (MobileParty.MainParty != null)
        {
            quest.HourlyTick();
            return;
        }
        // A captured owner has no party, but an active ambush must still lose invulnerability after six hours.
        var battle = quest._caravanParty?.MapEvent;
        if (battle != null && battle.IsInvulnerable && battle.BattleStartTime.ElapsedHoursUntilNow > 6f)
            battle.IsInvulnerable = false;
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
    private static void Recruited(MobileParty banditParty)
    {
        // Compare the player troop, because an owner without a party leaves the recruiter's MainParty in place.
        var recruiter = Game.Current.PlayerTroop;
        Run(quest =>
        {
            if (quest._banditParty != banditParty) return;
            if (Game.Current.PlayerTroop == recruiter) quest.OnPlayerHiredBandits();
            else quest.CompleteQuestWithCancel();
        });
    }

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

    private static void Run(Action<Quest> action)
    {
        if (ModInformation.IsServer && ContainerProvider.TryResolve<ICaravanAmbushEvents>(out var events)) events.Run(action);
    }
}
