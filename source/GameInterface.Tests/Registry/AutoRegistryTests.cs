using GameInterface.Registry.Auto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Moq;
using Common.Messaging;
using Common.Network;
using GameInterface.AutoSync;
using GameInterface.Services.ObjectManager;
using Common.Serialization;
using Common;

namespace GameInterface.Tests.Registry
{
    // Test classes for auto registry
    public class TestEntity
    {
        public string Id { get; set; }
        public string Name { get; set; }
        
        public TestEntity(string id)
        {
            Id = id;
        }

        public void Destroy()
        {
            // Cleanup logic
        }
    }

    [AutoRegister(typeof(TestEntity))]
    public class TestEntityAutoRegistry : IAutoRegistry<TestEntity>
    {
        public IEnumerable<MethodBase> Constructors => new[] {
            typeof(TestEntity).GetConstructor(new[] { typeof(string) })
        };

        public IEnumerable<MethodBase> DestroyMethods => new[] {
            typeof(TestEntity).GetMethod("Destroy")
        };

        public void RegisterAllObjects(IRegistry<TestEntity> registry)
        {
            // Register existing objects
            registry.RegisterExistingObject("test1", new TestEntity("test1"));
            registry.RegisterExistingObject("test2", new TestEntity("test2"));
        }

        public void OnClientCreated(TestEntity obj, string id) { }
        public void OnClientDestroyed(TestEntity obj, string id) { }
        public void OnServerCreated(TestEntity obj, string id) { }
        public void OnServerDestroyed(TestEntity obj, string id) { }
    }

    public class AutoRegistryTests
    {
        private readonly Mock<IRegistryCollection> mockCollection;
        private readonly Mock<IMessageBroker> mockMessageBroker;
        private readonly Mock<INetwork> mockNetwork;
        private readonly Mock<IAutoSyncPatchCollector> mockSyncPatchCollector;
        private readonly Mock<IObjectManager> mockObjectManager;
        private readonly Mock<ISerializableTypeMapper> mockTypeMapper;
        private readonly AutoRegistryFactory registryFactory;

        public AutoRegistryTests()
        {
            mockCollection = new Mock<IRegistryCollection>();
            mockMessageBroker = new Mock<IMessageBroker>();
            mockNetwork = new Mock<INetwork>();
            mockSyncPatchCollector = new Mock<IAutoSyncPatchCollector>();
            mockObjectManager = new Mock<IObjectManager>();
            mockTypeMapper = new Mock<ISerializableTypeMapper>();

            registryFactory = new AutoRegistryFactory(
                mockCollection.Object,
                mockMessageBroker.Object,
                mockNetwork.Object,
                mockSyncPatchCollector.Object,
                mockObjectManager.Object,
                mockTypeMapper.Object
            );
        }

        [Fact]
        public void RegistryScanner_FindsAutoRegisterTypes()
        {
            // Arrange
            var scanner = new RegistryScanner(registryFactory);

            // Act
            var types = scanner.GetAutoRegisterTypes();

            // Assert
            Assert.Contains(types, t => t == typeof(TestEntityAutoRegistry));
        }

        [Fact]
        public void RegistryScanner_ScansAndRegisters()
        {
            // Arrange
            var scanner = new RegistryScanner(registryFactory);

            // Act
            scanner.ScanAssembly(Assembly.GetExecutingAssembly());

            // Assert
            // Verify that type mapper was called to add types
            mockTypeMapper.Verify(x => x.AddTypes(It.IsAny<Type[]>()), Times.AtLeastOnce);
        }

        [Fact]
        public void AutoRegistry_RegistersWithFactory()
        {
            // Arrange
            var autoRegistry = new TestEntityAutoRegistry();

            // Act
            registryFactory.RegisterType(autoRegistry);

            // Assert
            mockSyncPatchCollector.Verify(x => x.AddPrefix(
                It.IsAny<MethodBase>(), 
                It.IsAny<MethodInfo>()), 
                Times.AtLeastOnce);
        }

        [Fact]
        public void AutoRegistry_RegistersExistingObjects()
        {
            // Arrange
            var autoRegistry = new TestEntityAutoRegistry();
            var mockRegistry = new Mock<IRegistry<TestEntity>>();

            // Act
            autoRegistry.RegisterAllObjects(mockRegistry.Object);

            // Assert
            mockRegistry.Verify(x => x.RegisterExistingObject(
                It.IsAny<string>(), 
                It.IsAny<TestEntity>()), 
                Times.Exactly(2));
        }

        [Fact]
        public void AutoRegisterAttribute_StoresRegistryType()
        {
            // Arrange & Act
            var attribute = new AutoRegisterAttribute(typeof(TestEntity));

            // Assert
            Assert.Equal(typeof(TestEntity), attribute.RegistryType);
            Assert.True(attribute.ScanConstructors);
            Assert.True(attribute.ScanDestructors);
        }

        [Fact]
        public void AutoRegistryFactory_ValidatesConstructorTypes()
        {
            // Arrange
            var wrongTypeConstructor = typeof(string).GetConstructor(Type.EmptyTypes);
            var callbacks = new Mock<AutoRegistryCallbacks<TestEntity>>(null);

            // Act & Assert
            Assert.Throws<AggregateException>(() => 
                registryFactory.TryRegisterType<TestEntity>(
                    new[] { wrongTypeConstructor },
                    Enumerable.Empty<MethodBase>(),
                    null,
                    callbacks.Object
                )
            );
        }

        [Fact]
        public void RegistryScanner_HandlesAssemblyLoadErrors()
        {
            // Arrange
            var scanner = new RegistryScanner(registryFactory);

            // Act & Assert - Should not throw
            scanner.ScanAssembly(null);
            scanner.ScanAndRegister(); // Should handle errors gracefully
        }

        [Fact]
        public void AutoRegistry_CallsLifecycleCallbacks()
        {
            // Arrange
            var autoRegistry = new TestEntityAutoRegistry();
            var entity = new TestEntity("test");

            // Act & Assert - Should not throw
            autoRegistry.OnClientCreated(entity, "test");
            autoRegistry.OnClientDestroyed(entity, "test");
            autoRegistry.OnServerCreated(entity, "test");
            autoRegistry.OnServerDestroyed(entity, "test");
        }
    }
}