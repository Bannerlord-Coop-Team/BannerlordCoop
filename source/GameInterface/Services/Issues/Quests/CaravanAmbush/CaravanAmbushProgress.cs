using Common.Network;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using System.Linq;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush;

internal interface ICaravanAmbushProgress
{
    void Publish(Quest quest);
    void SendConversation(Quest quest);
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

    public void Publish(Quest quest)
    {
        if (!TryGetOwnerPeer(quest, out var issue, out var ownerId, out var controllerId, out var peer) ||
            !TryGetPartyId(quest._caravanParty, out var caravanId) ||
            !TryGetPartyId(quest._banditParty, out var banditId)) return;
        var journal = quest.JournalEntries.Select(entry => new IssueJournalEntry(entry)).ToArray();
        network.Send(peer, new NetworkCaravanAmbushProgress(ownerId, issue.StringId, controllerId, caravanId,
            banditId, quest._isCaravanSaved, quest._vicinityCheckFailCounter, journal));
    }

    public void SendConversation(Quest quest)
    {
        if (!TryGetOwnerPeer(quest, out var issue, out var ownerId, out var controllerId, out var peer)) return;
        network.Send(peer, new NetworkCaravanAmbushConversation(ownerId, issue.StringId, controllerId));
    }

    private bool TryGetOwnerPeer(Quest quest, out IssueBase issue, out string ownerId,
        out string controllerId, out NetPeer peer)
    {
        ownerId = null;
        controllerId = null;
        peer = null;
        issue = IssueManager.GetIssueOfQuest(quest);
        return issue != null && objects.TryGetIdWithLogging(issue.IssueOwner, out ownerId) &&
            owners.TryGetOwnerControllerId(issue, out controllerId) && players.TryGetPeer(controllerId, out peer);
    }

    private bool TryGetPartyId(MobileParty party, out string id)
    {
        id = string.Empty;
        return party == null || !party.IsActive || objects.TryGetIdWithLogging(party, out id);
    }
}
