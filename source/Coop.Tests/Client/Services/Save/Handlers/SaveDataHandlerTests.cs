using Common.Tests.Utils;
using Coop.Core.Client.Services.Save.Handler;
using Coop.Core.Server.Services.Save.Messages;
using GameInterface.Services.Save.Interfaces;
using Moq;
using Xunit;

namespace Coop.Tests.Client.Services.Save.Handlers;

public class SaveDataHandlerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NetworkGameSaveStateChanged_UpdatesSaveNotification(bool isSaving)
    {
        var messageBroker = new TestMessageBroker();
        var saveNotificationInterface = new Mock<ISaveNotificationInterface>();
        using var handler = new SaveDataHandler(
            messageBroker,
            saveNotificationInterface.Object);

        messageBroker.Publish(handler, new NetworkGameSaveStateChanged(isSaving));

        saveNotificationInterface.Verify(
            value => value.SetSaving(isSaving),
            Times.Once);
    }

    [Fact]
    public void Dispose_ClearsSavingNotification()
    {
        var messageBroker = new TestMessageBroker();
        var saveNotificationInterface = new Mock<ISaveNotificationInterface>();
        var handler = new SaveDataHandler(
            messageBroker,
            saveNotificationInterface.Object);

        handler.Dispose();

        saveNotificationInterface.Verify(
            value => value.SetSaving(false),
            Times.Once);
    }
}
