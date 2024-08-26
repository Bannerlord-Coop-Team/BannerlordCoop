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
    private readonly IAutoSyncBuilder autoSyncBuilder;

    public AutoSyncPacketHandler(IPacketManager packetManager, IAutoSyncBuilder autoSyncBuilder)
    {
        this.packetManager = packetManager;
        this.autoSyncBuilder = autoSyncBuilder;
        packetManager.RegisterPacketHandler(this);
    }

    public void Dispose()
    {
        packetManager.RemovePacketHandler(this);
    }

    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        AutoSyncFieldPacket convertedPacket = (AutoSyncFieldPacket)packet;

        autoSyncBuilder.SwitchPacket(convertedPacket);
    }
}
