using Common.PacketHandlers;
using System;
using System.IO;
using Vec3 = TaleWorlds.Library.Vec3;

namespace Missions.Agents.Packets;

/// <summary>Packs and restores the rider, deck and mount columns of one datagram in snapshot order.</summary>
public interface IMovementSnapshotCodec
{
    /// <summary>Packs columns and extras up to <paramref name="limit"/> bytes. Returns false for small or oversized batches so the caller can serialize the protobuf arrays.</summary>
    bool TryPack(IPacket packet, int limit, out IPacket packed);

    /// <summary>Restores packed snapshots or returns an unpacked packet unchanged.</summary>
    IPacket Restore(IPacket packet);
}

/// <inheritdoc/>
internal sealed class MovementSnapshotCodec : IMovementSnapshotCodec
{
    // Small batches retain protobuf's sparse encoding. The column layout has little repetition at those sizes.
    private const int MinimumCount = 4;
    // The attachments column, then the rider columns.
    private const int RiderRowBytes = sizeof(byte) + AgentData.SnapshotBytes;
    private const byte HasDeck = 1;
    private const byte HasMount = 2;

    public bool TryPack(IPacket packet, int limit, out IPacket packed)
    {
        packed = packet switch
        {
            MovementPacket movement => PackRiders(movement, limit),
            MountMovementPacket mounts => PackMounts(mounts, limit),
            _ => null,
        };
        return packed != null;
    }

    public IPacket Restore(IPacket packet) => packet switch
    {
        MovementPacket movement when movement.PackedSnapshots != null =>
            movement.WithRestoredSnapshots(RestoreRiders(movement)),
        MountMovementPacket mounts when mounts.PackedSnapshots != null =>
            mounts.WithRestoredSnapshots(RestoreMounts(mounts)),
        _ => packet,
    };

    private static IPacket PackRiders(MovementPacket packet, int limit)
    {
        AgentData[] agents = packet.Agents;
        int count = packet.SnapshotCount;
        if (count < MinimumCount) return null;

        // Skip scanning extras when the rider columns alone exceed the envelope limit.
        if (count > limit / RiderRowBytes) return null;

        var layout = SnapshotLayout.ForRiders(agents, count);
        int packedBytes = layout.TotalBytes;
        if (packedBytes > limit) return null;

        var packed = new byte[packedBytes];
        var agentColumns = layout.AgentColumns(packed);
        var deckColumns = layout.DeckColumns(packed);
        var mountColumns = layout.MountColumns(packed);
        using var extras = layout.ExtrasWriter(packed);
        int deckIndex = 0;
        int mountIndex = 0;
        for (int i = 0; i < count; i++)
        {
            AgentData agent = agents[i];
            bool hasDeck = agent.HasDeckPose;
            AgentMountData mount = agent.MountData;
            byte attachments = 0;
            if (hasDeck) attachments |= HasDeck;
            if (mount != null) attachments |= HasMount;

            agentColumns.BeginSnapshot(i);
            agentColumns.WriteByte(attachments);
            agent.WriteSnapshot(ref agentColumns);

            if (hasDeck)
            {
                deckColumns.BeginSnapshot(deckIndex++);
                agent.WriteDeckSnapshot(ref deckColumns);
            }
            if (mount != null)
            {
                mountColumns.BeginSnapshot(mountIndex++);
                mount.WriteSnapshot(ref mountColumns, extras);
            }
        }
        return packet.WithPackedSnapshots(packed);
    }

    private static IPacket PackMounts(MountMovementPacket packet, int limit)
    {
        AgentMountData[] mounts = packet.Mounts;
        int count = packet.SnapshotCount;
        if (count < MinimumCount) return null;

        // Skip scanning extras when the fixed columns alone exceed the envelope limit.
        if (count > limit / AgentMountData.SnapshotBytes) return null;

        var layout = SnapshotLayout.ForMounts(mounts, count);
        int packedBytes = layout.TotalBytes;
        if (packedBytes > limit) return null;

        var packed = new byte[packedBytes];
        var columns = layout.MountColumns(packed);
        using var extras = layout.ExtrasWriter(packed);
        for (int i = 0; i < count; i++)
        {
            columns.BeginSnapshot(i);
            mounts[i].WriteSnapshot(ref columns, extras);
        }
        return packet.WithPackedSnapshots(packed);
    }

