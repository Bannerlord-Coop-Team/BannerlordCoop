using Common.Logging;
using Common.Messaging;
using Common.PacketHandlers;
using Coop.Core.Client.Messages;
using Coop.Core.Common.Network.Packets;
using LiteNetLib;
using Serilog;
using System;

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

        // Forensic fingerprint of the RECEIVED payload — the server logs the same fingerprint of what it
        // SENT ("Join transfer save" in TransferSaveState); compare the two lines when a join fails.
        Logger.Information("Received transfer save from peer {PeerId}: {Fingerprint} (campaign {CampaignId})",
            peer.Id, SaveDataCompression.Describe(convertedPacket.GameSaveData), convertedPacket.CampaignID);

        byte[] saveData;
        try
        {
            saveData = SaveDataCompression.Decompress(convertedPacket.GameSaveData);
        }
        catch (Exception ex)
        {
            // Identical fingerprints on both ends = the payload arrived intact but was never the deflate
            // stream this build's Compress produces (host/client build mismatch). Differing fingerprints =
            // the transfer or packet envelope diverged in flight.
            Logger.Error(ex, "Transfer save failed to decompress: {Fingerprint} (campaign {CampaignId}) — compare with the server's 'Join transfer save' line",
                SaveDataCompression.Describe(convertedPacket.GameSaveData), convertedPacket.CampaignID);
            throw;
        }

        messageBroker.Publish(this, new NetworkGameSaveDataReceived(
            saveData,
            convertedPacket.CampaignID,
            convertedPacket.CraftingPlayerData,
            convertedPacket.WorkshopPlayerData,
            convertedPacket.CaravansPlayerData,
            convertedPacket.AlleyPlayerData,
            convertedPacket.InteractionsPlayerData,
            convertedPacket.AttachmentIdMap));
    }
}
