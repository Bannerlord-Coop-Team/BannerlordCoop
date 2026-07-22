using Common.PacketHandlers;
using Common.Tests.Utils;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.Save.PacketHandlers;
using Coop.Core.Common.Network.Packets;
using GameInterface.Services.MapEvents.BattleSize;
using Moq;
using ProtoBuf.Meta;
using System;
using System.IO;
using Xunit;

namespace Coop.Tests.Client.Services.Save;

public class GameSaveDataPacketHandlerTests
{
    [Fact]
    public void GameSaveDataPacket_RoundTripPreservesBattleSize()
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

        Assert.Equal(800, result.BattleSize);
    }

    [Fact]
    public void HandlePacket_AppliesBattleSizeBeforePublishingSave()
    {
        var packetManager = new Mock<IPacketManager>();
        var messageBroker = new TestMessageBroker();
        var battleSizeProvider = new Mock<IServerBattleSizeProvider>();
        int currentBattleSize = ServerBattleSizeProvider.DefaultBattleSize;
        int battleSizeWhenSavePublished = 0;

        battleSizeProvider
            .Setup(m => m.SetBattleSize(It.IsAny<int>()))
            .Callback<int>(value => currentBattleSize = value);
        messageBroker.Subscribe<NetworkGameSaveDataReceived>(
            _ => battleSizeWhenSavePublished = currentBattleSize);

        var handler = new GameSaveDataPacketHandler(
            packetManager.Object,
            messageBroker,
            battleSizeProvider.Object);
        var packet = CreatePacket(800);

        handler.HandlePacket(null!, packet);

        Assert.Equal(800, currentBattleSize);
        Assert.Equal(800, battleSizeWhenSavePublished);
    }

    [Fact]
    public void Dispose_RemovesPacketHandler()
    {
        var packetManager = new Mock<IPacketManager>();
        var handler = new GameSaveDataPacketHandler(
            packetManager.Object,
            new TestMessageBroker(),
            Mock.Of<IServerBattleSizeProvider>());

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
            battleSize);
    }
}
