using Common.Messaging;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Issues.Messages;

public readonly struct AwaitingAlternativeSolutionTroopsDepositedLocally : IEvent
{
    public readonly Hero IssueOwner;
    public readonly string OwnerControllerId;
    public readonly TroopRoster Troops;

    public AwaitingAlternativeSolutionTroopsDepositedLocally(Hero issueOwner, string ownerControllerId, TroopRoster troops)
    {
        IssueOwner = issueOwner;
        OwnerControllerId = ownerControllerId;
        Troops = troops;
    }
}

public readonly struct AwaitingAlternativeSolutionTroopsDrainedLocally : IEvent
{
    public readonly string OwnerControllerId;
    public readonly TroopRoster Troops;

    public AwaitingAlternativeSolutionTroopsDrainedLocally(string ownerControllerId, TroopRoster troops)
    {
        OwnerControllerId = ownerControllerId;
        Troops = troops;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestAwaitingAlternativeSolutionTroopsDeposit : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly TroopRosterData Troops;

    public RequestAwaitingAlternativeSolutionTroopsDeposit(string ownerId, TroopRosterData troops)
    {
        OwnerId = ownerId;
        Troops = troops;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestAwaitingAlternativeSolutionTroopsDrain : ICommand
{
    [ProtoMember(1)]
    public readonly TroopRosterData Troops;

    [ProtoMember(2)]
    public readonly string Revision;

    public RequestAwaitingAlternativeSolutionTroopsDrain(TroopRosterData troops, string revision)
    {
        Troops = troops;
        Revision = revision;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAwaitingAlternativeSolutionTroopsDepositRejected : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public NetworkAwaitingAlternativeSolutionTroopsDepositRejected(string ownerId)
    {
        OwnerId = ownerId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly TroopRosterData Troops;

    [ProtoMember(3)]
    public readonly string Revision;

    public NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed(string ownerId, TroopRosterData troops, string revision)
    {
        OwnerId = ownerId;
        Troops = troops;
        Revision = revision;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAwaitingAlternativeSolutionTroopsDrainConfirmed : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly TroopRosterData Troops;

    [ProtoMember(2)]
    public readonly string Revision;

    public NetworkAwaitingAlternativeSolutionTroopsDrainConfirmed(TroopRosterData troops, string revision)
    {
        Troops = troops;
        Revision = revision;
    }
}
