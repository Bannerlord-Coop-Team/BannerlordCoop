using Autofac;
using E2E.Tests.Environment;
using GameInterface.AutoSync;
using GameInterface.AutoSync.Internal;
using HarmonyLib;
using ProtoBuf;
using ProtoBuf.Meta;
using TaleWorlds.CampaignSystem.MapEvents;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace E2E.Tests.AutoSync;

public class TestClass
{

    public string GetMyField => MyField;
    private string MyField = "Hi";
    public string MyProp { get; set; } = "Hello";

    public TestClass()
    {
        ;
    }

    public void SomeFn()
    {
        MyField = "Bye";
    }

    public void Destroy() { }
}

public class RefTestClass
{
    public MapEvent TestProp { get; set; }
    public RefTestClass(MapEvent mapEvent)
    {
        TestProp = mapEvent;
    }
}


public class AutoSyncTests : IDisposable
{
    E2ETestEnvironment TestEnvironment { get; }
    public AutoSyncTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void CreationSync()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var destroyMethod = AccessTools.Method(typeof(TestClass), nameof(TestClass.Destroy));

        List<IAutoSyncBuilder<TestClass>> builders = new();
        builders.AddRange(TestEnvironment.Clients.Select(c => c.Container.Resolve<IAutoSyncBuilder<TestClass>>()));
        builders.Add(server.Container.Resolve<IAutoSyncBuilder<TestClass>>());


        // Act
        foreach (var builder in builders)
        {
            builder.SyncCreation();
        }

        string? testclassId = null;
        server.Call(() =>
        {
            var testClass = new TestClass();
            Assert.True(server.ObjectManager.TryGetId(testClass, out testclassId));
        });

        Assert.NotNull(testclassId);

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TestClass>(testclassId, out var _));
        }
    }

    [Fact]
    public void DeletionSync()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var destroyMethod = AccessTools.Method(typeof(TestClass), nameof(TestClass.Destroy));

        List<IAutoSyncBuilder<TestClass>> builders = new();
        builders.AddRange(TestEnvironment.Clients.Select(c => c.Container.Resolve<IAutoSyncBuilder<TestClass>>()));
        builders.Add(server.Container.Resolve<IAutoSyncBuilder<TestClass>>());


        // Act
        foreach (var builder in builders)
        {
            builder.SyncCreation().SyncDeletion(destroyMethod);
        }

        server.Resolve<IAutoSyncPatcher>().PatchAll();

        string? testclassId = null;
        server.Call(() =>
        {
            var testClass = new TestClass();
            Assert.True(server.ObjectManager.TryGetId(testClass, out testclassId));

            testClass.Destroy();
            Assert.False(server.ObjectManager.TryGetId(testClass, out var _));
        });

        Assert.NotNull(testclassId);

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<TestClass>(testclassId, out var _));
        }
    }

    [Fact]
    public void PropertySync()
    {
        // Arrange
        var server = TestEnvironment.Server;
        var destroyMethod = AccessTools.Method(typeof(TestClass), nameof(TestClass.Destroy));

        List<IAutoSyncBuilder<TestClass>> builders = new();
        builders.AddRange(TestEnvironment.Clients.Select(c => c.Container.Resolve<IAutoSyncBuilder<TestClass>>()));
        builders.Add(server.Container.Resolve<IAutoSyncBuilder<TestClass>>());


        const string newPropValue = "ThisIsMyTestValue";

        // Act
        foreach (var builder in builders)
        {
            builder
                .SyncCreation()
                .SyncDeletion(destroyMethod)
                .SyncProperty(AccessTools.Property(typeof(TestClass), nameof(TestClass.MyProp)));
        }

        server.Resolve<IAutoSyncPatcher>().PatchAll();

        string? testclassId = null;
        server.Call(() =>
        {
            var testClass = new TestClass();
            Assert.True(server.ObjectManager.TryGetId(testClass, out testclassId));

            testClass.MyProp = newPropValue;
        });

        Assert.NotNull(testclassId);

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<TestClass>(testclassId, out var clientObj));

            Assert.Equal(newPropValue, clientObj.MyProp);
        }
    }

    [Fact]
    public void RefPropertySync()
    {
        // Arrange
        var server = TestEnvironment.Server;

        List<IAutoSyncBuilder<RefTestClass>> builders = new();
        builders.AddRange(TestEnvironment.Clients.Select(c => c.Container.Resolve<IAutoSyncBuilder<RefTestClass>>()));
        builders.Add(server.Container.Resolve<IAutoSyncBuilder<RefTestClass>>());

        foreach (var builder in builders)
        {
            builder
                .SyncCreation()
                .SyncProperty(AccessTools.Property(typeof(RefTestClass), nameof(RefTestClass.TestProp)));
        }

        server.Resolve<IAutoSyncPatcher>().PatchAll();

        // Act
        string? refclassId = null;
        string? mapEventId  = null;
        server.Call(() =>
        {
            var newMapEvent = new MapEvent();
            var refTestClass = new RefTestClass(new MapEvent());
            refTestClass.TestProp = newMapEvent;

            Assert.True(server.ObjectManager.TryGetId(refTestClass, out refclassId));
            mapEventId = newMapEvent.StringId;
        });

        Assert.NotNull(refclassId);
        Assert.NotNull(mapEventId);

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<RefTestClass>(refclassId, out var clientObj));
            Assert.Equal(mapEventId, clientObj.TestProp.StringId);
        }
    }
}

[ProtoContract(SkipConstructor = true)]
public class SomeClass<T>
{
    [ProtoMember(1)]
    public T Value { get; }

    public SomeClass(T value)
    {
        Value = value;
    }
}