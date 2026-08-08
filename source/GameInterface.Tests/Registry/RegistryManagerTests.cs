using Common.Messaging;
using GameInterface.AutoSync;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Registry.Messages;
using GameInterface.Services.ObjectManager;
using Moq;
using Xunit;

namespace GameInterface.Tests.Registry;

public class RegistryManagerTests
{
    [Fact]
    public void RegisterUntrackedGameObjects_RegistersWithoutPublishingCompletion()
    {
        var autoRegistryFactory = new Mock<IAutoRegistryFactory>();
        var messageBroker = new Mock<IMessageBroker>();
        var manager = CreateManager(autoRegistryFactory.Object, messageBroker.Object);

        manager.RegisterUntrackedGameObjects();

        autoRegistryFactory.Verify(factory => factory.RegisterAll(), Times.Once);
        messageBroker.Verify(
            broker => broker.Publish(It.IsAny<object>(), It.IsAny<AllGameObjectsRegistered>()),
            Times.Never);
    }

    [Fact]
    public void RegisterAllGameObjects_RegistersBeforePublishingCompletion()
    {
        var registrationOrder = new MockSequence();
        var autoRegistryFactory = new Mock<IAutoRegistryFactory>(MockBehavior.Strict);
        var messageBroker = new Mock<IMessageBroker>(MockBehavior.Strict);
        autoRegistryFactory
            .InSequence(registrationOrder)
            .Setup(factory => factory.RegisterAll());
        messageBroker
            .InSequence(registrationOrder)
            .Setup(broker => broker.Publish(It.IsAny<object>(), It.IsAny<AllGameObjectsRegistered>()));
        var manager = CreateManager(autoRegistryFactory.Object, messageBroker.Object);

        manager.RegisterAllGameObjects();

        autoRegistryFactory.VerifyAll();
        messageBroker.VerifyAll();
    }

    private static RegistryManager CreateManager(
        IAutoRegistryFactory autoRegistryFactory,
        IMessageBroker messageBroker)
    {
        return new RegistryManager(
            Mock.Of<IObjectManager>(),
            Mock.Of<IRegistryCollection>(),
            messageBroker,
            autoRegistryFactory,
            Mock.Of<IAutoSyncPatchCollector>());
    }
}
