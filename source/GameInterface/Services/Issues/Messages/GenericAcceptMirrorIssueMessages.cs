using Common.Messaging;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Messages;

public readonly struct GenericIssueQuestAcceptTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;

    public GenericIssueQuestAcceptTriggered(Hero owner, string controllerId)
    {
        Owner = owner;
        ControllerId = controllerId;
    }
}

public readonly struct GenericIssueAlternativeAcceptTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;

    public GenericIssueAlternativeAcceptTriggered(Hero owner, string controllerId)
    {
        Owner = owner;
        ControllerId = controllerId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestGenericIssueAcceptQuest : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly int Generation;

    public RequestGenericIssueAcceptQuest(string ownerId, int generation)
    {
        OwnerId = ownerId;
        Generation = generation;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkGenericIssueQuestAccepted : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string OwnerControllerId;

    public NetworkGenericIssueQuestAccepted(string ownerId, string ownerControllerId)
    {
        OwnerId = ownerId;
        OwnerControllerId = ownerControllerId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestGenericIssueAcceptAlternative : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly int Generation;
    [ProtoMember(3)]
    public readonly TroopRosterData SentTroops;

    public RequestGenericIssueAcceptAlternative(string ownerId, int generation, TroopRosterData sentTroops)
    {
        OwnerId = ownerId;
        Generation = generation;
        SentTroops = sentTroops;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkGenericIssueAlternativeAccepted : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string OwnerControllerId;
    [ProtoMember(3)]
    public readonly AlternativeSolutionVanillaState State;
    [ProtoMember(4)]
    public readonly TroopRosterData SentTroops;

    public NetworkGenericIssueAlternativeAccepted(string ownerId, string ownerControllerId, AlternativeSolutionVanillaState state, TroopRosterData sentTroops)
    {
        OwnerId = ownerId;
        OwnerControllerId = ownerControllerId;
        State = state;
        SentTroops = sentTroops;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkGenericIssueAcceptRejected : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public NetworkGenericIssueAcceptRejected(string ownerId)
    {
        OwnerId = ownerId;
    }
}
