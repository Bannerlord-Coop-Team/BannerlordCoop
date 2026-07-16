using Common.Logging;
using Common.Messaging;
using Common.PacketHandlers;
using Coop.Core.Client.Messages;
using Coop.Core.Common.Network.Packets;
using LiteNetLib;
using Serilog;
using System.IO;

namespace Coop.Core.Client.Services.Save.PacketHandlers;

/// <summary>
/// Receives the transfer save (<see cref="GameSaveDataPacket"/>) and republishes it as the local
/// <see cref="NetworkGameSaveDataReceived"/> event, leaving the client join state machine
/// (<c>ReceivingSavedDataState</c>) unchanged. The save travels as its own packet type — rather than
/// a message — so it is delivered separately from the world-change stream, which the server withholds
/// from this client until it has entered the campaign.
/// </summary>
internal class GameSaveDataPacketHandler : IPacketHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<GameSaveDataPacketHandler>();

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

        try
        {
            messageBroker.Publish(this, new NetworkGameSaveDataReceived(
                SaveDataCompression.Decompress(convertedPacket.GameSaveData),
                convertedPacket.CampaignID,
                convertedPacket.CraftingPlayerData,
                convertedPacket.WorkshopPlayerData,
                convertedPacket.CaravansPlayerData,
                convertedPacket.AlleyPlayerData,
                convertedPacket.InteractionsPlayerData,
                convertedPacket.AttachmentIdMap));
        }
        catch (InvalidDataException ex)
        {
            Logger.Warning(ex, "Rejected invalid transfer save");
            peer.Disconnect();
        }
    }
}
