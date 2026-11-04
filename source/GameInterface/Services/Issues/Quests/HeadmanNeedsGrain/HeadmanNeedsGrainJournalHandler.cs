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

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkHeadmanNeedsGrainAiCompleted : ICommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly string SolverId;

    public NetworkHeadmanNeedsGrainAiCompleted(string giverId, string issueId, string solverId)
    {
        GiverId = giverId;
        IssueId = issueId;
        SolverId = solverId;
    }
}

internal class HeadmanNeedsGrainJournalHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IObjectManager objectManager;

    public HeadmanNeedsGrainJournalHandler(IMessageBroker broker, IObjectManager objectManager)
    {
        this.broker = broker;
        this.objectManager = objectManager;
        broker.Subscribe<NetworkHeadmanNeedsGrainAiCompleted>(HandleAi);
        broker.Subscribe<NetworkHeadmanNeedsGrainEffectsResolved>(HandleEffectsResolved);
    }

    public void Dispose()
    {
        broker.Unsubscribe<NetworkHeadmanNeedsGrainAiCompleted>(HandleAi);
        broker.Unsubscribe<NetworkHeadmanNeedsGrainEffectsResolved>(HandleEffectsResolved);
    }

    private void HandleEffectsResolved(MessagePayload<NetworkHeadmanNeedsGrainEffectsResolved> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.GiverId, out var giver)) return;
            var issue = giver.Issue;
            if (!(issue is TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue) ||
                issue.StringId != data.IssueId || !issue.IsSolvingWithAlternative) return;
            issue._areIssueEffectsResolved = true;
        }, context: nameof(HeadmanNeedsGrainJournalHandler));
    }

    private void HandleAi(MessagePayload<NetworkHeadmanNeedsGrainAiCompleted> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.GiverId, out var giver) ||
                !objectManager.TryGetObjectWithLogging<Hero>(data.SolverId, out var solver)) return;
            var issue = giver.Issue;
            if (!(issue is TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue) ||
                issue.StringId != data.IssueId) return;
            using (new Common.Util.AllowedThread()) issue.CompleteIssueWithAiLord(solver);
        }, context: nameof(HeadmanNeedsGrainJournalHandler));
    }

}
