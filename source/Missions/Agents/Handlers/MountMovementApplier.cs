using Common;
using Common.PacketHandlers;
using LiteNetLib;
using Missions.Agents.Packets;
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
    private readonly INetworkAgentRegistry agentRegistry;
    private readonly IAgentPositionInterpolator interpolator;
    private readonly IPuppetMountStateRepairer puppetMountStateRepairer;
    private readonly Action<Agent, AgentMountData> updateSyntheticTurn;
    private readonly Action<MountMovementPacket> queueMovement;

    public MountMovementApplier(
        INetworkAgentRegistry agentRegistry,
        IAgentPositionInterpolator interpolator,
        IPuppetMountStateRepairer puppetMountStateRepairer,
        Action<Agent, AgentMountData> updateSyntheticTurn,
        Action<MountMovementPacket> queueMovement)
    {
        this.agentRegistry = agentRegistry;
        this.interpolator = interpolator;
        this.puppetMountStateRepairer = puppetMountStateRepairer;
        this.updateSyntheticTurn = updateSyntheticTurn;
        this.queueMovement = queueMovement;
    }

    public PacketType PacketType => PacketType.MountMovement;

    // Nothing to tear down: no threads, no subscriptions; the owning AgentMovementHandler removes this
    // handler's packet-manager registration in its own Dispose.
    public void Dispose() { }

    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        queueMovement((MountMovementPacket)packet);
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
            ? agentRegistry.TryGetAgentInfo(identityScopeId, compactId, out mountInfo)
            : agentRegistry.TryGetAgentInfo(canonicalId, out mountInfo);
        if (!found) return;

        Agent horse = mountInfo.Agent;
        if (horse == null || horse.Mission != Mission.Current || !horse.IsActive())
            return;
        if (agentRegistry.IsLocallyControlled(horse))
            return;

        // A stale loose-horse packet can arrive after a rider packet remounts it. Drop the direct target so
        // the rider and masterless-mount interpolators cannot fight.
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
