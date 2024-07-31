using Autofac;
using E2E.Tests.Environment;
using GameInterface.AutoSync;
using HarmonyLib;
using Xunit.Abstractions;

namespace E2E.Tests.AutoSync;

public class TestClass
{
    public TestClass()
    {
        ;
    }
    public void Destroy() { }
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

        IAutoSyncBuilder<TestClass>[] builders = TestEnvironment.Clients.Select(c => c.Container.Resolve<IAutoSyncBuilder<TestClass>>()).Append(
            server.Container.Resolve<IAutoSyncBuilder<TestClass>>()
            ).ToArray();


        // Act
        foreach (var builder in builders)
        {
            builder.SyncCreation();
        }
        builders[0].Build();


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

        IAutoSyncBuilder<TestClass>[] builders = TestEnvironment.Clients.Select(c => c.Container.Resolve<IAutoSyncBuilder<TestClass>>()).Append(
            server.Container.Resolve<IAutoSyncBuilder<TestClass>>()
            ).ToArray();


        // Act
        foreach(var builder in builders)
        {
            builder.SyncCreation().SyncDeletion(destroyMethod);
        }
        builders[0].Build();


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
}
