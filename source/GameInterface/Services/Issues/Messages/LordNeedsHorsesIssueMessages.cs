using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

/// <summary>Published on the server after a genuine <c>IssueManager.CreateNewIssue</c> creates a
/// <see cref="LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue"/>, carrying the live issue so
/// <see cref="Handlers.LordNeedsHorsesIssueHandler"/> can capture its three rolled/derived fields.</summary>
public readonly struct LordNeedsHorsesIssueCreated : IEvent
{
    public readonly LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue Issue;

    public LordNeedsHorsesIssueCreated(LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue issue)
    {
        Issue = issue;
    }
}

/// <summary>Server -&gt; all clients: the authoritatively-rolled mount item/count/per-unit-value for a newly
/// created Lord Needs Horses issue.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkLordNeedsHorsesIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string MountItemId;
    [ProtoMember(3)]
    public readonly int NumMountsToBeDelivered;
    [ProtoMember(4)]
    public readonly int MountValuePerUnit;

    public NetworkLordNeedsHorsesIssueCreated(string ownerId, string mountItemId, int numMountsToBeDelivered, int mountValuePerUnit)
    {
        OwnerId = ownerId;
        MountItemId = mountItemId;
        NumMountsToBeDelivered = numMountsToBeDelivered;
        MountValuePerUnit = mountValuePerUnit;
    }
}
