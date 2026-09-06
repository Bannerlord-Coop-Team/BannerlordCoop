using Common;
using Common.Messaging;
using Common.Network.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.Locations.Conversations;
using GameInterface.Services.Locations.Conversations.Handlers;
using GameInterface.Services.Locations.Messages.Conversation;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Moq;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;

namespace GameInterface.Tests.Services.Locations;

[Collection(ModInformationRoleCollection.Name)]
public sealed class LocationConversationHandlerThreadingTests
{
    static LocationConversationHandlerThreadingTests()
    {
        // Coop.Tests provides the game-loop pump used by GameThread in unit tests. Force its module
        // initializer to run so a filtered invocation of this test has a live pump too.
        RuntimeHelpers.RunModuleConstructor(typeof(TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void PlayerDisconnect_DoesNotOvertakeEarlierGameThreadWork()
    {
        Assert.True(GameThread.Instance.IsInitialized, "game-loop pump was not initialized");
        Assert.False(GameThread.Instance.IsGameThread, "the publisher must model the network thread");

        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;
        try
        {
            using var messageBroker = new MessageBroker();
            using var network = new TestNetwork();
            using var tracker = new LocationConversationTracker(new Mock<IObjectManager>().Object);
            using var handler = new LocationConversationHandler(
                messageBroker,
                network,
                tracker,
                new LocationConversationClientState(),
                new Mock<IPlayerManager>().Object);

            var peer = network.CreatePeer();
            Assert.True(tracker.TryBeginEngagement(peer, "location|player", "location|npc"));

            var releasePump = new ManualResetEventSlim(false);
            var pumpBlocked = new ManualResetEventSlim(false);
            bool engagementWasActiveForEarlierWork = false;
            try
            {
                GameThread.RunSafe(() =>
                {
                    pumpBlocked.Set();
                    releasePump.Wait(TimeSpan.FromSeconds(30));
                });
                Assert.True(pumpBlocked.Wait(TimeSpan.FromSeconds(10)), "the pump never reached the blocker");

                GameThread.RunSafe(() =>
                    engagementWasActiveForEarlierWork = tracker.TryGetEngagement(peer, out _));

                messageBroker.Publish(this, new PlayerDisconnected(peer, default));
            }
            finally
            {
                releasePump.Set();
            }

            GameThread.Run(() => { }, blocking: true);

            Assert.True(
                engagementWasActiveForEarlierWork,
                "disconnect release overtook previously queued authorization validation");
            Assert.False(tracker.TryGetEngagement(peer, out _));
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }

    [Fact]
    public void NetworkConversationEnd_DoesNotOvertakeEarlierGameThreadWork()
    {
        Assert.True(GameThread.Instance.IsInitialized, "game-loop pump was not initialized");
        Assert.False(GameThread.Instance.IsGameThread, "the publisher must model the network thread");

        var wasServer = ModInformation.IsServer;
        ModInformation.IsServer = true;
        try
        {
            using var messageBroker = new MessageBroker();
            using var network = new TestNetwork();
            using var tracker = new LocationConversationTracker(new Mock<IObjectManager>().Object);
            using var handler = new LocationConversationHandler(
                messageBroker,
                network,
                tracker,
                new LocationConversationClientState(),
                new Mock<IPlayerManager>().Object);

            var peer = network.CreatePeer();
            Assert.True(tracker.TryBeginEngagement(peer, "location|player", "location|npc"));

            // Do not dispose these events: if a failure leaves the shared pump touching one, disposal
            // would throw on that background thread and break every test that runs afterward.
            var releasePump = new ManualResetEventSlim(false);
            var pumpBlocked = new ManualResetEventSlim(false);
            bool engagementWasActiveForEarlierWork = false;
            try
            {
                GameThread.RunSafe(() =>
                {
                    pumpBlocked.Set();
                    releasePump.Wait(TimeSpan.FromSeconds(30));
                });
                Assert.True(pumpBlocked.Wait(TimeSpan.FromSeconds(10)), "the pump never reached the blocker");

                // This models authorization validation already queued by an earlier reliable packet.
                GameThread.RunSafe(() =>
                    engagementWasActiveForEarlierWork = tracker.TryGetEngagement(peer, out _));

                // The later conversation-end packet must enqueue its release behind that validation.
                messageBroker.Publish(peer, new NetworkLocationConversationEnded());
            }
            finally
            {
                releasePump.Set();
            }

            // A blocking probe completes after the validation and release queued ahead of it.
            GameThread.Run(() => { }, blocking: true);

            Assert.True(
                engagementWasActiveForEarlierWork,
                "conversation release overtook previously queued authorization validation");
            Assert.False(tracker.TryGetEngagement(peer, out _));
        }
        finally
        {
            ModInformation.IsServer = wasServer;
        }
    }
}
