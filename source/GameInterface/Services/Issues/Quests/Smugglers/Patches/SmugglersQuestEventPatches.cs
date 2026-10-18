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
using Quest = TaleWorlds.CampaignSystem.Issues.SmugglersIssueBehavior.SmugglersIssueQuest;

namespace GameInterface.Services.Issues.Quests.Smugglers.Patches;

/// <summary>
/// The fight with the smugglers, the wars that cancel the quest and the smugglers' route are decided on the
/// server, so its quest object runs these listeners as the owning player and the owner's client skips them.
/// </summary>
[HarmonyPatch]
internal class SmugglersQuestEventPatches
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventEnded))]
    [HarmonyPostfix]
    private static void OnMapEventEndedPostfix(MapEvent mapEvent)
    {
        // Most battles have no party used by a quest in them
        if (!mapEvent.InvolvedParties.Any(party => party.MobileParty?.IsCurrentlyUsedByAQuest == true))
        {
            return;
        }

        RunAsOwners(quest =>
        {
            // Vanilla reads the party without a null check, it is only missing when spawning it failed
            if (quest._smugglerParty == null)
            {
                return;
            }

            quest.OnMapEventEnded(mapEvent);
            FailIfOwnerWasCaptured(quest, mapEvent);
            CancelIfDefeatedWithoutOwner(quest, mapEvent);
        });
    }

    // The server takes a defeated owner prisoner before the battle ends, so vanilla no longer sees the owner's party in it
    private static void FailIfOwnerWasCaptured(Quest quest, MapEvent mapEvent)
    {
        var owner = Hero.MainHero;
        var smugglers = quest._smugglerParty.Party;
        if (!quest.IsOngoing || owner == null || !owner.IsPrisoner || owner.PartyBelongedToAsPrisoner != smugglers ||
            !mapEvent.InvolvedParties.Contains(smugglers))
        {
            return;
        }

        // Failing removes the smugglers, and removing a party frees every hero prisoner except the main hero
        EndCaptivityAction.ApplyByReleasedAfterBattle(owner);
        quest.FailQuest();
    }

    // Vanilla only expects the owner to fight the smugglers, another player beating them leaves the quest unwinnable
    private static void CancelIfDefeatedWithoutOwner(Quest quest, MapEvent mapEvent)
    {
        var smugglers = quest._smugglerParty.Party;
        if (!quest.IsOngoing || !mapEvent.InvolvedParties.Contains(smugglers))
        {
            return;
        }

        if (smugglers.Side == BattleSideEnum.None || mapEvent.DefeatedSide != smugglers.Side)
        {
            return;
        }

        quest.CompleteQuestWithCancel();
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnWarDeclared))]
    [HarmonyPostfix]
    private static void OnWarDeclaredPostfix(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail declareWarDetail)
    {
        RunAsOwners(quest => quest.OnWarDeclared(faction1, faction2, declareWarDetail));
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnClanChangedKingdom))]
    [HarmonyPostfix]
    private static void OnClanChangedKingdomPostfix(
        Clan clan,
        Kingdom oldKingdom,
        Kingdom newKingdom,
        ChangeKingdomAction.ChangeKingdomActionDetail actionDetail,
        bool showNotification)
    {
        RunAsOwners(quest => quest.OnClanChangedKingdom(clan, oldKingdom, newKingdom, actionDetail, showNotification));
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnSettlementOwnerChanged))]
    [HarmonyPostfix]
    private static void OnSettlementOwnerChangedPostfix(
        Settlement settlement,
        bool openToClaim,
        Hero newOwner,
        Hero oldOwner,
        Hero capturerHero,
        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        RunAsOwners(quest => quest.OnSettlementOwnerChanged(settlement, openToClaim, newOwner, oldOwner, capturerHero, detail));
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.HourlyTickParty))]
    [HarmonyPostfix]
    private static void HourlyTickPartyPostfix(MobileParty mobileParty)
    {
        // Every party ticks hourly, only one used by a quest can be the smugglers
        if (!mobileParty.IsCurrentlyUsedByAQuest)
        {
            return;
        }

        RunAsOwners(quest => quest.OnHourlyTickParty(mobileParty));
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnMapEventEnded))]
    [HarmonyPrefix]
    private static bool OnMapEventEndedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnWarDeclared))]
    [HarmonyPrefix]
    private static bool OnWarDeclaredPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnClanChangedKingdom))]
    [HarmonyPrefix]
    private static bool OnClanChangedKingdomPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnSettlementOwnerChanged))]
    [HarmonyPrefix]
    private static bool OnSettlementOwnerChangedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnHourlyTickParty))]
    [HarmonyPrefix]
    private static bool OnHourlyTickPartyPrefix() => ModInformation.IsServer;

    // The server fails the quest when it times out, a client replaying the timeout only ends its copy
    [HarmonyPatch(typeof(Quest), nameof(Quest.OnTimedOut))]
    [HarmonyPrefix]
    private static bool OnTimedOutPrefix() => ModInformation.IsServer;

    private static void RunAsOwners(Action<Quest> listener)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return;
        }

        dispatcher.RunAsOwners(listener);
    }
}
