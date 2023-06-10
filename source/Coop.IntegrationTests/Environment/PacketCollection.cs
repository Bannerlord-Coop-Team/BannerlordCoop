using Common.Messaging;
using Common.PacketHandlers;

namespace Coop.IntegrationTests.Environment;

internal class PacketCollection
{
    public readonly List<IPacket> Packets = new List<IPacket>();

    public int Count => Packets.Count;

    public IEnumerable<TPacket> GetPackets<TPacket>() where TPacket : IPacket
    {
        return Packets
            .Where(msg => typeof(TPacket).IsAssignableFrom(msg.GetType()))
            .Select(msg => (TPacket)msg);
    }

    public int GetPacketCount<TPacket>() where TPacket : IPacket
    {
        return GetPackets<TPacket>().Count();
    }

    public void Add(IPacket message) => Packets.Add(message);
}