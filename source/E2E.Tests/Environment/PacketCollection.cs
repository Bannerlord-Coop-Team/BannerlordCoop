using Common.PacketHandlers;

namespace E2E.Tests.Environment;

/// <summary>
/// Collection of <see cref="IPacket"/>s
/// </summary>
public class PacketCollection
{
    public readonly List<IPacket> Packets = new List<IPacket>();

    // Same hazard as MessageCollection: background-thread sends racing test-thread assertions.
    private readonly object gate = new object();

    public int Count
    {
        get
        {
            lock (gate)
            {
                return Packets.Count;
            }
        }
    }

    public IEnumerable<TPacket> GetPackets<TPacket>() where TPacket : IPacket
    {
        List<IPacket> snapshot;
        lock (gate)
        {
            snapshot = new List<IPacket>(Packets);
        }

        return snapshot
            .Where(msg => typeof(TPacket).IsAssignableFrom(msg.GetType()))
            .Select(msg => (TPacket)msg);
    }

    public int GetPacketCount<TPacket>() where TPacket : IPacket
    {
        return GetPackets<TPacket>().Count();
    }

    public void Add(IPacket message)
    {
        lock (gate)
        {
            Packets.Add(message);
        }
    }
}
