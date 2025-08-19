using GameInterface.AutoSync;
using System;
using System.Reflection;
using Xunit;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Moq;

namespace GameInterface.Tests.AutoSync
{
    // Test interfaces and implementations
    public interface ITestInterface
    {
        string Name { get; set; }
        int Value { get; set; }
    }

    public abstract class AbstractTestClass
    {
        public abstract string Description { get; set; }
    }

    public class ConcreteTestClass : AbstractTestClass, ITestInterface
    {
        public string Name { get; set; }
        public int Value { get; set; }
        public override string Description { get; set; }
    }

    public class AnotherConcreteClass : ITestInterface
    {
        public string Name { get; set; }
        public int Value { get; set; }
    }

    public class TestClassWithInterfaceField
    {
        public ITestInterface InterfaceField;
        public AbstractTestClass AbstractField;
    }

    public class TestClassWithInterfaceProperty
    {
        public ITestInterface InterfaceProperty { get; set; }
        public AbstractTestClass AbstractProperty { get; set; }
    }

    public class InterfaceSupportTests
    {
        private readonly Mock<IObjectManager> mockObjectManager;
        private readonly Mock<IPacketSwitchProvider> mockPacketSwitchProvider;
        private readonly Mock<IAutoSyncPatchCollector> mockPatchCollector;
        private readonly Harmony harmony;
        private readonly AutoSyncBuilder autoSyncBuilder;

        public InterfaceSupportTests()
        {
            mockObjectManager = new Mock<IObjectManager>();
            mockPacketSwitchProvider = new Mock<IPacketSwitchProvider>();
            mockPatchCollector = new Mock<IAutoSyncPatchCollector>();
            harmony = new Harmony("InterfaceSupportTests");
            
            autoSyncBuilder = new AutoSyncBuilder(
                mockObjectManager.Object, 
                harmony, 
                mockPacketSwitchProvider.Object, 
                mockPatchCollector.Object);
        }

        [Fact]
        public void AddField_WithInterfaceType_ShouldNotThrowException()
        {
            // Arrange
            var fieldInfo = typeof(TestClassWithInterfaceField).GetField(nameof(TestClassWithInterfaceField.InterfaceField));

            // Act & Assert - Should not throw
            autoSyncBuilder.AddField(fieldInfo);
        }

        [Fact]
        public void AddField_WithAbstractType_ShouldNotThrowException()
        {
            // Arrange
            var fieldInfo = typeof(TestClassWithInterfaceField).GetField(nameof(TestClassWithInterfaceField.AbstractField));

            // Act & Assert - Should not throw
            autoSyncBuilder.AddField(fieldInfo);
        }

        [Fact]
        public void AddProperty_WithInterfaceType_ShouldNotThrowException()
        {
            // Arrange
            var propertyInfo = typeof(TestClassWithInterfaceProperty).GetProperty(nameof(TestClassWithInterfaceProperty.InterfaceProperty));

            // Act & Assert - Should not throw
            autoSyncBuilder.AddProperty(propertyInfo);
        }

        [Fact]
        public void AddProperty_WithAbstractType_ShouldNotThrowException()
        {
            // Arrange
            var propertyInfo = typeof(TestClassWithInterfaceProperty).GetProperty(nameof(TestClassWithInterfaceProperty.AbstractProperty));

            // Act & Assert - Should not throw
            autoSyncBuilder.AddProperty(propertyInfo);
        }

        [Fact]
        public void InterfaceResolver_ShouldFindConcreteTypes()
        {
            // Act
            var result = InterfaceResolver.TryResolveConcreteTypes(typeof(ITestInterface), out var concreteTypes);

            // Assert
            Assert.True(result);
            Assert.NotNull(concreteTypes);
            Assert.Contains(concreteTypes, t => t == typeof(ConcreteTestClass) || t == typeof(AnotherConcreteClass));
        }

        [Fact]
        public void InterfaceResolver_ShouldFindConcreteTypesForAbstract()
        {
            // Act
            var result = InterfaceResolver.TryResolveConcreteTypes(typeof(AbstractTestClass), out var concreteTypes);

            // Assert
            Assert.True(result);
            Assert.NotNull(concreteTypes);
            Assert.Contains(concreteTypes, t => t == typeof(ConcreteTestClass));
        }

        [Fact]
        public void InterfaceResolver_WithConcreteType_ShouldReturnItself()
        {
            // Act
            var result = InterfaceResolver.TryResolveConcreteTypes(typeof(ConcreteTestClass), out var concreteTypes);

            // Assert
            Assert.True(result);
            Assert.NotNull(concreteTypes);
            Assert.Single(concreteTypes);
            Assert.Equal(typeof(ConcreteTestClass), concreteTypes[0]);
        }

        [Fact]
        public void AddField_DuplicateField_ShouldThrowArgumentException()
        {
            // Arrange
            var fieldInfo = typeof(TestClassWithInterfaceField).GetField(nameof(TestClassWithInterfaceField.InterfaceField));

            // Act
            autoSyncBuilder.AddField(fieldInfo);

            // Assert
            Assert.Throws<ArgumentException>(() => autoSyncBuilder.AddField(fieldInfo));
        }

        [Fact]
        public void AddProperty_DuplicateProperty_ShouldThrowArgumentException()
        {
            // Arrange
            var propertyInfo = typeof(TestClassWithInterfaceProperty).GetProperty(nameof(TestClassWithInterfaceProperty.InterfaceProperty));

            // Act
            autoSyncBuilder.AddProperty(propertyInfo);

            // Assert
            Assert.Throws<ArgumentException>(() => autoSyncBuilder.AddProperty(propertyInfo));
        }
    }
}