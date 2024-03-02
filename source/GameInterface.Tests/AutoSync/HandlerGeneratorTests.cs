using Castle.Core.Logging;
using Common;
using Common.Messaging;
using Common.Tests.Utils;
using Coop.Tests.Mocks;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Registry;
using GameInterface.Utils.AutoSync.Dynamic;
using GameInterface.Utils.AutoSync.Example;
using GameInterface.Utils.AutoSync.Template;
using HarmonyLib;
using System;
using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace GameInterface.Tests.AutoSync;
public class HandlerGeneratorTests
{
    [Fact]
    public void CreateHandlerClass()
    {
        // Arrange
        var assemblyName = new AssemblyName("AutoSyncDynamicAssembly");
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

        GameLoopRunner.Instance.SetGameLoopThread();

        var eventClassCreator = new EventMessageGenerator();

        var testObj = new TestObject("TestId", 1);
        var testObjType = testObj.GetType();
        var syncedProperty = testObjType.GetProperty(nameof(TestObject.SomeValue))!;

        var dataClassType = new DataClassGenerator(moduleBuilder).GenerateClass(syncedProperty.PropertyType, "TestObjectData");
        var eventType = eventClassCreator.GenerateEvent(moduleBuilder, syncedProperty);

        var networkMessageType = new NetworkMessageGenerator().GenerateNetworkMessage(moduleBuilder, syncedProperty);

        // Act
        var network = new TestNetwork();
        var registryCollection = new RegistryCollection();
        var testObjectRegistry = new TestObjectRegistry(registryCollection);
        
        testObjectRegistry.RegisterExistingObject(testObj.StringId, testObj);
        
        var objectManager = new ObjectManager(registryCollection);
        

        var messageBroker = new TestMessageBroker();

        var objType = syncedProperty.DeclaringType!;
        var dataType = syncedProperty.PropertyType;
        var handlerType = typeof(AutoSyncHandlerTemplate<,,,>).MakeGenericType(objType, dataType, networkMessageType, eventType);
        ILogger logger = null;
        var handlerInstance = (IHandler)Activator.CreateInstance(handlerType, new object[] { messageBroker, objectManager, network, logger, syncedProperty });

        // Assert
        Assert.NotNull(handlerInstance);

        var dataInstance = Activator.CreateInstance(dataClassType, new object[] { testObj.StringId, testObj.SomeValue })!;
        var eventInstance = Activator.CreateInstance(eventType, new object[] { dataInstance })!;

        typeof(MessageBroker).GetMethod(nameof(MessageBroker.Publish)).MakeGenericMethod(eventType)
            .Invoke(messageBroker, new object[] { null, eventInstance });

        handlerInstance.Dispose();

        Assert.True(true);
    }

    public class TestObject
    {
        public string StringId;
        public int SomeValue { get; set; }
        public TestObject(string stringId, int someValue)
        {
            StringId = stringId;
            SomeValue = someValue;
        }

        public override bool Equals(object? obj)
        {
            return obj is TestObject @object &&
                   StringId == @object.StringId &&
                   SomeValue == @object.SomeValue;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(StringId, SomeValue);
        }
    }

    class TestObjectRegistry : RegistryBase<TestObject>
    {
        public TestObjectRegistry(IRegistryCollection collection) : base(collection)
        {
        }

        public override void RegisterAll()
        {
            throw new NotImplementedException();
        }

        private int nextId = 0;
        protected override string GetNewId(TestObject obj)
        {
            return $"{nameof(TestObject)}_{nextId++}";
        }
    }
}
