using System;
using System.IO;

namespace Missions.Agents.Packets;

/// <summary>Writes the rider, deck and mount columns of one datagram in snapshot order.</summary>
internal static class MovementSnapshotPacking
{
    // Small batches retain protobuf's sparse encoding. The column layout has little repetition at those sizes.
    private const int MinimumCount = 4;
    private const byte HasDeck = 1;
    private const byte HasMount = 2;

    public static byte[] Pack(MovementPacket packet, int limit)
    {
        AgentData[] agents = packet.Agents;
        int count = packet.AgentIds?.Length ?? packet.AgentGuids?.Length ?? 0;
        if (count < MinimumCount || count > limit / AgentData.SnapshotBytes || agents?.Length != count)
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

        int agentBytes = count * AgentData.SnapshotBytes;
        int deckBytes = decks * AgentData.DeckSnapshotBytes;
        int mountBytes = mounts * AgentMountData.SnapshotBytes;
        int fixedBytes = agentBytes + deckBytes + mountBytes;
        if (extraBytes > limit - fixedBytes) return null;

        var packed = new byte[fixedBytes + extraBytes];
        var agentColumns = new MovementSnapshotColumns(packed.AsSpan(0, agentBytes), count);
        var deckColumns = new MovementSnapshotColumns(packed.AsSpan(agentBytes, deckBytes), decks);
        var mountColumns = new MovementSnapshotColumns(packed.AsSpan(agentBytes + deckBytes, mountBytes), mounts);
        using var extras = mounts == 0 ? null : new BinaryWriter(new MemoryStream(packed, fixedBytes, extraBytes));
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

            agentColumns.BeginSnapshot(i);
            agent.WriteSnapshot(ref agentColumns, attachments);
        }
        return packed;
    }

    public static byte[] Pack(MountMovementPacket packet, int limit)
    {
        AgentMountData[] mounts = packet.Mounts;
        int count = packet.MountIds?.Length ?? packet.MountGuids?.Length ?? 0;
        if (count < MinimumCount || count > limit / AgentMountData.SnapshotBytes || mounts?.Length != count)
            return null;

        int fixedBytes = count * AgentMountData.SnapshotBytes;
        int extraBytes = 0;
        foreach (AgentMountData mount in mounts)
        {
            if (mount == null) return null;
            extraBytes += mount.GetSnapshotExtraBytes();
        }
        if (extraBytes > limit - fixedBytes) return null;

        var packed = new byte[fixedBytes + extraBytes];
        var columns = new MovementSnapshotColumns(packed.AsSpan(0, fixedBytes), count);
        using var extras = new BinaryWriter(new MemoryStream(packed, fixedBytes, extraBytes));
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
        int count = packet.AgentIds?.Length ?? packet.AgentGuids?.Length ?? 0;
        if (count == 0 || count > packed.Length / AgentData.SnapshotBytes)
            throw new InvalidDataException("Packed movement has no snapshots for its ids.");

        // The first column identifies the rows with deck or mount state, which are stored densely.
        int decks = 0;
        int mounts = 0;
        for (int i = 0; i < count; i++)
        {
            if ((packed[i] & HasDeck) != 0) decks++;
            if ((packed[i] & HasMount) != 0) mounts++;
        }

        int agentBytes = count * AgentData.SnapshotBytes;
        int deckBytes = decks * AgentData.DeckSnapshotBytes;
        int mountBytes = mounts * AgentMountData.SnapshotBytes;
        int fixedBytes = agentBytes + deckBytes + mountBytes;
        var agentColumns = new MovementSnapshotColumns(packed.AsSpan(0, agentBytes), count);
        var deckColumns = new MovementSnapshotColumns(packed.AsSpan(agentBytes, deckBytes), decks);
        var mountColumns = new MovementSnapshotColumns(packed.AsSpan(agentBytes + deckBytes, mountBytes), mounts);
        using var extras = mounts == 0 ? null : new BinaryReader(new MemoryStream(packed, fixedBytes, packed.Length - fixedBytes));
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

    public static AgentMountData[] Restore(MountMovementPacket packet)
    {
        byte[] packed = packet.PackedSnapshots;
        int count = packet.MountIds?.Length ?? packet.MountGuids?.Length ?? 0;
        if (count == 0 || count > packed.Length / AgentMountData.SnapshotBytes)
            throw new InvalidDataException("Packed mount movement has no snapshots for its ids.");

        int fixedBytes = count * AgentMountData.SnapshotBytes;
        var columns = new MovementSnapshotColumns(packed.AsSpan(0, fixedBytes), count);
        using var extras = new BinaryReader(new MemoryStream(packed, fixedBytes, packed.Length - fixedBytes));
        var mounts = new AgentMountData[count];
        for (int i = 0; i < count; i++)
        {
            columns.BeginSnapshot(i);
            mounts[i] = AgentMountData.ReadSnapshot(ref columns, extras);
        }
        return mounts;
    }
}
