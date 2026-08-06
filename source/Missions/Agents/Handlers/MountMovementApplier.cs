using Common;
using Common.Logging;
using Common.PacketHandlers;
using LiteNetLib;
using Missions.Agents.Packets;
#if DEBUG
using Missions.Diagnostics;
#endif
using Serilog;
using System;
using TaleWorlds.MountAndBlade;
using AgentControllerType = TaleWorlds.Core.AgentControllerType;

namespace Missions.Agents.Handlers;

/// <summary>
/// Receive side for <see cref="MountMovementPacket"/>: applies another owner's MASTERLESS-horse snapshots to
/// our local copies of those horses, through the same <see cref="AgentMountData.ApplyMount"/> path a ridden
/// horse's pose takes, and feeds the shared position interpolator. Send side lives in
/// <see cref="AgentMovementHandler"/>'s movement tick (one registry pass partitions riders and riderless horses), which
/// also owns this applier's packet-manager registration so the two movement handlers share one lifecycle —
/// this class has no threads or subscriptions of its own.
/// </summary>
public class MountMovementApplier : IPacketHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MountMovementApplier>();

    private readonly INetworkAgentRegistry agentRegistry;
    private readonly IAgentPositionInterpolator interpolator;
    private readonly IPuppetMountStateRepairer puppetMountStateRepairer;
    private readonly Action<Agent, AgentMountData> updateSyntheticTurn;
    private readonly Action<MountMovementPacket> queueMovement;
    private readonly IAgentReplicationValidator replicationValidator;

    public MountMovementApplier(
        INetworkAgentRegistry agentRegistry,
        IAgentPositionInterpolator interpolator,
        IPuppetMountStateRepairer puppetMountStateRepairer,
        Action<Agent, AgentMountData> updateSyntheticTurn,
        Action<MountMovementPacket> queueMovement,
        IAgentReplicationValidator replicationValidator)
    {
        this.agentRegistry = agentRegistry;
        this.interpolator = interpolator;
        this.puppetMountStateRepairer = puppetMountStateRepairer;
        this.updateSyntheticTurn = updateSyntheticTurn;
        this.queueMovement = queueMovement;
        this.replicationValidator = replicationValidator;
    }

    public PacketType PacketType => PacketType.MountMovement;

    // Nothing to tear down: no threads, no subscriptions; the owning AgentMovementHandler removes this
    // handler's packet-manager registration in its own Dispose.
    public void Dispose() { }

    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        var movement = (MountMovementPacket)packet;
        if (!replicationValidator.TryValidate(movement, out string validationFailure))
        {
#if DEBUG
            ClientReplicationDiagnostics.RecordValidationRejection();
#endif
            if (replicationValidator.ShouldLogRejection(out int suppressed))
            {
                Logger.Warning(
                    "Discarding invalid mount movement packet: {Failure} " +
                    "(suppressed since last log: {Suppressed})",
                    validationFailure,
                    suppressed);
            }
            return;
        }

        int idCount = movement.Mounts.Length;
#if DEBUG
        for (int i = 0; i < idCount; i++)
            ClientReplicationDiagnostics.RecordAccepted(movement, i);
#endif
        queueMovement(movement);
    }

    internal void ApplySnapshot(
        string identityScopeId,
        ushort compactId,
        Guid canonicalId,
        bool usesCompactId,
        AgentMountData data)
    {
        CoopAgentInfo mountInfo;
        bool found = usesCompactId
            ? agentRegistry.TryGetAgentInfo(
                identityScopeId, compactId, out mountInfo)
            : agentRegistry.TryGetAgentInfo(canonicalId, out mountInfo);
        if (!found) return;

        Agent horse = mountInfo.Agent;
        if (horse == null || horse.Mission != Mission.Current || !horse.IsActive())
            return;
        if (agentRegistry.IsLocallyControlled(horse))
            return;

        if (horse.RiderAgent is Agent rider && rider.IsActive())
        {
            interpolator.Forget(horse);
            return;
        }

        if (horse.Controller != AgentControllerType.None)
            horse.Controller = AgentControllerType.None;
        puppetMountStateRepairer.PreserveRiderlessPuppet(horse);

        data.ApplyMount(horse);
        updateSyntheticTurn(horse, data);
        interpolator.SetMountTarget(horse, data);
    }
}
