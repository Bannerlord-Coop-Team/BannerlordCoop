using Common.PacketHandlers;
using System;
using System.IO;

namespace Missions.Agents.Packets;

/// <summary>Packs and restores the rider, deck and mount columns of one datagram in snapshot order.</summary>
internal static class MovementSnapshotPacking
{
    // Small batches retain protobuf's sparse encoding. The column layout has little repetition at those sizes.
    private const int MinimumCount = 4;
    private const int AgentSnapshotBytes = sizeof(byte) + AgentData.SnapshotBytes;
    private const byte HasDeck = 1;
    private const byte HasMount = 2;

    public static IPacket TryPack(IPacket packet, int limit)
    {
        switch (packet)
        {
            case MovementPacket movement:
                byte[] riders = PackRiders(movement, limit);
                return riders == null ? null : movement.WithPackedSnapshots(riders);
            case MountMovementPacket mounts:
                byte[] horses = PackMounts(mounts, limit);
                return horses == null ? null : mounts.WithPackedSnapshots(horses);
            default:
                return null;
        }
    }

    public static IPacket Restore(IPacket packet) => packet switch
    {
        MovementPacket movement when movement.PackedSnapshots != null =>
            movement.WithRestoredSnapshots(RestoreRiders(movement)),
        MountMovementPacket mounts when mounts.PackedSnapshots != null =>
            mounts.WithRestoredSnapshots(RestoreMounts(mounts)),
        _ => packet,
    };

    private static byte[] PackRiders(MovementPacket packet, int limit)
    {
        AgentData[] agents = packet.Agents;
        int count = packet.SnapshotCount;
        if (count < MinimumCount || count > limit / AgentSnapshotBytes || agents?.Length != count)
            return null;

        int decks = 0;
        int mounts = 0;
        int extraBytes = 0;
        foreach (AgentData agent in agents)
        {
            if (agent.DeckShipIndex != 0) decks++;
            if (agent.MountData == null) continue;
            mounts++;
            extraBytes += agent.MountData.GetSnapshotExtraBytes();
        }

        var layout = new SnapshotLayout(count, decks, mounts);
        if (extraBytes > limit - layout.ExtrasOffset) return null;

        var packed = new byte[layout.ExtrasOffset + extraBytes];
        var agentColumns = layout.AgentColumns(packed);
        var deckColumns = layout.DeckColumns(packed);
        var mountColumns = layout.MountColumns(packed);
        using var extras = mounts == 0 ? null : new BinaryWriter(new MemoryStream(packed, layout.ExtrasOffset, extraBytes));
        int deckIndex = 0;
        int mountIndex = 0;
        for (int i = 0; i < count; i++)
        {
            AgentData agent = agents[i];
            bool hasDeck = agent.DeckShipIndex != 0;
            AgentMountData mount = agent.MountData;
            byte attachments = hasDeck ? HasDeck : (byte)0;
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
        return packed;
    }

    private static byte[] PackMounts(MountMovementPacket packet, int limit)
    {
        AgentMountData[] mounts = packet.Mounts;
        int count = packet.SnapshotCount;
        if (count < MinimumCount || count > limit / AgentMountData.SnapshotBytes || mounts?.Length != count)
            return null;

        var layout = new SnapshotLayout(0, 0, count);
        int extraBytes = 0;
        foreach (AgentMountData mount in mounts)
            extraBytes += mount.GetSnapshotExtraBytes();
        if (extraBytes > limit - layout.ExtrasOffset) return null;

        var packed = new byte[layout.ExtrasOffset + extraBytes];
        var columns = layout.MountColumns(packed);
        using var extras = new BinaryWriter(new MemoryStream(packed, layout.ExtrasOffset, extraBytes));
        for (int i = 0; i < count; i++)
        {
            columns.BeginSnapshot(i);
            mounts[i].WriteSnapshot(ref columns, extras);
        }
        return packed;
    }

    private static AgentData[] RestoreRiders(MovementPacket packet)
    {
        byte[] packed = packet.PackedSnapshots;
        int count = packet.SnapshotCount;

        // The first column identifies the rows with deck or mount state, which are stored densely.
        int decks = 0;
        int mounts = 0;
        foreach (byte attachments in packed.AsSpan(0, count))
        {
            if ((attachments & HasDeck) != 0) decks++;
            if ((attachments & HasMount) != 0) mounts++;
        }

        var layout = new SnapshotLayout(count, decks, mounts);
        var agentColumns = layout.AgentColumns(packed);
        var deckColumns = layout.DeckColumns(packed);
        var mountColumns = layout.MountColumns(packed);
        using var extras = mounts == 0 ? null : new BinaryReader(new MemoryStream(packed, layout.ExtrasOffset, packed.Length - layout.ExtrasOffset));
        var agents = new AgentData[count];
        int deckIndex = 0;
        int mountIndex = 0;
        for (int i = 0; i < count; i++)
        {
            agentColumns.BeginSnapshot(i);
            byte attachments = agentColumns.ReadByte();
            AgentData agent = AgentData.ReadSnapshot(ref agentColumns);
            if ((attachments & HasDeck) != 0)
            {
                deckColumns.BeginSnapshot(deckIndex++);
                agent.ReadDeckSnapshot(ref deckColumns);
            }
            if ((attachments & HasMount) != 0)
            {
                mountColumns.BeginSnapshot(mountIndex++);
                agent.SetSnapshotMount(AgentMountData.ReadSnapshot(ref mountColumns, extras));
            }
            agents[i] = agent;
        }
        return agents;
    }

    private static AgentMountData[] RestoreMounts(MountMovementPacket packet)
    {
        byte[] packed = packet.PackedSnapshots;
        int count = packet.SnapshotCount;

        var layout = new SnapshotLayout(0, 0, count);
        var columns = layout.MountColumns(packed);
        using var extras = new BinaryReader(new MemoryStream(packed, layout.ExtrasOffset, packed.Length - layout.ExtrasOffset));
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

        public int ExtrasOffset { get; }

        public SnapshotLayout(int agents, int decks, int mounts)
        {
            this.agents = agents;
            this.decks = decks;
            this.mounts = mounts;
            deckOffset = agents * AgentSnapshotBytes;
            mountOffset = deckOffset + (decks * AgentData.DeckSnapshotBytes);
            ExtrasOffset = mountOffset + (mounts * AgentMountData.SnapshotBytes);
        }

        public MovementSnapshotColumns AgentColumns(byte[] packed) =>
            new MovementSnapshotColumns(packed.AsSpan(0, deckOffset), agents);

        public MovementSnapshotColumns DeckColumns(byte[] packed) =>
            new MovementSnapshotColumns(packed.AsSpan(deckOffset, mountOffset - deckOffset), decks);

        public MovementSnapshotColumns MountColumns(byte[] packed) =>
            new MovementSnapshotColumns(packed.AsSpan(mountOffset, ExtrasOffset - mountOffset), mounts);
    }
}
