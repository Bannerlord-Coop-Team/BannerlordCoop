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
        [ProtoMember(2, IsPacked = true)]
        public ushort[] MountIds { get; }
        [ProtoMember(3)]
        public AgentMountData[] Mounts { get; }
        [ProtoMember(4)]
        public Guid[] MountGuids { get; }

        // Count the ids because the snapshot array is null while packed.
        internal int SnapshotCount => MountIds?.Length ?? MountGuids?.Length ?? 0;

        [ProtoMember(5)]
        public string SenderControllerId { get; }

        [ProtoMember(6, IsPacked = true)]
        public long[] AuthorityRevisions { get; }

        // All snapshots in a packet come from the same sender capture, including split batches.
        [ProtoMember(7)]
        public long SampleSequence { get; }

        /// <summary>Column-packed snapshots inside a <see cref="CompressedMovementPacket"/>. <see cref="Mounts"/> is null until restored.</summary>
        [ProtoMember(8)]
        internal byte[] PackedSnapshots { get; }

        public MountMovementPacket(string identityScopeId, ushort[] mountIds, AgentMountData[] mounts, string senderControllerId = null, long[] authorityRevisions = null, long sampleSequence = 0)
        {
            SampleSequence = sampleSequence;
            SenderControllerId = senderControllerId;
            AuthorityRevisions = authorityRevisions;
            IdentityScopeId = identityScopeId;
            MountIds = mountIds;
            Mounts = mounts;
            MountGuids = null;
            PackedSnapshots = null;
        }

        public MountMovementPacket(Guid[] mountGuids, AgentMountData[] mounts, string senderControllerId = null, long[] authorityRevisions = null, long sampleSequence = 0)
        {
            SampleSequence = sampleSequence;
            SenderControllerId = senderControllerId;
            AuthorityRevisions = authorityRevisions;
            IdentityScopeId = null;
            MountIds = null;
            MountGuids = mountGuids;
            Mounts = mounts;
            PackedSnapshots = null;
        }

        private MountMovementPacket(MountMovementPacket packet, AgentMountData[] mounts, byte[] packedSnapshots)
        {
            this = packet;
            Mounts = mounts;
            PackedSnapshots = packedSnapshots;
        }

        private MountMovementPacket(MountMovementPacket packet, long[] authorityRevisions)
        {
            this = packet;
            AuthorityRevisions = authorityRevisions;
        }

        internal MountMovementPacket WithPackedSnapshots(byte[] snapshots) => new MountMovementPacket(this, null, snapshots);
        internal MountMovementPacket WithRestoredSnapshots(AgentMountData[] mounts) => new MountMovementPacket(this, mounts, null);

        internal MountMovementPacket WithAuthorityRevisions(long[] revisions) => new MountMovementPacket(this, revisions);
    }
}
