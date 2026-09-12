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
/// it (SR-011/SR-013); <see cref="WasMigration"/> is the EXPLICIT promotion signal so the director
/// never has to infer it from held-puppet state (which is empty when the promoted client had not
/// applied its catch-up yet).
/// </summary>
public record LocationHostAuthorityAcquired : IEvent
{
    public string InstanceId { get; }

    /// <summary>True when this authority came from a migration promotion (a previous host existed
    /// and departed), false for the initial election of a fresh instance.</summary>
    public bool WasMigration { get; }

    public LocationHostAuthorityAcquired(string instanceId, bool wasMigration = false)
    {
        InstanceId = instanceId;
        WasMigration = wasMigration;
    }
}

/// <summary>
/// [Client, local] A location instance moved to a new NPC host. Every peer updates registry authority;
/// the promoted host also adopts the previous host's NPC puppets.
/// </summary>
public record LocationHostMigrated : IEvent
{
    public string InstanceId { get; }

    /// <summary>The controller whose agents move to the new host.</summary>
    public string PreviousHostControllerId { get; }
    public string NewHostControllerId { get; }
    public long AuthorityRevision { get; }

    public LocationHostMigrated(
        string instanceId,
        string previousHostControllerId,
        string newHostControllerId,
        long authorityRevision)
    {
        InstanceId = instanceId;
        PreviousHostControllerId = previousHostControllerId;
        NewHostControllerId = newHostControllerId;
        AuthorityRevision = authorityRevision;
    }
}
