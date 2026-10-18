using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest;

namespace GameInterface.Services.Issues.Quests.RaidAnEnemyTerritory.Patches;

[HarmonyPatch]
internal sealed class RaidAnEnemyTerritoryRaidCompletionPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<RaidAnEnemyTerritoryRaidCompletionPatch>();

    [HarmonyPatch(typeof(Quest), "OnRaidCompleted")]
    [HarmonyPrefix]
    private static bool OnRaidCompletedPrefix() => false;

    [HarmonyPatch(typeof(RaidAnEnemyTerritoryIssueBehavior), nameof(RaidAnEnemyTerritoryIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void RegisterEventsPostfix(RaidAnEnemyTerritoryIssueBehavior __instance)
    {
        if (ModInformation.IsClient)
        {
            return;
        }

        CampaignEvents.RaidCompletedEvent.AddNonSerializedListener(__instance, OnRaidCompleted);
    }

    private static void OnRaidCompleted(BattleSideEnum winnerSide, RaidEventComponent raidEvent)
    {
        if (winnerSide != BattleSideEnum.Attacker || raidEvent?.MapEvent?.MapEventSettlement?.IsVillage != true ||
            raidEvent.MapEvent.MapEventSettlement.SettlementHitPoints != 0f || Campaign.Current?.IssueManager == null)
        {
            return;
        }

        var issues = new List<IssueBase>();
        foreach (var issue in Campaign.Current.IssueManager.Issues.Values)
        {
            issues.Add(issue);
        }

        foreach (var issue in issues)
        {
            if (issue.IssueQuest is not Quest quest || quest.IsFinalized)
            {
                continue;
            }

            foreach (var party in raidEvent.MapEvent.AttackerSide.Parties)
            {
                Resolve(quest, raidEvent.MapEvent.MapEventSettlement, party.Party?.MobileParty);
            }
        }
    }

    internal static void Resolve(Quest quest, Settlement village, MobileParty attackerParty)
    {
        if (quest == null || village == null || attackerParty == null || quest.IsFinalized ||
            quest._raidedVillages.Contains(village))
        {
            return;
        }

        if (!ContainerProvider.TryResolve<IIssueOwnerResolver>(out var ownerResolver) ||
            !ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !ownerResolver.TryGetOwnerControllerId(quest.QuestGiver?.Issue, out var controllerId) ||
            !playerManager.TryGetPlayer(controllerId, out var player) ||
            !ContainerProvider.TryResolve<IObjectManager>(out var ownerObjects) ||
            !ownerObjects.TryGetObject(player.MobilePartyId, out MobileParty ownerParty) ||
            (!ownerResolver.TryResolveOwnerHero(quest.QuestGiver?.Issue, out var ownerHero) ||
                (ownerParty != attackerParty && ownerHero.PartyBelongedTo != attackerParty)))
        {
            return;
        }

        quest._raidedVillages.Add(village);
        quest._raidedVillagesTrackLog?.UpdateCurrentProgress(quest._raidedVillages.Count);

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !objectManager.TryGetIdWithLogging(quest.QuestGiver, out var giverId))
        {
            return;
        }

        var villageIds = new List<string>();
        foreach (var raidedVillage in quest._raidedVillages)
        {
            if (objectManager.TryGetId(raidedVillage, out var raidedVillageId))
            {
                villageIds.Add(raidedVillageId);
            }
        }
        var entries = quest.JournalEntries.Select(log => new RaidAnEnemyTerritoryJournalEntry
        {
            Time = log.LogTime,
            Text = log.LogText,
            Task = log.TaskName,
            Progress = log.CurrentProgress,
            Range = log.Range,
            Type = log.Type,
        }).ToArray();

        MessageBroker.Instance.Publish(quest, new RaidAnEnemyTerritoryProgressReady(
            giverId,
            quest.StringId,
            villageIds.ToArray(),
            entries));
    }
}
