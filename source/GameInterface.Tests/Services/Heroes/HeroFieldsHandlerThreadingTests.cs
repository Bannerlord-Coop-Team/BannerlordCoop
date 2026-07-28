using Common;
using Common.Messaging;
using GameInterface.Services.Heroes.Data;
using GameInterface.Services.Heroes.Handlers;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using Moq;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
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

        Action<MessagePayload<ChangeName>> nameSubscriber = null;
        Action<MessagePayload<ChangeFirstName>> firstNameSubscriber = null;
        var messageBroker = new Mock<IMessageBroker>();
        messageBroker
            .Setup(b => b.Subscribe(It.IsAny<Action<MessagePayload<ChangeName>>>()))
            .Callback<Action<MessagePayload<ChangeName>>>(s => nameSubscriber = s);
        messageBroker
            .Setup(b => b.Subscribe(It.IsAny<Action<MessagePayload<ChangeFirstName>>>()))
            .Callback<Action<MessagePayload<ChangeFirstName>>>(s => firstNameSubscriber = s);

        using var handler = new HeroFieldsHandler(messageBroker.Object, objectManager.Object);
        Assert.NotNull(nameSubscriber);
        Assert.NotNull(firstNameSubscriber);

        PublishWhileCreationIsQueued(
            () => Volatile.Write(ref registered, true),
            () =>
            {
                nameSubscriber(new MessagePayload<ChangeName>(this, new ChangeName("New Name", "hero-1")));
                firstNameSubscriber(new MessagePayload<ChangeFirstName>(this, new ChangeFirstName("First", "hero-1")));
            });

        Assert.NotNull(hero._name);
        Assert.Equal("New Name", hero._name.Value);
        Assert.NotNull(hero._firstName);
        Assert.Equal("First", hero._firstName.Value);
    }

    [Fact]
    public void ChangeHeroName_PublishedWhileCreationIsStillQueued_AppliesAfterTheQueueDrains()
    {
        var hero = new Hero { StringId = "hero-2" };
        bool registered = false;

        var objectManager = new Mock<IObjectManager>();
        objectManager
            .Setup(o => o.TryGetObjectWithLogging(hero.StringId, out hero))
            .Returns(() => Volatile.Read(ref registered));

        Action<MessagePayload<ChangeHeroName>> subscriber = null;
        var messageBroker = new Mock<IMessageBroker>();
        messageBroker
            .Setup(b => b.Subscribe(It.IsAny<Action<MessagePayload<ChangeHeroName>>>()))
            .Callback<Action<MessagePayload<ChangeHeroName>>>(s => subscriber = s);

        using var handler = new HeroDataHandler(messageBroker.Object, objectManager.Object);
        Assert.NotNull(subscriber);
        var data = new HeroChangeNameData(hero, new TextObject("Full Name"), new TextObject("First"));

        PublishWhileCreationIsQueued(
            () => Volatile.Write(ref registered, true),
            () => subscriber(new MessagePayload<ChangeHeroName>(this, new ChangeHeroName(data))));

        Assert.Equal("Full Name", hero._name.Value);
        Assert.Equal("First", hero._firstName.Value);
    }

    private static void PublishWhileCreationIsQueued(Action register, Action publish)
    {
        // Not disposed deliberately: the blocker runs on the shared pump thread, and disposing an
        // event it may still be touching would throw inside the pump and kill it for every later test.
        var gate = new ManualResetEventSlim(false);
        var pumpBlocked = new ManualResetEventSlim(false);
        try
        {
            GameThread.RunSafe(() =>
            {
                pumpBlocked.Set();
                gate.Wait(TimeSpan.FromSeconds(30));
            });
            Assert.True(pumpBlocked.Wait(TimeSpan.FromSeconds(10)), "the pump never picked up the blocker");

            GameThread.EnqueueSafe(register);
            publish();
        }
        finally
        {
            gate.Set();
        }

        GameThread.Run(() => { }, blocking: true);
    }
}
