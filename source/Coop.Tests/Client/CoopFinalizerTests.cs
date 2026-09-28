using Common.Tests.Utils;
using Coop.Core.Common;
using Coop.Core.Common.Services.Connection.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.UI.Interfaces;
using Moq;
using Xunit;

namespace Coop.Tests.Client;

public class CoopFinalizerTests
{
    [Fact]
    public void Finalize_ShowsItsOwnTextAndEndsCoop()
    {
        using var messageBroker = new TestMessageBroker();
        var loadingInterface = new Mock<ILoadingInterface>();
        var finalizer = new CoopFinalizer(messageBroker, loadingInterface.Object);

        finalizer.Finalize("Client has been stopped");

        loadingInterface.Verify(value => value.HideLoadingScreen(), Times.Once);
        Assert.Equal("Client has been stopped", Assert.Single(messageBroker.GetMessagesFromType<SendPopupMessage>()).Text);
        Assert.Single(messageBroker.GetMessagesFromType<EndCoopMode>());
    }

    [Fact]
    public void Finalize_AfterSetCloseText_ShowsThatTextInstead()
    {
        using var messageBroker = new TestMessageBroker();
        var finalizer = new CoopFinalizer(messageBroker, Mock.Of<ILoadingInterface>());

        finalizer.SetCloseText("The server is restarting. Try again in a few minutes.");
        finalizer.Finalize("Client has been stopped");

        Assert.Equal(
            "The server is restarting. Try again in a few minutes.",
            Assert.Single(messageBroker.GetMessagesFromType<SendPopupMessage>()).Text);
        Assert.Single(messageBroker.GetMessagesFromType<EndCoopMode>());
    }

    [Fact]
    public void Finalize_WithoutText_EndsCoopWithoutAPopup()
    {
        using var messageBroker = new TestMessageBroker();
        var finalizer = new CoopFinalizer(messageBroker, Mock.Of<ILoadingInterface>());

        finalizer.Finalize(closeText: null);

        Assert.Empty(messageBroker.GetMessagesFromType<SendPopupMessage>());
        Assert.Single(messageBroker.GetMessagesFromType<EndCoopMode>());
    }
}
