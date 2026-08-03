using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Messages;

/// <summary>Published from <see cref="Patches.LadysKnightOutAcceptancePatches"/> whenever
/// <c>IssueManager.StartIssueQuest</c> genuinely runs for a Lady's Knight Out issue. Carries the accepting
/// machine's captured <c>_difficultyMultiplier</c> - see <see cref="Interfaces.ILadysKnightOutIssueInterface"/>'s
/// doc comment.</summary>
public readonly struct LadysKnightOutIssueQuestAcceptTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;
    public readonly float DifficultyMultiplier;

    public LadysKnightOutIssueQuestAcceptTriggered(Hero owner, string controllerId, float difficultyMultiplier)
    {
        Owner = owner;
        ControllerId = controllerId;
        DifficultyMultiplier = difficultyMultiplier;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestLadysKnightOutIssueAcceptQuest : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public RequestLadysKnightOutIssueAcceptQuest(string ownerId)
    {
        OwnerId = ownerId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkLadysKnightOutIssueQuestAccepted : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string OwnerControllerId;
    [ProtoMember(3)]
    public readonly float DifficultyMultiplier;

    public NetworkLadysKnightOutIssueQuestAccepted(string ownerId, string ownerControllerId, float difficultyMultiplier)
    {
        OwnerId = ownerId;
        OwnerControllerId = ownerControllerId;
        DifficultyMultiplier = difficultyMultiplier;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkLadysKnightOutIssueAcceptRejected : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public NetworkLadysKnightOutIssueAcceptRejected(string ownerId)
    {
        OwnerId = ownerId;
    }
}
