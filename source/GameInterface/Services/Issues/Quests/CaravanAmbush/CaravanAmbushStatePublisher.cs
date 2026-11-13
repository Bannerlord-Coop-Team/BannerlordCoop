using Common.Network;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush;

internal interface ICaravanAmbushStatePublisher
{
    void Publish(Quest quest);
    void Forget(Quest quest);
}

internal class CaravanAmbushStatePublisher : ICaravanAmbushStatePublisher
{
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueOwnerResolver owners;
    private readonly IPlayerManager players;
    private readonly Dictionary<Quest, State> published = new();

    public CaravanAmbushStatePublisher(INetwork network, IObjectManager objects, IIssueOwnerResolver owners,
        IPlayerManager players)
    {
        this.network = network;
        this.objects = objects;
        this.owners = owners;
        this.players = players;
    }

    public void Publish(Quest quest)
    {
        var issue = IssueManager.GetIssueOfQuest(quest);
        if (issue == null || !objects.TryGetIdWithLogging(issue.IssueOwner, out var ownerId) ||
            !owners.TryGetOwnerControllerId(issue, out var controllerId) ||
            !players.TryGetPeer(controllerId, out var peer)) return;
        if (!TryGetPartyId(quest._caravanParty, out var caravanId) ||
            !TryGetPartyId(quest._banditParty, out var banditId)) return;
        var journalCount = quest.JournalEntries.Count;
        if (published.TryGetValue(quest, out var previous) && previous.Matches(ownerId, issue.StringId,
            controllerId, caravanId, banditId, quest._isCaravanSaved, quest._vicinityCheckFailCounter, journalCount)) return;
        var journal = quest.JournalEntries.Select(entry => new IssueJournalEntry(entry)).ToArray();
        var state = new State(ownerId, issue.StringId, controllerId, caravanId, banditId,
            quest._isCaravanSaved, quest._vicinityCheckFailCounter, journalCount);
        published[quest] = state;
        network.Send(peer, new NetworkCaravanAmbushState(ownerId, issue.StringId, controllerId, caravanId,
            banditId, quest._isCaravanSaved, quest._vicinityCheckFailCounter, journal));
    }

    public void Forget(Quest quest) => published.Remove(quest);

    private bool TryGetPartyId(MobileParty party, out string id)
    {
        id = string.Empty;
        return party == null || !party.IsActive || objects.TryGetIdWithLogging(party, out id);
    }

    private readonly struct State
    {
        private readonly string ownerId;
        private readonly string issueId;
        private readonly string controllerId;
        private readonly string caravanId;
        private readonly string banditId;
        private readonly bool saved;
        private readonly int warnings;
        private readonly int journalCount;

        public State(string ownerId, string issueId, string controllerId, string caravanId, string banditId,
            bool saved, int warnings, int journalCount)
        {
            this.ownerId = ownerId;
            this.issueId = issueId;
            this.controllerId = controllerId;
            this.caravanId = caravanId;
            this.banditId = banditId;
            this.saved = saved;
            this.warnings = warnings;
            this.journalCount = journalCount;
        }

        public bool Matches(string ownerId, string issueId, string controllerId, string caravanId, string banditId,
            bool saved, int warnings, int journalCount) => this.ownerId == ownerId && this.issueId == issueId &&
            this.controllerId == controllerId && this.caravanId == caravanId && this.banditId == banditId &&
            this.saved == saved && this.warnings == warnings && this.journalCount == journalCount;
    }
}
