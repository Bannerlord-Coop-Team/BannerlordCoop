#if DEBUG
using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;
using System.Linq;

namespace GameInterface.Services.MapEvents.NavalScenarios;

/// <summary>
/// DEBUG, server: a scenario player that reconnects while the scenario's battle runs lost its scenario with its session,
/// so it gets the scenario again with its disconnect done and re-enters the battle as a late joiner (D4).
/// </summary>
internal class NavalScenarioRejoinHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalScenarioRejoinHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INavalScenarioState state;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IObjectManager objectManager;

    public NavalScenarioRejoinHandler(
        IMessageBroker messageBroker,
        INavalScenarioState state,
        INetwork network,
        IPlayerManager playerManager,
        IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.state = state;
        this.network = network;
        this.playerManager = playerManager;
        this.objectManager = objectManager;

        messageBroker.Subscribe<PlayerReconnectedToMapEvent>(Handle_PlayerReconnectedToMapEvent);
    }

    // Drops the subscription.
    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerReconnectedToMapEvent>(Handle_PlayerReconnectedToMapEvent);
    }

    // [Server, game thread] Re-sends the running disconnect scenario to the player that came back into its map event.
    private void Handle_PlayerReconnectedToMapEvent(MessagePayload<PlayerReconnectedToMapEvent> payload)
    {
        if (ModInformation.IsClient) return;

        var reconnected = payload.What;
        var config = state.Current;
        if (!IsRejoinOf(config, reconnected.ControllerId, MapEventIdOf(reconnected))) return;
        if (reconnected.MapEvent.IsFinalized || !playerManager.TryGetPeer(reconnected.ControllerId, out var peer)) return;

        network.Send(peer, new NetworkNavalScenarioStarted(config, rejoining: true));
        Logger.Information("[NavalScenario] {ControllerId} reconnected into {MapEventId}; re-sent {Preset} {ScenarioId} to re-enter it",
            reconnected.ControllerId, config.MapEventId, config.Preset, config.ScenarioId);
    }

    // The registry id of the map event the player came back into, or null.
    private string MapEventIdOf(PlayerReconnectedToMapEvent reconnected) =>
        reconnected.MapEvent != null && objectManager.TryGetId(reconnected.MapEvent, out string mapEventId) ? mapEventId : null;

    // Whether a reconnect into this map event belongs to a player of the running disconnect scenario.
    internal static bool IsRejoinOf(NavalScenarioConfig config, string controllerId, string mapEventId) =>
        config != null && config.DisconnectBattleHost && !string.IsNullOrEmpty(mapEventId) && mapEventId == config.MapEventId
        && config.ControllerIds.Contains(controllerId);
}
#endif
