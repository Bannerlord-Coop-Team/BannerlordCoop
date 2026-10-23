using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;

namespace GameInterface.Services.Issues.Quests.ArtisanProducts;

public readonly struct ArtisanProductsOwnerChanged : IEvent
{
    public readonly Issue Issue;
    public readonly Hero Previous;
    public ArtisanProductsOwnerChanged(Issue issue, Hero previous) { Issue = issue; Previous = previous; }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkArtisanProductsOwnerChanged : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string PreviousId;
    [ProtoMember(2)] public readonly string OwnerId;
    [ProtoMember(3)] public readonly string IssueId;
    [ProtoMember(4)] public readonly string ControllerId;
    public NetworkArtisanProductsOwnerChanged(string previousId, string ownerId, string issueId, string controllerId)
    { PreviousId = previousId; OwnerId = ownerId; IssueId = issueId; ControllerId = controllerId; }
}

internal class ArtisanProductsInheritanceHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueOwnershipRegistry ownership;

    public ArtisanProductsInheritanceHandler(IMessageBroker broker, INetwork network, IObjectManager objects, IIssueOwnershipRegistry ownership)
    {
        this.broker = broker; this.network = network; this.objects = objects; this.ownership = ownership;
        broker.Subscribe<ArtisanProductsOwnerChanged>(Changed);
        broker.Subscribe<NetworkArtisanProductsOwnerChanged>(Apply);
    }

    public void Dispose()
    {
        broker.Unsubscribe<ArtisanProductsOwnerChanged>(Changed);
        broker.Unsubscribe<NetworkArtisanProductsOwnerChanged>(Apply);
    }

    private void Changed(MessagePayload<ArtisanProductsOwnerChanged> payload)
    {
        if (ModInformation.IsClient) return;
        var issue = payload.What.Issue;
        if (!objects.TryGetIdWithLogging(payload.What.Previous, out var previousId) ||
            !objects.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;
        ownership.TryGetOwner(previousId, issue.StringId, out var controllerId);
        UpdateQuestGiver(issue, payload.What.Previous);
        Move(previousId, ownerId, issue.StringId, controllerId);
        network.SendAll(new NetworkArtisanProductsOwnerChanged(previousId, ownerId, issue.StringId, controllerId));
    }

    private void Apply(MessagePayload<NetworkArtisanProductsOwnerChanged> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objects.TryGetObjectWithLogging<Hero>(data.PreviousId, out var previous) ||
                !objects.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner) ||
                !(previous.Issue is Issue issue) || issue.StringId != data.IssueId || owner.Issue != null) return;
            using (new AllowedThread())
            {
                Campaign.Current.IssueManager.ChangeIssueOwner(issue, owner);
                UpdateQuestGiver(issue, previous);
            }
            Move(data.PreviousId, data.OwnerId, data.IssueId, data.ControllerId);
        }, context: nameof(ArtisanProductsInheritanceHandler));
    }

    private void Move(string previousId, string ownerId, string issueId, string controllerId)
    {
        ownership.Remove(previousId);
        if (!string.IsNullOrEmpty(controllerId)) ownership.TrySetOwner(ownerId, issueId, controllerId);
    }

    private void UpdateQuestGiver(Issue issue, Hero previous)
    {
        var quest = issue.IssueQuest;
        if (quest == null) return;
        quest.QuestGiver = issue.IssueOwner;
        var manager = Campaign.Current.QuestManager;
        if (!manager.Quests.Contains(quest)) return;
        if (manager.TrackedObjects.TryGetValue(previous, out var oldQuests) && oldQuests.Contains(quest))
            manager.RemoveTrackedObjectForQuest(previous, quest);
        if (!manager.TrackedObjects.TryGetValue(issue.IssueOwner, out var newQuests) || !newQuests.Contains(quest))
            manager.AddTrackedObjectForQuest(issue.IssueOwner, quest);
    }
}
