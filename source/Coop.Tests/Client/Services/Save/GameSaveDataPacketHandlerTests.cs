using Common.PacketHandlers;
using Common.Tests.Utils;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.Save.PacketHandlers;
using Coop.Core.Common.Network.Packets;
using GameInterface.Services.CampaignService.Data;
using Moq;
using ProtoBuf.Meta;
using System;
using System.IO;
using Xunit;

namespace Coop.Tests.Client.Services.Save;

public class GameSaveDataPacketHandlerTests
{
    [Fact]
    public void GameSaveDataPacket_RoundTripPreservesServerOptions()
    {
        var original = CreatePacket(800);

        GameSaveDataPacket result;
        using (var stream = new MemoryStream())
        {
            RuntimeTypeModel.Default.Serialize(stream, original);
            stream.Position = 0;
            result = (GameSaveDataPacket)RuntimeTypeModel.Default.Deserialize(
                stream, null, typeof(GameSaveDataPacket));
        }

        Assert.Equal(2, result.ServerOptions.PlayerReceivedDamage);
        Assert.Equal(800, result.ServerOptions.BattleSize);
    }

    [Fact]
    public void HandlePacket_PublishesServerOptionsWithSave()
    {
        var packetManager = new Mock<IPacketManager>();
        var messageBroker = new TestMessageBroker();
        ServerOptions receivedOptions = null!;
        messageBroker.Subscribe<NetworkGameSaveDataReceived>(
            payload => receivedOptions = payload.What.ServerOptions);

        var handler = new GameSaveDataPacketHandler(
            packetManager.Object,
            messageBroker);
        var packet = CreatePacket(800);

        handler.HandlePacket(null!, packet);

        Assert.Equal(2, receivedOptions.PlayerReceivedDamage);
        Assert.Equal(800, receivedOptions.BattleSize);
    }

    [Fact]
    public void Dispose_RemovesPacketHandler()
    {
        var packetManager = new Mock<IPacketManager>();
        var handler = new GameSaveDataPacketHandler(
            packetManager.Object,
            new TestMessageBroker());

        handler.Dispose();

        packetManager.Verify(m => m.RemovePacketHandler(handler), Times.Once);
    }

    private static GameSaveDataPacket CreatePacket(int battleSize)
    {
        byte[] compressedSave = SaveDataCompression.Compress(Array.Empty<byte>());
        return new GameSaveDataPacket(
            compressedSave,
            "campaign",
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            new ServerOptions(2, battleSize));
    }
}
