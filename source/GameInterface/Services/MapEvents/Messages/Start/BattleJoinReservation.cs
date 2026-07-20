using Common.Messaging;

namespace GameInterface.Services.MapEvents.Messages.Start;

public readonly struct BattleJoinAccepted : IEvent
{
    public readonly string InstanceId;
    public readonly string ControllerId;

    public BattleJoinAccepted(string instanceId, string controllerId)
    {
        InstanceId = instanceId;
        ControllerId = controllerId;
    }
}

public readonly struct BattleJoinCancelled : IEvent
{
    public readonly string InstanceId;
    public readonly string ControllerId;

    public BattleJoinCancelled(string instanceId, string controllerId)
    {
        InstanceId = instanceId;
        ControllerId = controllerId;
    }
}
