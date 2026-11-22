using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
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
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
internal sealed class RaidAnEnemyTerritoryRaidCompletionPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<RaidAnEnemyTerritoryRaidCompletionPatch>();

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnRaidCompleted))]
    [HarmonyPrefix]
    private static bool OnRaidCompletedPrefix() => false;

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.RaidCompleted))]
    [HarmonyPostfix]
    private static void RaidCompletedPostfix(BattleSideEnum winnerSide, RaidEventComponent raidEvent)
    {
        if (ModInformation.IsClient || !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return;
        }

        // Count progress as each owner without ending the quest or releasing a party inside the map event.
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

    private static void Resolve(Quest quest, Settlement village, MobileParty attackerParty)
    {
        if (quest == null || village == null || attackerParty == null || quest.IsFinalized ||
            quest._raidedVillages.Contains(village))
        {
            return;
        }

        var issue = quest.QuestGiver.Issue;
        if (issue == null || !ContainerProvider.TryResolve<IIssueOwnerResolver>(out var ownerResolver) ||
            !ownerResolver.TryResolveOwnerParty(issue, out var ownerParty) ||
            (ownerParty != attackerParty &&
             (!ownerResolver.TryResolveOwnerHero(issue, out var ownerHero) || ownerHero.PartyBelongedTo != attackerParty)))
        {
            return;
        }

        quest._raidedVillages.Add(village);
        quest._raidedVillagesTrackLog?.UpdateCurrentProgress(quest._raidedVillages.Count);
        Logger.Debug("Raid quest progress issue={issue} giver={giver} count={count} village={village}",
            issue.StringId,
            quest.QuestGiver.StringId,
            quest._raidedVillages.Count,
            village.StringId);

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager) ||
            !objectManager.TryGetIdWithLogging(quest.QuestGiver, out var giverId))
        {
            return;
        }

        var villageIds = new List<string>();
        foreach (var raidedVillage in quest._raidedVillages)
        {
            if (!objectManager.TryGetIdWithLogging(raidedVillage, out var raidedVillageId))
            {
                return;
            }
            villageIds.Add(raidedVillageId);
        }
        var entries = quest.JournalEntries.Select(log => new IssueJournalEntry(log)).ToArray();

        MessageBroker.Instance.Publish(quest, new RaidAnEnemyTerritoryProgressReady(
            giverId,
            issue.StringId,
            villageIds.ToArray(),
            entries));
    }
}
