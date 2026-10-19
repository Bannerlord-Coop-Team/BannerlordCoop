using Common.Network;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Issues;
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
        var caravanId = IdOf(quest._caravanParty);
        var banditId = IdOf(quest._banditParty);
        var journal = quest.JournalEntries.Select(entry => entry.LogText.ToString()).ToArray();
        var state = new State(ownerId, issue.StringId, controllerId, caravanId, banditId,
            quest._isCaravanSaved, quest._vicinityCheckFailCounter, journal);
        if (published.TryGetValue(quest, out var previous) && previous.Equals(state)) return;
        published[quest] = state;
        network.Send(peer, new NetworkCaravanAmbushState(ownerId, issue.StringId, controllerId, caravanId,
            banditId, quest._isCaravanSaved, quest._vicinityCheckFailCounter, journal));
    }

    public void Forget(Quest quest) => published.Remove(quest);

    private string IdOf(object obj)
    {
        if (obj == null) return string.Empty;
        objects.TryGetIdWithLogging(obj, out var id);
        return id ?? string.Empty;
    }

    private readonly struct State : IEquatable<State>
    {
        private readonly string ownerId;
        private readonly string issueId;
        private readonly string controllerId;
        private readonly string caravanId;
        private readonly string banditId;
        private readonly bool saved;
        private readonly int warnings;
        private readonly string[] journal;

        public State(string ownerId, string issueId, string controllerId, string caravanId, string banditId,
            bool saved, int warnings, string[] journal)
        {
            this.ownerId = ownerId;
            this.issueId = issueId;
            this.controllerId = controllerId;
            this.caravanId = caravanId;
            this.banditId = banditId;
            this.saved = saved;
            this.warnings = warnings;
            this.journal = journal;
        }

        public bool Equals(State other) => ownerId == other.ownerId && issueId == other.issueId &&
            controllerId == other.controllerId && caravanId == other.caravanId && banditId == other.banditId &&
            saved == other.saved && warnings == other.warnings && journal.SequenceEqual(other.journal);

        public override bool Equals(object obj) => obj is State other && Equals(other);

        public override int GetHashCode() => 0;
    }
}
