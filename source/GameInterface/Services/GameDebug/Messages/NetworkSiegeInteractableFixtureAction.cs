using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.GameDebug.Messages;

public enum SiegeInteractableFixtureAction
{
    Capture,
    Prepare,
    Use,
    Stop,
    Restore,
}

[ProtoContract(SkipConstructor = true)]
public record NetworkSiegeInteractableFixtureAction : ICommand
{
    [ProtoMember(1)] public string ControllerId { get; }
    [ProtoMember(2)] public int MachineId { get; }
    [ProtoMember(3)] public SiegeInteractableFixtureAction Action { get; }
    [ProtoMember(4)] public int OriginalGateState { get; }
    [ProtoMember(5)] public string MachineType { get; }

    public NetworkSiegeInteractableFixtureAction(
        string controllerId,
        int machineId,
        SiegeInteractableFixtureAction action,
        int originalGateState,
        string machineType)
    {
        ControllerId = controllerId;
        MachineId = machineId;
        Action = action;
        OriginalGateState = originalGateState;
        MachineType = machineType;
    }
}

[ProtoContract(SkipConstructor = true)]
public record NetworkSiegeInteractableFixtureReport : IEvent
{
    [ProtoMember(1)] public string ControllerId { get; }
    [ProtoMember(2)] public int MachineId { get; }
    [ProtoMember(3)] public SiegeInteractableFixtureAction Action { get; }
    [ProtoMember(4)] public bool Success { get; }
    [ProtoMember(5)] public int EligiblePoints { get; }
    [ProtoMember(6)] public bool CurrentlyUsing { get; }
    [ProtoMember(7)] public int GateState { get; }
    [ProtoMember(8)] public bool SimulatedLocally { get; }
    [ProtoMember(9)] public string Error { get; }

    public NetworkSiegeInteractableFixtureReport(
        string controllerId,
        int machineId,
        SiegeInteractableFixtureAction action,
        bool success,
        int eligiblePoints,
        bool currentlyUsing,
        int gateState,
        bool simulatedLocally,
        string error)
    {
        ControllerId = controllerId;
        MachineId = machineId;
        Action = action;
        Success = success;
        EligiblePoints = eligiblePoints;
        CurrentlyUsing = currentlyUsing;
        GateState = gateState;
        SimulatedLocally = simulatedLocally;
        Error = error;
    }
}
