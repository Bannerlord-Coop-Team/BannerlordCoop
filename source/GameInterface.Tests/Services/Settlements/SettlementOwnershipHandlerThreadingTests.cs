using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Settlements.Handlers;
using GameInterface.Services.Settlements.Messages;
using Moq;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;
using SettlementOwnershipMessageHandler = System.Action<Common.Messaging.MessagePayload<GameInterface.Services.Settlements.Messages.NetworkChangeSettlementOwnership>>;

namespace GameInterface.Tests.Services.Settlements;

public class SettlementOwnershipHandlerThreadingTests
{
    static SettlementOwnershipHandlerThreadingTests()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void NetworkOwnershipChange_PublishedWhileOwnerCreationIsQueued_LooksUpOwnerAfterTheQueueDrains()
    {
        Assert.True(GameThread.Instance.IsInitialized, "game-loop pump was not initialized");

        var settlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
        Hero owner = null!;
        bool ownerRegistered = false;
        bool lookupSawRegisteredOwner = false;
        var ownerLookupCompleted = new ManualResetEventSlim(false);

        var objectManager = new Mock<IObjectManager>();
        objectManager
            .Setup(o => o.TryGetObjectWithLogging("town-1", out settlement))
            .Returns(true);
        objectManager
            .Setup(o => o.TryGetObjectWithLogging("hero-1", out owner))
            .Returns(() =>
            {
                lookupSawRegisteredOwner = Volatile.Read(ref ownerRegistered);
                ownerLookupCompleted.Set();
                return false;
            });

        SettlementOwnershipMessageHandler subscriber = null!;
        var messageBroker = new Mock<IMessageBroker>();
        messageBroker
            .Setup(b => b.Subscribe(It.IsAny<SettlementOwnershipMessageHandler>()))
            .Callback<SettlementOwnershipMessageHandler>(s => subscriber = s);

        using var handler = new SettlementOwnershipHandler(
            messageBroker.Object,
            objectManager.Object,
            new Mock<INetwork>().Object);
        Assert.NotNull(subscriber);

        var releasePump = new ManualResetEventSlim(false);
        var pumpBlocked = new ManualResetEventSlim(false);
        try
        {
            GameThread.RunSafe(() =>
            {
                pumpBlocked.Set();
                releasePump.Wait(TimeSpan.FromSeconds(30));
            });
            Assert.True(pumpBlocked.Wait(TimeSpan.FromSeconds(10)), "the pump never reached the blocker");

            GameThread.EnqueueSafe(() => Volatile.Write(ref ownerRegistered, true));
            subscriber(new MessagePayload<NetworkChangeSettlementOwnership>(
                this,
                new NetworkChangeSettlementOwnership("town-1", "hero-1", null, 0)));

            Assert.False(ownerLookupCompleted.IsSet);
        }
        finally
        {
            releasePump.Set();
        }

        Assert.True(ownerLookupCompleted.Wait(TimeSpan.FromSeconds(10)), "the owner lookup was not drained");
        Assert.True(lookupSawRegisteredOwner, "the owner lookup overtook its queued creation");
        objectManager.Verify(o => o.TryGetObject(It.IsAny<string>(), out It.Ref<Settlement>.IsAny), Times.Never);
        objectManager.Verify(o => o.TryGetObject(It.IsAny<string>(), out It.Ref<Hero>.IsAny), Times.Never);
    }
}
