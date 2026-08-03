using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Messages;

/// <summary>Published from <see cref="Patches.GangLeaderNeedsRecruitsAcceptancePatches"/> whenever
/// <c>IssueManager.StartIssueQuest</c> genuinely runs for a Gang Leader Needs Recruits issue. Carries the
/// accepting machine's captured <c>_requestedRecruitCount</c> - see
/// <see cref="Interfaces.IGangLeaderNeedsRecruitsIssueInterface"/>'s doc comment.</summary>
public readonly struct GangRecruitsIssueQuestAcceptTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;
    public readonly int RequestedRecruitCount;

    public GangRecruitsIssueQuestAcceptTriggered(Hero owner, string controllerId, int requestedRecruitCount)
    {
        Owner = owner;
        ControllerId = controllerId;
        RequestedRecruitCount = requestedRecruitCount;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestGangRecruitsIssueAcceptQuest : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public RequestGangRecruitsIssueAcceptQuest(string ownerId)
    {
        OwnerId = ownerId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkGangRecruitsIssueQuestAccepted : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string OwnerControllerId;
    [ProtoMember(3)]
    public readonly int RequestedRecruitCount;

    public NetworkGangRecruitsIssueQuestAccepted(string ownerId, string ownerControllerId, int requestedRecruitCount)
    {
        OwnerId = ownerId;
        OwnerControllerId = ownerControllerId;
        RequestedRecruitCount = requestedRecruitCount;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkGangRecruitsIssueAcceptRejected : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public NetworkGangRecruitsIssueAcceptRejected(string ownerId)
    {
        OwnerId = ownerId;
    }
}
