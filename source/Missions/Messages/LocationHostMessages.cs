using Common.Messaging;

namespace Missions.Messages;

/// <summary>
/// [Client, local] The settlement location mission FINISHED LOADING on this client (the local player
/// agent is registered), published by <c>CoopLocationsController</c>. The location host handler
/// requests the election on it (SR-010/SR-013).
/// </summary>
public record LocationMissionReady : IEvent
{
    /// <summary>The location's P2P instance id ("{settlementId}|{locationId}").</summary>
    public string InstanceId { get; }

    public LocationMissionReady(string instanceId)
    {
        InstanceId = instanceId;
    }
}

/// <summary>
/// [Client, local] This client became the confirmed NPC host of a location instance — either by the
/// initial election or by a migration promotion. The population director runs the native spawn pass on
/// it (SR-011/SR-013).
/// </summary>
public record LocationHostAuthorityAcquired : IEvent
{
    public string InstanceId { get; }

    public LocationHostAuthorityAcquired(string instanceId)
    {
        InstanceId = instanceId;
    }
}

/// <summary>
/// [Client, local] This client was PROMOTED to NPC host of a location instance whose previous host
/// departed (SR-014). The authority migrator adopts the previous host's NPC puppets on it.
/// </summary>
public record LocationHostMigrated : IEvent
{
    public string InstanceId { get; }

    /// <summary>The controller whose agents the promoted client must adopt.</summary>
    public string PreviousHostControllerId { get; }

    public LocationHostMigrated(string instanceId, string previousHostControllerId)
    {
        InstanceId = instanceId;
        PreviousHostControllerId = previousHostControllerId;
    }
}
