using Common;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkHeadmanNeedsGrainEffectsResolved : ICommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly string IssueId;

    public NetworkHeadmanNeedsGrainEffectsResolved(string giverId, string issueId)
    {
        GiverId = giverId;
        IssueId = issueId;
    }
}

internal class HeadmanNeedsGrainJournalHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IObjectManager objectManager;
    private readonly IIssueConversationGuard conversationGuard;

    public HeadmanNeedsGrainJournalHandler(IMessageBroker broker, IObjectManager objectManager,
        IIssueConversationGuard conversationGuard)
    {
        this.broker = broker;
        this.objectManager = objectManager;
        this.conversationGuard = conversationGuard;
        broker.Subscribe<NetworkHeadmanNeedsGrainEffectsResolved>(HandleEffectsResolved);
    }

    public void Dispose()
    {
        broker.Unsubscribe<NetworkHeadmanNeedsGrainEffectsResolved>(HandleEffectsResolved);
    }

    private void HandleEffectsResolved(MessagePayload<NetworkHeadmanNeedsGrainEffectsResolved> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() => conversationGuard.RunInOrder(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.GiverId, out var giver)) return;
            var issue = giver.Issue;
            if (!(issue is TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue) ||
                issue.StringId != data.IssueId || !issue.IsSolvingWithAlternative) return;
            issue._areIssueEffectsResolved = true;
        }, data.GiverId), context: nameof(HeadmanNeedsGrainJournalHandler));
    }
}
