using Common.Logging;
using Common.Network;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Quest = TaleWorlds.CampaignSystem.Issues.RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest;

namespace GameInterface.Services.Issues.Quests.RaidAnEnemyTerritory;

internal interface IRaidAnEnemyTerritoryProgress
{
    void RecordCompletedRaid(Quest quest, Settlement village, ISet<MobileParty> attackers);
}

internal sealed class RaidAnEnemyTerritoryProgress : IRaidAnEnemyTerritoryProgress
{
    private static readonly ILogger Logger = LogManager.GetLogger<RaidAnEnemyTerritoryProgress>();

    private readonly IIssueOwnerResolver owners;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager players;
    private readonly INetwork network;

    public RaidAnEnemyTerritoryProgress(IIssueOwnerResolver owners, IObjectManager objectManager,
        IPlayerManager players, INetwork network)
    {
        this.owners = owners;
        this.objectManager = objectManager;
        this.players = players;
        this.network = network;
    }

    public void RecordCompletedRaid(Quest quest, Settlement village, ISet<MobileParty> attackers)
    {
        var issue = quest.QuestGiver.Issue;
        if (quest._raidedVillages.Contains(village) ||
            !owners.TryResolveOwnerParty(issue, out var ownerParty) ||
            !owners.TryResolveOwnerHero(issue, out var ownerHero))
        {
            return;
        }

        // The owner can raid with a party other than their registered party.
        var ownerRaided = attackers.Contains(ownerParty) || attackers.Contains(ownerHero.PartyBelongedTo);
        if (!ownerRaided) return;

        quest._raidedVillages.Add(village);
        quest._raidedVillagesTrackLog.UpdateCurrentProgress(quest._raidedVillages.Count);
        Logger.Debug("Raid quest progress issue={issue} giver={giver} count={count} village={village}",
            issue.StringId, quest.QuestGiver.StringId, quest._raidedVillages.Count, village.StringId);

        SendProgress(quest, issue);
    }

    private void SendProgress(Quest quest, IssueBase issue)
    {
        if (!owners.TryGetOwnerControllerId(issue, out var owner) || !players.TryGetPeer(owner, out var peer) ||
            !objectManager.TryGetIdWithLogging(quest.QuestGiver, out var giverId))
        {
            return;
        }

        var villageIds = new List<string>();
        foreach (var raidedVillage in quest._raidedVillages)
        {
            if (!objectManager.TryGetIdWithLogging(raidedVillage, out var villageId))
            {
                return;
            }
            villageIds.Add(villageId);
        }

        var entries = quest.JournalEntries.Select(log => new IssueJournalEntry(log)).ToArray();
        network.Send(peer, new NetworkRaidAnEnemyTerritoryProgress(giverId, issue.StringId, villageIds.ToArray(), entries));
    }
}
