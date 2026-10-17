using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.Util;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue;

namespace GameInterface.Services.Issues.Quests.EscortMerchantCaravan;

internal readonly struct EscortGiverChanged : IEvent
{
    public readonly Issue Issue;
    public readonly Hero Previous;

    public EscortGiverChanged(Issue issue, Hero previous)
    {
        Issue = issue;
        Previous = previous;
    }
}

[ProtoContract(SkipConstructor = true)]
internal sealed class NetworkEscortGiverChanged : IServerToClientCommand
{
    [ProtoMember(1)] public string PreviousId;
    [ProtoMember(2)] public string GiverId;
    [ProtoMember(3)] public string IssueId;
    [ProtoMember(4)] public string ControllerId;
}

internal sealed class EscortGiverChangedHandler : IHandler
{
    private readonly IMessageBroker messages;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueResolver issues;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly ISendCoalescer coalescer;

    public EscortGiverChangedHandler(IMessageBroker messages, INetwork network, IObjectManager objects,
        IIssueResolver issues, IIssueOwnershipRegistry ownership, ISendCoalescer coalescer = null)
    {
        this.messages = messages;
        this.network = network;
        this.objects = objects;
        this.issues = issues;
        this.ownership = ownership;
        this.coalescer = coalescer;
        messages.Subscribe<EscortGiverChanged>(Changed);
        messages.Subscribe<NetworkEscortGiverChanged>(Receive);
    }

    public void Dispose()
    {
        messages.Unsubscribe<EscortGiverChanged>(Changed);
        messages.Unsubscribe<NetworkEscortGiverChanged>(Receive);
    }

    private void Changed(MessagePayload<EscortGiverChanged> payload)
    {
        if (ModInformation.IsClient) return;
        var issue = payload.What.Issue;
        if (!objects.TryGetIdWithLogging(payload.What.Previous, out var previousId) ||
            !objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId)) return;
        ownership.TryGetOwner(previousId, issue.StringId, out var controller);
        MoveOwnership(previousId, giverId, issue.StringId, controller);
        coalescer?.Flush(network);
        network.SendAll(new NetworkEscortGiverChanged
        {
            PreviousId = previousId, GiverId = giverId, IssueId = issue.StringId, ControllerId = controller,
        });
    }

    private void Receive(MessagePayload<NetworkEscortGiverChanged> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!issues.TryResolve(data.PreviousId, data.IssueId, out var previous, out var issue, out _) ||
                !(issue is Issue) || issue.IssueQuest != null ||
                !objects.TryGetObjectWithLogging<Hero>(data.GiverId, out var giver) || giver.Issue != null) return;
            if (Hero.OneToOneConversationHero == previous && Campaign.Current.ConversationManager.IsConversationInProgress)
                Campaign.Current.ConversationManager.EndConversation();
            using (new AllowedThread()) Campaign.Current.IssueManager.ChangeIssueOwner(issue, giver);
            MoveOwnership(data.PreviousId, data.GiverId, data.IssueId, data.ControllerId);
        }, context: nameof(EscortGiverChangedHandler));
    }

    private void MoveOwnership(string previous, string giver, string issue, string controller)
    {
        ownership.Remove(previous);
        if (controller != null) ownership.TrySetOwner(giver, issue, controller);
    }
}
