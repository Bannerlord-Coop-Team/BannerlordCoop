using GameInterface.Services.Entity;
using GameInterface.Services.Locations.Hosting;

namespace Missions.Locations;

/// <summary>
/// Shared per-mission context for the location NPC host components composed by
/// <c>CoopLocationsController</c>: which location instance this mission belongs to
/// ("{settlementId}|{locationId}"), the local controller id, and the host checks every component
/// otherwise re-derives from <see cref="ILocationHostRegistry"/>. Mirrors the battle
/// <c>IBattleSession</c>.
/// </summary>
public interface ILocationSession
{
    /// <summary>The location's P2P instance id. Null until the location is entered.</summary>
    string InstanceId { get; }

    /// <summary>The local client's controller id.</summary>
    string OwnControllerId { get; }

    /// <summary>True once the location instance has been entered (an instance id is set).</summary>
    bool HasInstance { get; }

    /// <summary>True if this client is the elected NPC host of this location mission. False while no instance is set.</summary>
    bool IsLocalHost { get; }

    /// <summary>The current NPC host controller id, or null before assignment.</summary>
    string HostControllerId { get; }

    /// <summary>
    /// The epoch of the current host assignment for this instance (SR-016): the server issues 1 at the
    /// election and +1 per host change. 0 while no instance is set or no assignment has been received yet.
    /// </summary>
    int HostEpoch { get; }

    /// <summary>
    /// Record the location instance on entry. Returns false (and changes nothing) when the session has
    /// already begun — the entry patches can fire more than once per visit, and the mission must connect once.
    /// </summary>
    bool TryBegin(string instanceId);

    /// <summary>True if <paramref name="controllerId"/> is the local controller.</summary>
    bool IsOwn(string controllerId);

    /// <summary>True if <paramref name="controllerId"/> is the recorded host of this location mission.</summary>
    bool IsHostController(string controllerId);
}

/// <inheritdoc cref="ILocationSession"/>
public class LocationSession : ILocationSession
{
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly ILocationHostRegistry hostRegistry;
    private bool begun;

    public LocationSession(IControllerIdProvider controllerIdProvider, ILocationHostRegistry hostRegistry)
    {
        this.controllerIdProvider = controllerIdProvider;
        this.hostRegistry = hostRegistry;
    }

    public string InstanceId { get; private set; }

    // Pass-through, not a snapshot: the controller id can be assigned after this session is constructed.
    public string OwnControllerId => controllerIdProvider.ControllerId;

    public bool HasInstance => InstanceId != null;

    public bool IsLocalHost => InstanceId != null && hostRegistry.IsHost(InstanceId);

    public string HostControllerId => InstanceId != null
        && hostRegistry.TryGet(InstanceId, out var assignment)
            ? assignment.HostControllerId
            : null;

    public int HostEpoch => InstanceId != null && hostRegistry.TryGet(InstanceId, out var assignment)
        ? assignment.Epoch
        : 0;

    public bool TryBegin(string instanceId)
    {
        if (begun) return false;
        begun = true;
        InstanceId = instanceId;
        return true;
    }

    public bool IsOwn(string controllerId) => controllerId == OwnControllerId;

    public bool IsHostController(string controllerId)
        => InstanceId != null
           && hostRegistry.TryGet(InstanceId, out var assignment)
           && assignment.HostControllerId == controllerId;
}
