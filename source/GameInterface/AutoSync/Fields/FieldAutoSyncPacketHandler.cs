using Common.PacketHandlers;
using LiteNetLib;

namespace GameInterface.AutoSync.Fields;
internal class FieldAutoSyncPacketHandler : IPacketHandler
{
    public PacketType PacketType => PacketType.FieldAutoSync;

    private readonly IPacketManager packetManager;
    private readonly IPacketSwitchProvider packetSwitchProvider;

    public FieldAutoSyncPacketHandler(IPacketManager packetManager, IPacketSwitchProvider packetSwitchProvider)
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
        if (packetSwitchProvider.FieldSwitch == null) return;

        FieldAutoSyncPacket convertedPacket = (FieldAutoSyncPacket)packet;

        packetSwitchProvider.FieldSwitch.TypeSwitch(convertedPacket);
    }
}
