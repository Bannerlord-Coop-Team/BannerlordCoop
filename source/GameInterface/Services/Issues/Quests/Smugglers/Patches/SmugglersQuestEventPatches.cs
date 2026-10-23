using Common;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.SmugglersIssueBehavior.SmugglersIssueQuest;

namespace GameInterface.Services.Issues.Quests.Smugglers.Patches;

/// <summary>
/// The fight with the smugglers, the town owner change that cancels the quest and the smugglers' route are decided
/// on the server, so its quest object runs these listeners as the owning player and the owner's client skips them.
/// Wars and kingdom changes are relayed by the framework.
/// </summary>
[HarmonyPatch]
internal class SmugglersQuestEventPatches
{
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnMapEventEnded))]
    [HarmonyPostfix]
    private static void OnMapEventEndedPostfix(MapEvent mapEvent)
    {
        if (!HasActiveSmugglersQuest())
        {
            return;
        }

        // Most battles have no party used by a quest in them
        if (!HasQuestParty(mapEvent))
        {
            return;
        }

        var capturedOwners = new List<(Quest Quest, Hero Owner)>();

        RunAsOwners(quest =>
        {
            // Vanilla reads the party without a null check, it is only missing when spawning it failed
            if (quest._smugglerParty == null)
            {
                return;
            }

            quest.OnMapEventEnded(mapEvent);

            if (IsOwnerCapturedInTheFight(quest, mapEvent))
            {
                capturedOwners.Add((quest, Hero.MainHero));
            }
            else
            {
                CancelIfDefeatedWithoutOwner(quest, mapEvent);
            }
        });

        FailForCapturedOwners(capturedOwners);
    }

    // The server takes a defeated owner prisoner before the battle ends, so vanilla no longer sees the owner's party in it.
    // The captor can be a party that joined the smugglers, and an owner taken before this battle did not fight in it.
    private static bool IsOwnerCapturedInTheFight(Quest quest, MapEvent mapEvent)
    {
        var owner = Hero.MainHero;
        var smugglers = quest._smugglerParty.Party;
        var captor = owner?.PartyBelongedToAsPrisoner;

        return quest.IsOngoing && captor != null && owner.IsPrisoner && ContainsParty(mapEvent, smugglers) &&
            ContainsParty(mapEvent, captor) && captor.Side == smugglers.Side &&
            owner.CaptivityStartTime >= mapEvent.BattleStartTime;
    }

    private static void FailForCapturedOwners(List<(Quest Quest, Hero Owner)> capturedOwners)
    {
        if (capturedOwners.Count == 0)
        {
            return;
        }

        // Failing removes the smugglers and removing a party frees every hero prisoner except the main hero, so an owner
        // they hold is released first, outside the owner's scope where the server would take them for its own main hero
        foreach (var (quest, owner) in capturedOwners)
        {
            if (owner.PartyBelongedToAsPrisoner == quest._smugglerParty.Party)
            {
                EndCaptivityAction.ApplyByReleasedAfterBattle(owner);
            }
        }

        RunAsOwners(quest =>
        {
            if (quest.IsOngoing && capturedOwners.Exists(captured => ReferenceEquals(captured.Quest, quest)))
            {
                quest.FailQuest();
            }
        });
    }

    // Vanilla only expects the owner to fight the smugglers, another player beating them leaves the quest unwinnable
    private static void CancelIfDefeatedWithoutOwner(Quest quest, MapEvent mapEvent)
    {
        var smugglers = quest._smugglerParty.Party;
        if (!quest.IsOngoing || !ContainsParty(mapEvent, smugglers))
        {
            return;
        }

        if (smugglers.Side == BattleSideEnum.None || mapEvent.DefeatedSide != smugglers.Side)
        {
            return;
        }

        quest.CompleteQuestWithCancel();
    }

    private static bool HasActiveSmugglersQuest()
    {
        return Campaign.Current?.IssueManager?.Issues.Values.Any(issue =>
            issue.IssueQuest is Quest quest && quest.IsOngoing) == true;
    }

    private static bool HasQuestParty(MapEvent mapEvent)
    {
        return mapEvent?._sides?.Any(side => side?.Parties?.Any(party =>
            party?.Party?.MobileParty?.IsCurrentlyUsedByAQuest == true) == true) == true;
    }

    private static bool ContainsParty(MapEvent mapEvent, PartyBase party)
    {
        return mapEvent?._sides?.Any(side => side?.Parties?.Any(mapEventParty => mapEventParty?.Party == party) == true) == true;
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
