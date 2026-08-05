using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Messages;

/// <summary>Published from <see cref="Patches.GangLeaderNeedsSpecialWeaponsAcceptancePatches"/> whenever
/// <c>IssueManager.StartIssueQuest</c> genuinely runs for a Gang Leader Needs Special Weapons issue. Carries
/// the accepting machine's captured <c>_numberOfDaggersRequested</c> - see
/// <see cref="Interfaces.IGangLeaderNeedsSpecialWeaponsIssueInterface"/>'s doc comment.</summary>
public readonly struct GangSpecialWeaponsIssueQuestAcceptTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;
    public readonly int NumberOfDaggersRequested;

    public GangSpecialWeaponsIssueQuestAcceptTriggered(Hero owner, string controllerId, int numberOfDaggersRequested)
    {
        Owner = owner;
        ControllerId = controllerId;
        NumberOfDaggersRequested = numberOfDaggersRequested;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestGangSpecialWeaponsIssueAcceptQuest : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public RequestGangSpecialWeaponsIssueAcceptQuest(string ownerId)
    {
        OwnerId = ownerId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkGangSpecialWeaponsIssueQuestAccepted : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string OwnerControllerId;
    [ProtoMember(3)]
    public readonly int NumberOfDaggersRequested;

    public NetworkGangSpecialWeaponsIssueQuestAccepted(string ownerId, string ownerControllerId, int numberOfDaggersRequested)
    {
        OwnerId = ownerId;
        OwnerControllerId = ownerControllerId;
        NumberOfDaggersRequested = numberOfDaggersRequested;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkGangSpecialWeaponsIssueAcceptRejected : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public NetworkGangSpecialWeaponsIssueAcceptRejected(string ownerId)
    {
        OwnerId = ownerId;
    }
}
