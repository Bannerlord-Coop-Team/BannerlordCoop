using Common.Logging;
using Common.Network;
using Coop.Core.Server.Connections;
using GameInterface.Services.MapEvents;
using LiteNetLib;
using Serilog;
using Coop.Core.Common.Network;

namespace Coop.Core.Server.Services.Instances;

/// <summary>
/// [Server, poll thread] Drops a battle host the server hasn't heard from for <see cref="INetworkConfig.BattleHostDisconnectTimeout"/>,
/// so its successor takes the battle over well before <see cref="INetworkConfig.DisconnectTimeout"/>. Every other peer keeps that one.
/// </summary>
public interface IUnresponsiveBattleHostManager
{
    /// <summary>Disconnects every battle host with a successor waiting that went silent past the battle host timeout.</summary>
    void DisconnectUnresponsiveHosts();
}

/// <inheritdoc cref="IUnresponsiveBattleHostManager"/>
internal class UnresponsiveBattleHostManager : IUnresponsiveBattleHostManager
{
    private static readonly ILogger Logger = LogManager.GetLogger<UnresponsiveBattleHostManager>();

    private readonly INetworkConfig config;
    private readonly IMissionManager missionManager;
    private readonly IBattleHostRegistry hostRegistry;
    private readonly IPeerTerminator peerTerminator;

    public UnresponsiveBattleHostManager(
        INetworkConfig config,
        IMissionManager missionManager,
        IBattleHostRegistry hostRegistry,
        IPeerTerminator peerTerminator)
    {
        this.config = config;
        this.missionManager = missionManager;
        this.hostRegistry = hostRegistry;
        this.peerTerminator = peerTerminator;
    }

    /// <inheritdoc/>
    public void DisconnectUnresponsiveHosts()
    {
        double timeoutMs = config.BattleHostDisconnectTimeout.TotalMilliseconds;
        // A battle's mission instance is named after its map event.
        foreach (var (controllerId, mapEventId, peer) in missionManager.GetMembers())
        {
            if (peer.ConnectionState != ConnectionState.Connected) continue;
            if (peer.TimeSinceLastPacket < timeoutMs) continue;
            if (!IsHostWithSuccessor(mapEventId, controllerId)) continue;

            Logger.Warning("[BattleHost] Disconnecting battle host {Host} of {MapEventId}: nothing heard for {Silence:0.0}s " +
                "(limit {Limit:0}s), so its successor takes over", controllerId, mapEventId, peer.TimeSinceLastPacket / 1000f,
                config.BattleHostDisconnectTimeout.TotalSeconds);
            // The same disconnect a drop or a timeout takes, so the departure and promotion that follow are the usual ones.
            peerTerminator.Disconnect(peer, DisconnectReasons.BattleHostUnresponsive);
        }
    }

    // A lone host has nobody to hand the battle to, so dropping it early would only cost it the campaign.
    private bool IsHostWithSuccessor(string mapEventId, string controllerId) =>
        hostRegistry.TryGet(mapEventId, out var assignment)
        && assignment.HostControllerId == controllerId
        && assignment.SuccessorControllerIds.Count > 0;
}
