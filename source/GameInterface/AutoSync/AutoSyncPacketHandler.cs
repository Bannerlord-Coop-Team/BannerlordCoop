using Common.Messaging;
using Common.PacketHandlers;
using GameInterface.AutoSync.Builders;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using System.Reflection.Emit;

namespace GameInterface.AutoSync;
internal class AutoSyncPacketHandler : IPacketHandler
{
    public PacketType PacketType => PacketType.AutoSync;

    private readonly IPacketManager packetManager;
    private readonly IPacketSwitchProvider packetSwitchProvider;

    public AutoSyncPacketHandler(IPacketManager packetManager, IPacketSwitchProvider packetSwitchProvider)
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
        if (packetSwitchProvider.Switcher == null) return;

        AutoSyncFieldPacket convertedPacket = (AutoSyncFieldPacket)packet;

        packetSwitchProvider.Switcher.TypeSwitch(convertedPacket);
    }
}
