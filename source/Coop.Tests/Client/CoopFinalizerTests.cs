using Common;
using Common.Messaging;
using Common.Tests.Utils;
using Coop.Core.Common;
using Coop.Core.Common.Services.Connection.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.UI.Interfaces;
using Moq;
using System;
using System.Threading;
using Xunit;

namespace Coop.Tests.Client;

public class CoopFinalizerTests
{
    private const string RestartingMessage = "The server is restarting. Try again in a few minutes.";

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

        finalizer.SetCloseText(RestartingMessage);
        finalizer.Finalize("Client has been stopped");

        Assert.Equal(RestartingMessage, Assert.Single(messageBroker.GetMessagesFromType<SendPopupMessage>()).Text);
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

    [Fact]
    public void ShowCloseText_ShowsTheCloseTextOnceWithoutEndingCoop()
    {
        using var messageBroker = new TestMessageBroker();
        var finalizer = new CoopFinalizer(messageBroker, Mock.Of<ILoadingInterface>());

        finalizer.SetCloseText(RestartingMessage);
        finalizer.ShowCloseText();
        finalizer.ShowCloseText();

        Assert.Equal(RestartingMessage, Assert.Single(messageBroker.GetMessagesFromType<SendPopupMessage>()).Text);
        Assert.Empty(messageBroker.GetMessagesFromType<EndCoopMode>());
    }

    [Fact]
    public void ShowCloseText_WithoutACloseText_ShowsNothing()
    {
        using var messageBroker = new TestMessageBroker();
        var finalizer = new CoopFinalizer(messageBroker, Mock.Of<ILoadingInterface>());

        finalizer.ShowCloseText();

        Assert.Empty(messageBroker.GetMessagesFromType<SendPopupMessage>());
    }

    [Fact]
    public void ShowCloseText_AfterAFinalize_ShowsNothingMore()
    {
        using var messageBroker = new TestMessageBroker();
        var finalizer = new CoopFinalizer(messageBroker, Mock.Of<ILoadingInterface>());

        finalizer.SetCloseText(RestartingMessage);
        finalizer.Finalize("Client has been stopped");
        finalizer.ShowCloseText();

        Assert.Equal(RestartingMessage, Assert.Single(messageBroker.GetMessagesFromType<SendPopupMessage>()).Text);
    }

    [Fact]
    public void ShowCloseText_FromTheFinalizeTeardown_ShowsNothingMore()
    {
        using var messageBroker = new TestMessageBroker();
        var finalizer = new CoopFinalizer(messageBroker, Mock.Of<ILoadingInterface>());
        // The teardown cancels the session and waits for the poller, whose disconnect handler then shows the close text.
        Action<MessagePayload<EndCoopMode>> teardown = _ => finalizer.ShowCloseText();
        messageBroker.Subscribe(teardown);

        finalizer.SetCloseText(RestartingMessage);
        finalizer.Finalize("Client has been stopped");

        Assert.Equal(RestartingMessage, Assert.Single(messageBroker.GetMessagesFromType<SendPopupMessage>()).Text);
        GC.KeepAlive(teardown);
    }

    [Fact]
    public void ShowCloseText_AfterAFinalizeOnAnEndedSession_ShowsTheCloseText()
    {
        using var messageBroker = new TestMessageBroker();
        var finalizer = new CoopFinalizer(messageBroker, Mock.Of<ILoadingInterface>());
        using var session = new CancellationTokenSource();
        session.Cancel();

        finalizer.SetCloseText(RestartingMessage);
        // A finalize from the poller throws before its popup once the session has ended.
        using (GameThread.ActivateCancellation(session.Token))
        {
            Assert.Throws<OperationCanceledException>(() => finalizer.Finalize("Client has been stopped"));
        }
        finalizer.ShowCloseText();

        Assert.Equal(RestartingMessage, Assert.Single(messageBroker.GetMessagesFromType<SendPopupMessage>()).Text);
        Assert.Empty(messageBroker.GetMessagesFromType<EndCoopMode>());
    }
}
