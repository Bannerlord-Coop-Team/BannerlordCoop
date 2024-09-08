using Common.PacketHandlers;
using GameInterface.AutoSync.Fields;
using LiteNetLib;

namespace GameInterface.AutoSync.Properties;
internal class PropertyAutoSyncPacketHandler : IPacketHandler
{
    public PacketType PacketType => PacketType.FieldAutoSync;

    private readonly IPacketManager packetManager;
    private readonly IPacketSwitchProvider packetSwitchProvider;

    public PropertyAutoSyncPacketHandler(IPacketManager packetManager, IPacketSwitchProvider packetSwitchProvider)
    {
        this.packetManager = packetManager;
        this.packetSwitchProvider = packetSwitchProvider;

        packetManager.RegisterPacketHandler(this);
    }

    public void Dispose()
    {
        packetManager.RemovePacketHandler(this);
    }

    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        if (packetSwitchProvider.PropertySwitch == null) return;

        PropertyAutoSyncPacket convertedPacket = (PropertyAutoSyncPacket)packet;

        packetSwitchProvider.PropertySwitch.TypeSwitch(convertedPacket);
    }
}
