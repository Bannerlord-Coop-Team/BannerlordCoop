using Common.Network;
using Common.Tests.Utils;
using Coop.Core.Server.Services.Save.Handlers;
using Coop.Core.Server.Services.Save.Messages;
using GameInterface.Services.Save.Messages;
using Moq;
using Xunit;

namespace Coop.Tests.Server.Services.Save;

public class SaveGameHandlerTests
{
    [Fact]
    public void GameSaveStateChanged_BroadcastsFirstStartAndLastEnd()
    {
        var messageBroker = new TestMessageBroker();
        var network = new Mock<INetwork>();
        using var handler = new SaveGameHandler(
            messageBroker,
            null!,
            null!,
            null!,
            network.Object);

        var firstSave = new object();
        var secondSave = new object();

        messageBroker.Publish(firstSave, new GameSaveStateChanged(true));
        messageBroker.Publish(secondSave, new GameSaveStateChanged(true));
        messageBroker.Publish(firstSave, new GameSaveStateChanged(false));
        messageBroker.Publish(secondSave, new GameSaveStateChanged(false));

        network.Verify(
            value => value.SendAll(It.Is<NetworkGameSaveStateChanged>(
                message => message.IsSaving)),
            Times.Once);
        network.Verify(
            value => value.SendAll(It.Is<NetworkGameSaveStateChanged>(
                message => !message.IsSaving)),
            Times.Once);
        network.VerifyNoOtherCalls();
    }
}
