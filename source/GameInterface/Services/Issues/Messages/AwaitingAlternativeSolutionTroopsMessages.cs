using Common.Messaging;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Issues.Messages;

public readonly struct AwaitingAlternativeSolutionTroopsDepositedLocally : IEvent
{
    public readonly string OwnerControllerId;
    public readonly TroopRoster Troops;

    public AwaitingAlternativeSolutionTroopsDepositedLocally(string ownerControllerId, TroopRoster troops)
    {
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
    public readonly TroopRosterData Troops;

    public RequestAwaitingAlternativeSolutionTroopsDeposit(TroopRosterData troops)
    {
        Troops = troops;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestAwaitingAlternativeSolutionTroopsDrain : ICommand
{
}
