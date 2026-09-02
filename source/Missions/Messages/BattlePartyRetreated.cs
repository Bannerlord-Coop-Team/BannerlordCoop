using Common.Messaging;

namespace Missions.Messages;

/// <summary>
/// [Server, local] A verified controller deliberately withdrew from an unresolved battle.
/// Network handlers must authenticate the controller before publishing this event.
/// </summary>
public readonly struct BattlePartyRetreated : IEvent
{
    public readonly string ControllerId;
    public readonly string InstanceId;

    public BattlePartyRetreated(string controllerId, string instanceId)
    {
        ControllerId = controllerId;
        InstanceId = instanceId;
    }
}