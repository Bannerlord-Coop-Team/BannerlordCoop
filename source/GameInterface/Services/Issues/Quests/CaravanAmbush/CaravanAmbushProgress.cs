using Common.Network;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System.Linq;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush;

internal interface ICaravanAmbushProgress
{
    (MobileParty Caravan, MobileParty Bandits, bool Saved, int Warnings, int JournalCount) Capture(Quest quest);
    void Publish(Quest quest, bool openRewardConversation = false);
}

internal class CaravanAmbushProgress : ICaravanAmbushProgress
{
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueOwnerResolver owners;
    private readonly IPlayerManager players;

    public CaravanAmbushProgress(INetwork network, IObjectManager objects, IIssueOwnerResolver owners,
        IPlayerManager players)
    {
        this.network = network;
        this.objects = objects;
        this.owners = owners;
        this.players = players;
    }

    public (MobileParty Caravan, MobileParty Bandits, bool Saved, int Warnings, int JournalCount) Capture(Quest quest) =>
        (quest._caravanParty, quest._banditParty, quest._isCaravanSaved, quest._vicinityCheckFailCounter, quest.JournalEntries.Count);

    public void Publish(Quest quest, bool openRewardConversation = false)
    {
        var issue = IssueManager.GetIssueOfQuest(quest);
        if (issue == null) return;
        if (!objects.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;
        if (!owners.TryGetOwnerControllerId(issue, out var controllerId)) return;
        if (!players.TryGetPeer(controllerId, out var peer)) return;
        if (!TryGetPartyId(quest._caravanParty, out var caravanId)) return;
        if (!TryGetPartyId(quest._banditParty, out var banditId)) return;
        var journal = quest.JournalEntries.Select(entry => new IssueJournalEntry(entry)).ToArray();
        network.Send(peer, new NetworkCaravanAmbushProgress(ownerId, issue.StringId, controllerId, caravanId,
            banditId, quest._isCaravanSaved, quest._vicinityCheckFailCounter, journal, openRewardConversation));
    }

    private bool TryGetPartyId(MobileParty party, out string id)
    {
        id = null;
        return party == null || !party.IsActive || objects.TryGetIdWithLogging(party, out id);
    }
}
