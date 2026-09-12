using Common;
using Common.Messaging;
using GameInterface.Services.GameDebug.Handlers;
using GameInterface.Services.GameDebug.Messages;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;

namespace GameInterface.Tests.Services.GameDebug;

public class DebugMessageHandlerTests
{
    static DebugMessageHandlerTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void SendPopupMessage_WhenPublishedOnGameThread_RunsAfterCurrentTeardownStack()
    {
        using var shown = new ManualResetEventSlim(false);
        using var messageBroker = new MessageBroker();
        using var handler = new DebugMessageHandler(messageBroker, _ => shown.Set());
        bool ranInline = true;

        GameThread.Run(() =>
        {
            messageBroker.Publish(this, new SendPopupMessage("connection timed out"));
            ranInline = shown.IsSet;
        }, blocking: true);

        Assert.False(ranInline);
        Assert.True(shown.Wait(TimeSpan.FromSeconds(5)), "the deferred popup was not drained");
    }

    [Fact]
    public void SendPopupMessage_WaitsUntilTheMainMenuScreenCanHostTheInquiry()
    {
        using var messageBroker = new MessageBroker();
        var queued = new Queue<Action>();
        var shown = new List<string>();
        bool popupHostReady = false;
        DateTime now = new(2026, 7, 24, 0, 0, 0, DateTimeKind.Utc);
        var handler = new DebugMessageHandler(
            messageBroker,
            queued.Enqueue,
            () => popupHostReady,
            () => now,
            shown.Add);

        messageBroker.Publish(this, new SendPopupMessage("connection timed out"));
        handler.Dispose();

        Assert.Single(queued);
        Assert.Empty(shown);

        queued.Dequeue().Invoke();

        Assert.Single(queued);
        Assert.Empty(shown);

        popupHostReady = true;
        queued.Dequeue().Invoke();

        Assert.Empty(queued);
        Assert.Equal(new[] { "connection timed out" }, shown);
    }

    [Fact]
    public void SendPopupMessage_UsesElapsedTimeInsteadOfFrameCountForTheHeadlessFallback()
    {
        using var messageBroker = new MessageBroker();
        var queued = new Queue<Action>();
        var shown = new List<string>();
        DateTime now = new(2026, 7, 24, 0, 0, 0, DateTimeKind.Utc);
        using var handler = new DebugMessageHandler(
            messageBroker,
            queued.Enqueue,
            () => false,
            () => now,
            shown.Add);

        messageBroker.Publish(this, new SendPopupMessage("server stopped"));

        for (int drained = 0; drained < 200; drained++)
        {
            Assert.Single(queued);
            queued.Dequeue().Invoke();
        }

        Assert.Single(queued);
        Assert.Empty(shown);

        now = now.AddSeconds(30);
        queued.Dequeue().Invoke();

        Assert.Empty(queued);
        Assert.Equal(new[] { "server stopped" }, shown);
    }
}
