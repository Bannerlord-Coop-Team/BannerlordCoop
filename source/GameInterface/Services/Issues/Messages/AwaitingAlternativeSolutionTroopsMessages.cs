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

    public AwaitingAlternativeSolutionTroopsDrainedLocally(string ownerControllerId)
    {
        OwnerControllerId = ownerControllerId;
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
