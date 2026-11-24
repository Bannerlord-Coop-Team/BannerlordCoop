using System;
using System.IO;

namespace Missions.Agents.Packets;

/// <summary>Writes the rider, deck and mount columns of one datagram in snapshot order.</summary>
internal static class MovementSnapshotPacking
{
    // Small batches retain protobuf's sparse encoding. The column layout has little repetition at those sizes.
    private const int MinimumCount = 4;
    private const int AgentSnapshotBytes = sizeof(byte) + AgentData.SnapshotBytes;
    private const byte HasDeck = 1;
    private const byte HasMount = 2;

    public static byte[] Pack(MovementPacket packet, int limit)
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
            byte attachments = 0;
            if (agent.DeckShipIndex != 0)
            {
                attachments |= HasDeck;
                deckColumns.BeginSnapshot(deckIndex++);
                agent.WriteDeckSnapshot(ref deckColumns);
            }
            if (agent.MountData != null)
            {
                attachments |= HasMount;
                mountColumns.BeginSnapshot(mountIndex++);
                agent.MountData.WriteSnapshot(ref mountColumns, extras);
            }

            packed[i] = attachments;
            agentColumns.BeginSnapshot(i);
            agent.WriteSnapshot(ref agentColumns);
        }
        return packed;
    }

    public static byte[] Pack(MountMovementPacket packet, int limit)
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

    public static AgentData[] Restore(MovementPacket packet)
    {
        byte[] packed = packet.PackedSnapshots;
        int count = packet.SnapshotCount;
        if (count == 0 || count > packed.Length / AgentSnapshotBytes)
            throw new InvalidDataException("Packed movement has no snapshots for its ids.");

        // The first column identifies the rows with deck or mount state, which are stored densely.
        int decks = 0;
        int mounts = 0;
        for (int i = 0; i < count; i++)
        {
            if ((packed[i] & HasDeck) != 0) decks++;
            if ((packed[i] & HasMount) != 0) mounts++;
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
            byte attachments = packed[i];
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

    public static AgentMountData[] Restore(MountMovementPacket packet)
    {
        byte[] packed = packet.PackedSnapshots;
        int count = packet.SnapshotCount;
        if (count == 0 || count > packed.Length / AgentMountData.SnapshotBytes)
            throw new InvalidDataException("Packed mount movement has no snapshots for its ids.");

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

        // The first byte per rider is its attachment flag, before the rider's own columns.
        public MovementSnapshotColumns AgentColumns(byte[] packed) =>
            new MovementSnapshotColumns(packed.AsSpan(agents, deckOffset - agents), agents);

        public MovementSnapshotColumns DeckColumns(byte[] packed) =>
            new MovementSnapshotColumns(packed.AsSpan(deckOffset, mountOffset - deckOffset), decks);

        public MovementSnapshotColumns MountColumns(byte[] packed) =>
            new MovementSnapshotColumns(packed.AsSpan(mountOffset, ExtrasOffset - mountOffset), mounts);
    }
}
