using GameInterface.Services.Entity;

namespace Missions.Battles;

/// <summary>
/// Shared per-battle context for the components composed by <see cref="CoopBattleController"/>: which battle
/// instance this mission belongs to (the map event's object-manager id), the local controller id, and the
/// host checks every component otherwise re-derives from <see cref="IBattleHostRegistry"/>.
/// </summary>
public interface IBattleSession
{
    /// <summary>The battle's P2P instance id — the map event's object-manager id. Null until the battle is entered.</summary>
    string InstanceId { get; }

    /// <summary>The local client's controller id.</summary>
    string OwnControllerId { get; }

    /// <summary>True once the battle instance has been entered (an instance id is set).</summary>
    bool HasInstance { get; }

    /// <summary>True if this client is the elected host of this battle. False while no instance is set.</summary>
    bool IsLocalHost { get; }

    /// <summary>
    /// Record the battle instance on entry. Returns false (and changes nothing) when the session has already
    /// begun — OpenBattleMission can fire more than once around an encounter, and the battle must connect once.
    /// </summary>
    bool TryBegin(string instanceId);

    /// <summary>True if <paramref name="controllerId"/> is the local controller.</summary>
    bool IsOwn(string controllerId);

    /// <summary>True if <paramref name="controllerId"/> is the recorded host of this battle.</summary>
    bool IsHostController(string controllerId);
}

/// <inheritdoc cref="IBattleSession"/>
public class BattleSession : IBattleSession
{
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IBattleHostRegistry hostRegistry;
    private bool begun;

    public BattleSession(IControllerIdProvider controllerIdProvider, IBattleHostRegistry hostRegistry)
    {
        this.controllerIdProvider = controllerIdProvider;
        this.hostRegistry = hostRegistry;
    }

    public string InstanceId { get; private set; }

    // Pass-through, not a snapshot: the controller id can be assigned after this session is constructed.
    public string OwnControllerId => controllerIdProvider.ControllerId;

    public bool HasInstance => InstanceId != null;

    public bool IsLocalHost => InstanceId != null && hostRegistry.IsHost(InstanceId);

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
