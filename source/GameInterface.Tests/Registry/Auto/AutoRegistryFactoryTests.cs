using Common.Messaging;
using Common.Network;
using Common.Serialization;
using GameInterface.AutoSync;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using Moq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace GameInterface.Tests.Registry.Auto;

public class AutoRegistryFactoryTests
{
    [Fact]
    public void RegisterAll_RunsHigherPriorityRegistriesFirst()
    {
        var calls = new List<string>();
        using var factory = CreateFactory();
        _ = new StandardPriorityRegistry(factory, calls);
        _ = new ParentRegistry(factory, calls);

        factory.RegisterAll();

        Assert.Equal(new[] { "parent", "standard" }, calls);
    }

    private static AutoRegistryFactory CreateFactory()
    {
        return new AutoRegistryFactory(
            new Mock<IRegistryCollection>().Object,
            new Mock<IMessageBroker>().Object,
            new Mock<INetwork>().Object,
            new Mock<IAutoSyncPatchCollector>().Object,
            new Mock<IObjectManager>().Object,
            new Mock<ISerializableTypeMapper>().Object);
    }

    private abstract class TestRegistry : AutoRegistryBase<object>
    {
        private readonly List<string> calls;
        private readonly string name;

        protected TestRegistry(IAutoRegistryFactory factory, List<string> calls, string name)
            : base(new Mock<ILogger>().Object, factory, new Mock<IObjectManager>().Object)
        {
            this.calls = calls;
            this.name = name;
        }

        public override IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();
        public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

        public override void RegisterAllObjects() => calls.Add(name);
        public override void OnClientCreated(object obj, string id) { }
        public override void OnClientDestroyed(object obj, string id) { }
        public override void OnServerCreated(object obj, string id) { }
        public override void OnServerDestroyed(object obj, string id) { }
    }

    private sealed class StandardPriorityRegistry : TestRegistry
    {
        public StandardPriorityRegistry(IAutoRegistryFactory factory, List<string> calls)
            : base(factory, calls, "standard")
        {
        }
    }

    private sealed class ParentRegistry : TestRegistry
    {
        public override int RegistrationPriority => 100;

        public ParentRegistry(IAutoRegistryFactory factory, List<string> calls)
            : base(factory, calls, "parent")
        {
        }
    }
}
