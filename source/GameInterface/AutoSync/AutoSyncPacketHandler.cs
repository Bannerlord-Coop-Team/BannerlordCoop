using Common.Messaging;
using Common.PacketHandlers;
using LiteNetLib;

namespace GameInterface.AutoSync;
internal class AutoSyncPacketHandler : IPacketHandler
{
    public PacketType PacketType => PacketType.AutoSync;

    private readonly IPacketManager packetManager;

    public AutoSyncPacketHandler(IPacketManager packetManager)
    {
        this.packetManager = packetManager;
        packetManager.RegisterPacketHandler(this);
    }

    public void Dispose()
    {
        packetManager.RemovePacketHandler(this);
    }

    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        AutoSyncFieldPacket convertedPacket = (AutoSyncFieldPacket)packet;
        
        // TODO
    }
}
