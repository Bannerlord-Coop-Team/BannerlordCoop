using Common.PacketHandlers;
using GameInterface.AutoSync.Fields;
using LiteNetLib;
using Serilog;

namespace GameInterface.AutoSync.Properties;
internal class PropertyAutoSyncPacketHandler : IPacketHandler
{
    public PacketType PacketType => PacketType.PropertyAutoSync;

    private readonly IPacketManager packetManager;
    private readonly IPacketSwitchProvider packetSwitchProvider;
    private readonly ILogger logger;

    public PropertyAutoSyncPacketHandler(IPacketManager packetManager, IPacketSwitchProvider packetSwitchProvider, ILogger logger)
    {
        this.packetManager = packetManager;
        this.packetSwitchProvider = packetSwitchProvider;
        this.logger = logger;
        packetManager.RegisterPacketHandler(this);
    }

    public void Dispose()
    {
        packetManager.RemovePacketHandler(this);
    }

    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        if (packetSwitchProvider.PropertySwitch == null)
        {
            logger.Error("Tried to handle autosync packet but autosync has not been build on this instance");
            return;
        }

        PropertyAutoSyncPacket convertedPacket = (PropertyAutoSyncPacket)packet;

        packetSwitchProvider.PropertySwitch.TypeSwitch(convertedPacket);
    }
}
