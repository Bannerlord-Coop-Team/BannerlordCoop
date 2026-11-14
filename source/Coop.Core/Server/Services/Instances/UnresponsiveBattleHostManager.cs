using Common.Logging;
using Common.Network;
using Coop.Core.Server.Connections;
using GameInterface.Services.MapEvents;
using LiteNetLib;
using Serilog;

namespace Coop.Core.Server.Services.Instances;

/// <summary>
/// [Server, poll thread] Drops a battle host the server hasn't heard from for <see cref="INetworkConfig.BattleHostDisconnectTimeout"/>,
/// so its successor takes the battle over well before the link's own 60 s timeout. Every other peer keeps that timeout.
/// </summary>
public interface IUnresponsiveBattleHostManager
{
    /// <summary>Disconnects every battle host with a successor waiting that went silent past the battle host timeout.</summary>
    void DisconnectUnresponsiveHosts();
}

/// <inheritdoc cref="IUnresponsiveBattleHostManager"/>
internal class UnresponsiveBattleHostManager : IUnresponsiveBattleHostManager
{
    /// <summary>The reason code a dropped host's client reads, if it is still alive to read it.</summary>
    internal const string DisconnectReason = "BattleHostUnresponsive";

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
        foreach (var (controllerId, instanceId, peer) in missionManager.GetMembers())
        {
            if (peer.ConnectionState != ConnectionState.Connected) continue;
            if (peer.TimeSinceLastPacket < timeoutMs) continue;
            if (!IsHostWithSuccessor(instanceId, controllerId)) continue;

            Logger.Warning("[BattleHost] Disconnecting battle host {Host} of {MapEventId}: nothing heard for {Silence:0.0}s " +
                "(limit {Limit:0}s), so its successor takes over", controllerId, instanceId, peer.TimeSinceLastPacket / 1000f,
                config.BattleHostDisconnectTimeout.TotalSeconds);
            // The same disconnect a drop or a timeout takes, so the departure and promotion that follow are the usual ones.
            peerTerminator.Disconnect(peer, DisconnectReason);
        }
    }

    // A lone host has nobody to hand the battle to, so dropping it early would only cost it the campaign.
    private bool IsHostWithSuccessor(string mapEventId, string controllerId) =>
        hostRegistry.TryGet(mapEventId, out var assignment)
        && assignment.HostControllerId == controllerId
        && assignment.SuccessorControllerIds.Count > 0;
}
