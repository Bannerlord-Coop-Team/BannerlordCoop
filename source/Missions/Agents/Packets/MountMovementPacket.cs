using Common.PacketHandlers;
using LiteNetLib;
using ProtoBuf;
using System;

namespace Missions.Agents.Packets
{
    /// <summary>
    /// A batch of MASTERLESS-horse movement snapshots for one poll tick. A ridden horse's pose rides inside
    /// its rider's <see cref="AgentData"/> (as <see cref="AgentMountData"/>); a masterless registered horse
    /// has no rider stream, so its owner broadcasts the SAME shape standalone, keyed by the horse's own
    /// registry id — one wire representation of horse pose either way, applied by the one
    /// <see cref="AgentMountData.ApplyMount"/> path.
    /// </summary>
    [ProtoContract]
    public readonly struct MountMovementPacket : IPacket
    {
        public DeliveryMethod DeliveryMethod => DeliveryMethod.Unreliable;

        public PacketType PacketType => PacketType.MountMovement;

        [ProtoMember(1)]
        public string IdentityScopeId { get; }
        [ProtoMember(2)]
        public ushort[] MountIds { get; }
        [ProtoMember(3)]
        public AgentMountData[] Mounts { get; }
        [ProtoMember(4)]
        public Guid[] MountGuids { get; }

        public MountMovementPacket(string identityScopeId, ushort[] mountIds, AgentMountData[] mounts)
        {
            IdentityScopeId = identityScopeId;
            MountIds = mountIds;
            Mounts = mounts;
            MountGuids = null;
        }

        public MountMovementPacket(Guid[] mountGuids, AgentMountData[] mounts)
        {
            IdentityScopeId = null;
            MountIds = null;
            MountGuids = mountGuids;
            Mounts = mounts;
        }
    }
}
