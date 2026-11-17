using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// [Server, game thread] The naval departure rules (D4, R41a); each answers false for a land battle. Whether a host is kept
/// lives on its assignment (<see cref="BattleHostAssignment.KeptAfterResult"/>), set by the host succession.
/// </summary>
public interface IBattleHostRetentionPolicy
{
    /// <summary>A disconnected member withdraws its party like a leave, instead of leaving it to the host to adopt.</summary>
    bool DisconnectWithdraws(string mapEventId);

    /// <summary>The host stays host when it leaves after it reported a result, or after the map event applied one.</summary>
    bool KeepsHostAfterResult(string mapEventId, bool hasResult);

    /// <summary>The host's own finalize waits for the server's conclusion, which it would otherwise drop.</summary>
    bool DefersHostFinalize(string mapEventId);

    /// <summary>A host kept after its result may not enter again, as that would reset the result it reported.</summary>
    bool RefusesEntry(string mapEventId, string controllerId);
}

/// <inheritdoc cref="IBattleHostRetentionPolicy"/>
public class BattleHostRetentionPolicy : IBattleHostRetentionPolicy
{
    private readonly IObjectManager objectManager;
    private readonly IBattleHostRegistry hostRegistry;

    public BattleHostRetentionPolicy(IObjectManager objectManager, IBattleHostRegistry hostRegistry)
    {
        this.objectManager = objectManager;
        this.hostRegistry = hostRegistry;
    }

    public bool DisconnectWithdraws(string mapEventId) => IsNaval(mapEventId);

    public bool KeepsHostAfterResult(string mapEventId, bool hasResult) => hasResult && IsNaval(mapEventId);

    public bool DefersHostFinalize(string mapEventId) =>
        objectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent)
        && mapEvent.IsNavalMapEvent && mapEvent.BattleState == BattleState.None;

    public bool RefusesEntry(string mapEventId, string controllerId) =>
        !string.IsNullOrEmpty(mapEventId) && hostRegistry.TryGet(mapEventId, out var assignment)
        && assignment.KeptAfterResult && assignment.HostControllerId == controllerId;

    // A battle at sea, read from its map event or, once that is gone, from the election's record on the host assignment.
    private bool IsNaval(string mapEventId)
    {
        if (string.IsNullOrEmpty(mapEventId)) return false;
        if (objectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent)) return mapEvent.IsNavalMapEvent;

        return hostRegistry.TryGet(mapEventId, out var assignment) && assignment.IsNaval;
    }
}
