using Common.Messaging;
using ProtoBuf;
using SandBox.Issues;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Messages;

/// <summary>Published on the server after a genuine <c>IssueManager.CreateNewIssue</c> creates a
/// <see cref="ProdigalSonIssueBehavior.ProdigalSonIssue"/>.</summary>
public readonly struct ProdigalSonIssueCreated : IEvent
{
    public readonly ProdigalSonIssueBehavior.ProdigalSonIssue Issue;

    public ProdigalSonIssueCreated(ProdigalSonIssueBehavior.ProdigalSonIssue issue)
    {
        Issue = issue;
    }
}

/// <summary>Server -&gt; all clients: the two rolled heroes (the prodigal son and the gang-leader holding him)
/// for a newly created Prodigal Son issue, so every client replicates the exact same pair instead of
/// independently re-rolling.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkProdigalSonIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string ProdigalSonId;
    [ProtoMember(3)]
    public readonly string TargetHeroId;

    public NetworkProdigalSonIssueCreated(string ownerId, string prodigalSonId, string targetHeroId)
    {
        OwnerId = ownerId;
        ProdigalSonId = prodigalSonId;
        TargetHeroId = targetHeroId;
    }
}
