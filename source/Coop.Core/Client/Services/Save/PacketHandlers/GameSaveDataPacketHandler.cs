using Common.Messaging;
using Common.PacketHandlers;
using Coop.Core.Client.Messages;
using Coop.Core.Common.Network.Packets;
using LiteNetLib;

namespace Coop.Core.Client.Services.Save.PacketHandlers;

/// <summary>
/// Receives the transfer save (<see cref="GameSaveDataPacket"/>) and republishes it as the local
/// <see cref="NetworkGameSaveDataReceived"/> event, leaving the client join state machine
/// (<c>ReceivingSavedDataState</c>) unchanged. The save travels as its own packet type — rather than
/// a message — so <c>LoadingPacketBuffer</c> can use its arrival as the start-of-buffering divider.
/// </summary>
internal class GameSaveDataPacketHandler : IPacketHandler
{
    public PacketType PacketType => PacketType.SaveData;

    private readonly IPacketManager packetManager;
    private readonly IMessageBroker messageBroker;

    public GameSaveDataPacketHandler(IPacketManager packetManager, IMessageBroker messageBroker)
    {
        this.packetManager = packetManager;
        this.messageBroker = messageBroker;
        packetManager.RegisterPacketHandler(this);
    }

    public void Dispose()
    {
        packetManager.RemovePacketHandler(this);
    }

    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        GameSaveDataPacket convertedPacket = (GameSaveDataPacket)packet;

        messageBroker.Publish(this, new NetworkGameSaveDataReceived(
            convertedPacket.GameSaveData,
            convertedPacket.CampaignID,
            convertedPacket.CraftingPlayerData));
    }
}