    private static AgentData[] RestoreRiders(MovementPacket packet)
    {
        byte[] packed = packet.PackedSnapshots;
        int count = packet.SnapshotCount;

        var layout = SnapshotLayout.ForPackedRiders(packed, count);
        var agentColumns = layout.AgentColumns(packed);
        var deckColumns = layout.DeckColumns(packed);
        var mountColumns = layout.MountColumns(packed);
        using var extras = layout.ExtrasReader(packed);
        var agents = new AgentData[count];
        int deckIndex = 0;
        int mountIndex = 0;
        for (int i = 0; i < count; i++)
        {
            agentColumns.BeginSnapshot(i);
            byte attachments = agentColumns.ReadByte();

            (int ShipIndex, Vec3 Local) deck = default;
            if ((attachments & HasDeck) != 0)
            {
                deckColumns.BeginSnapshot(deckIndex++);
                deck = AgentData.ReadDeckSnapshot(ref deckColumns);
            }

            AgentMountData mount = null;
            if ((attachments & HasMount) != 0)
            {
                mountColumns.BeginSnapshot(mountIndex++);
                mount = AgentMountData.ReadSnapshot(ref mountColumns, extras);
            }

            agents[i] = AgentData.ReadSnapshot(ref agentColumns, deck, mount);
        }
        return agents;
    }

    private static AgentMountData[] RestoreMounts(MountMovementPacket packet)
    {
        byte[] packed = packet.PackedSnapshots;
        int count = packet.SnapshotCount;

        var layout = SnapshotLayout.ForPackedMounts(count);
        var columns = layout.MountColumns(packed);
        using var extras = layout.ExtrasReader(packed);
        var mounts = new AgentMountData[count];
        for (int i = 0; i < count; i++)
        {
            columns.BeginSnapshot(i);
            mounts[i] = AgentMountData.ReadSnapshot(ref columns, extras);
        }
        return mounts;
    }

    private readonly struct SnapshotLayout
    {
        private readonly int agents;
        private readonly int decks;
        private readonly int mounts;
        private readonly int deckOffset;
        private readonly int mountOffset;
        private readonly int extrasOffset;
        private readonly int extraBytes;

        public int TotalBytes => extrasOffset + extraBytes;

        private SnapshotLayout(int agents, int decks, int mounts, int extraBytes = 0)
        {
            this.agents = agents;
            this.decks = decks;
            this.mounts = mounts;
            deckOffset = agents * RiderRowBytes;
            mountOffset = deckOffset + (decks * AgentData.DeckSnapshotBytes);
            extrasOffset = mountOffset + (mounts * AgentMountData.SnapshotBytes);
            this.extraBytes = extraBytes;
        }

        public static SnapshotLayout ForRiders(AgentData[] agents, int count)
        {
            int decks = 0;
            int mounts = 0;
            int extraBytes = 0;
            for (int i = 0; i < count; i++)
            {
                AgentData agent = agents[i];
                if (agent.HasDeckPose) decks++;
                if (agent.MountData == null) continue;
                mounts++;
                extraBytes += agent.MountData.GetSnapshotExtraBytes();
            }
            return new SnapshotLayout(count, decks, mounts, extraBytes);
        }

        public static SnapshotLayout ForMounts(AgentMountData[] mounts, int count)
        {
            int extraBytes = 0;
            for (int i = 0; i < count; i++)
                extraBytes += mounts[i].GetSnapshotExtraBytes();
            return new SnapshotLayout(agents: 0, decks: 0, mounts: count, extraBytes: extraBytes);
        }

        public static SnapshotLayout ForPackedRiders(byte[] packed, int count)
        {
            // The rider section can be read before the attachment counts are known.
            var columns = new SnapshotLayout(agents: count, decks: 0, mounts: 0).AgentColumns(packed);
            int decks = 0;
            int mounts = 0;
            for (int i = 0; i < count; i++)
            {
                columns.BeginSnapshot(i);
                byte attachments = columns.ReadByte();
                if ((attachments & HasDeck) != 0) decks++;
                if ((attachments & HasMount) != 0) mounts++;
            }
            return new SnapshotLayout(count, decks, mounts);
        }

        public static SnapshotLayout ForPackedMounts(int count) =>
            new SnapshotLayout(agents: 0, decks: 0, mounts: count);

        public MovementSnapshotColumns AgentColumns(byte[] packed) =>
            new MovementSnapshotColumns(packed.AsSpan(0, deckOffset), agents);

        public MovementSnapshotColumns DeckColumns(byte[] packed) =>
            new MovementSnapshotColumns(packed.AsSpan(deckOffset, mountOffset - deckOffset), decks);

        public MovementSnapshotColumns MountColumns(byte[] packed) =>
            new MovementSnapshotColumns(packed.AsSpan(mountOffset, extrasOffset - mountOffset), mounts);

        // Rider-only batches skip the extras streams because only mounts have extras.
        public BinaryWriter ExtrasWriter(byte[] packed)
        {
            if (mounts == 0) return null;
            return new BinaryWriter(new MemoryStream(packed, extrasOffset, extraBytes));
        }

        // The packed buffer ends with the extras, so restore uses the remaining bytes as their length.
        public BinaryReader ExtrasReader(byte[] packed)
        {
            if (mounts == 0) return null;
            return new BinaryReader(new MemoryStream(packed, extrasOffset, packed.Length - extrasOffset));
        }
    }
}
