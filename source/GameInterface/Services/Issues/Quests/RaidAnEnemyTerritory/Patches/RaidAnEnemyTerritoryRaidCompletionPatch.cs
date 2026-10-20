using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
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

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.RaidCompleted))]
    [HarmonyPostfix]
    private static void RaidCompletedPostfix(BattleSideEnum winnerSide, RaidEventComponent raidEvent)
    {
        Logger.Information("Raid quest raid callback winner={winner} village={village} hitPoints={hitPoints} mapEvent={mapEvent}",
            winnerSide,
            raidEvent?.MapEvent?.MapEventSettlement?.StringId,
            raidEvent?.MapEvent?.MapEventSettlement?.SettlementHitPoints,
            raidEvent?.MapEvent?.StringId);
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return;
        }

        // RaidCompleted runs while the map event is unwinding. Apply only after the vanilla callback has
        // returned so owner substitution cannot re-enter map-event finalization or release the owner party.
        GameThread.RunSafe(
            () => dispatcher.RunAsOwners<Quest>(quest => ResolveForOwner(quest, winnerSide, raidEvent)),
            context: nameof(RaidCompletedPostfix));
    }

    private static void ResolveForOwner(Quest quest, BattleSideEnum winnerSide, RaidEventComponent raidEvent)
    {
        if (winnerSide != BattleSideEnum.Attacker || raidEvent?.MapEvent?.MapEventSettlement?.IsVillage != true ||
            raidEvent.MapEvent.MapEventSettlement.SettlementHitPoints != 0f || Campaign.Current?.IssueManager == null)
        {
            return;
        }

        foreach (var party in raidEvent.MapEvent.AttackerSide.Parties)
        {
            Resolve(quest, raidEvent.MapEvent.MapEventSettlement, party.Party?.MobileParty);
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
        Logger.Information("Raid quest progress issue={issue} giver={giver} owner={owner} count={count} village={village}",
            quest.QuestGiver?.Issue?.StringId,
            quest.QuestGiver?.StringId,
            quest.QuestGiver?.Issue?.IssueOwner?.StringId,
            quest._raidedVillages.Count,
            village.StringId);

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !objectManager.TryGetIdWithLogging(quest.QuestGiver, out var giverId))
        {
            return;
        }

        var issueId = quest.QuestGiver?.Issue?.StringId;
        if (string.IsNullOrEmpty(issueId))
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
            issueId,
            villageIds.ToArray(),
            entries));
    }
}
