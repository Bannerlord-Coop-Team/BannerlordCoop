using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Handlers;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using Moq;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading;
using TaleWorlds.CampaignSystem;
using Xunit;

namespace GameInterface.Tests.Services.Heroes;

/// <summary>
/// Verifies that <see cref="HeroFieldsHandler"/> resolves the target hero on the game-loop thread,
/// in queue order with the marshaled hero creation, instead of on the network (poller) thread that
/// publishes the message. A poller-thread lookup races a creation still waiting in the game-thread
/// queue and permanently drops the one-shot apply, leaving a partially-initialized hero — a
/// name-less hero in the campaign's hero list breaks every encyclopedia open.
/// </summary>
public class HeroFieldsHandlerThreadingTests
{
    static HeroFieldsHandlerThreadingTests()
    {
        // Coop.Tests starts and continuously pumps a dedicated game-loop thread from a
        // [ModuleInitializer] (TestGameLoopPump); force that initializer to run so the pump is up
        // even when this class runs in isolation. RunModuleConstructor is idempotent.
        RuntimeHelpers.RunModuleConstructor(typeof(Coop.Tests.Mocks.TestNetwork).Module.ModuleHandle);
    }

    [Fact]
    public void ChangeName_PublishedWhileCreationIsStillQueued_AppliesAfterTheQueueDrains()
    {
        Assert.True(GameThread.Instance.IsInitialized, "game-loop pump was not initialized");

        var hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
        bool registered = false;

        var objectManager = new Mock<IObjectManager>();
        objectManager
            .Setup(o => o.TryGetObjectWithLogging("hero-1", out hero))
            .Returns(() => Volatile.Read(ref registered));

        Action<MessagePayload<ChangeName>> subscriber = null;
        var messageBroker = new Mock<IMessageBroker>();
        messageBroker
            .Setup(b => b.Subscribe(It.IsAny<Action<MessagePayload<ChangeName>>>()))
            .Callback<Action<MessagePayload<ChangeName>>>(s => subscriber = s);

        using var handler = new HeroFieldsHandler(messageBroker.Object, objectManager.Object);
        Assert.NotNull(subscriber);

        // Not disposed deliberately: the blocker runs on the shared pump thread, and disposing an
        // event it may still be touching would throw inside the pump and kill it for every later test.
        var gate = new ManualResetEventSlim(false);
        var pumpBlocked = new ManualResetEventSlim(false);
        try
        {
            // Park the pump so the queued "creation" below is provably still pending when the
            // message is handled, mirroring a game loop that is not draining (e.g. while alt-tabbed).
            GameThread.RunSafe(() =>
            {
                pumpBlocked.Set();
                gate.Wait(TimeSpan.FromSeconds(30));
            });
            Assert.True(pumpBlocked.Wait(TimeSpan.FromSeconds(10)), "the pump never picked up the blocker");

            // The marshaled hero creation, still waiting in the game-thread queue.
            GameThread.EnqueueSafe(() => Volatile.Write(ref registered, true));

            // The one-shot name apply arrives on the poller thread while the creation is still
            // queued. Resolving here instead of in queue order would drop the name forever.
            subscriber(new MessagePayload<ChangeName>(this, new ChangeName("New Name", "hero-1")));
            Assert.Null(hero._name);
        }
        finally
        {
            gate.Set();
        }

        // A blocking probe queued after the apply completes only after everything ahead of it in
        // the queue has drained, so the creation and the apply have both run by the time it returns.
        GameThread.Run(() => { }, blocking: true);

        Assert.NotNull(hero._name);
        Assert.Equal("New Name", hero._name.Value);
    }
}
