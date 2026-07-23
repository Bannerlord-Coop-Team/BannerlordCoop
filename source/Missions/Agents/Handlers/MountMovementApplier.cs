using Common;
using Common.PacketHandlers;
using Common.Util;
using LiteNetLib;
using Missions.Agents.Packets;
using System.Collections.Generic;
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

    public MountMovementApplier(INetworkAgentRegistry agentRegistry, IAgentPositionInterpolator interpolator)
    {
        this.agentRegistry = agentRegistry;
        this.interpolator = interpolator;
    }

    public PacketType PacketType => PacketType.MountMovement;

    // Nothing to tear down: no threads, no subscriptions; the owning AgentMovementHandler removes this
    // handler's packet-manager registration in its own Dispose.
    public void Dispose() { }

    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        var movement = (MountMovementPacket)packet;
        if (movement.MountIds == null) return;

        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;

            // Resolve and apply the whole batch in ONE game-thread action, matching
            // AgentMovementHandler.HandlePacket.
            var toApply = new List<(Agent horse, AgentMountData data)>();
            for (int i = 0; i < movement.MountIds.Length; i++)
            {
                var mountId = movement.MountIds[i];
                if (agentRegistry.IsLocallyControlled(mountId)) continue;
                if (!agentRegistry.TryGetAgentInfo(mountId, out var mountInfo)) continue;
                toApply.Add((mountInfo.Agent, movement.Mounts[i]));
            }

            if (toApply.Count == 0) return;

            using (new AllowedThread())
            {
                foreach (var (horse, data) in toApply)
                {
                    // The horse may have become invalid (died, mission torn down) between queueing and
                    // running; only apply while it is still active in the current mission.
                    if (horse == null || horse.Mission != Mission.Current || !horse.IsActive())
                        continue;

                    // Re-check authority ON the game thread: a packet from the previous owner can be queued
                    // behind a host-migration adoption, and applying it after the transfer would re-pin the
                    // freshly adopted horse to a stale snapshot.
                    if (agentRegistry.IsLocallyControlled(horse))
                        continue;

                    // The standalone stream is masterless-only. A stale loose-horse packet can arrive after
                    // a rider packet remounts it; drop the direct target so the two interpolators cannot fight.
                    if (horse.RiderAgent is Agent rider && rider.IsActive())
                    {
                        interpolator.Forget(horse);
                        continue;
                    }

                    if (horse.Controller != AgentControllerType.None)
                        horse.Controller = AgentControllerType.None;

                    data.ApplyMount(horse);
                    interpolator.SetMountTarget(horse, data.MountPosition, data.MountMovementDirection);
                }
            }
        });
    }
}
