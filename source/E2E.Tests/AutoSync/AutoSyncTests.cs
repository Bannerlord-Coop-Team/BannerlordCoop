using Autofac;
using E2E.Tests.Environment;
using GameInterface;
using GameInterface.AutoSync;
using GameInterface.AutoSync.Builders;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Registry;
using GameInterface.Tests.AutoSync;
using HarmonyLib;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Xunit.Abstractions;
using static GameInterface.Tests.AutoSync.AutoSyncTests;

namespace E2E.Tests.AutoSync;

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
    public void FieldTest()
    {
        var server = TestEnvironment.Server;
        const string instanceId = "MyObj";
        const int newMyIntValue = 2002;

        var containers = TestEnvironment.Clients.Select(client => client.Container).AddItem(TestEnvironment.Server.Container);

        foreach (var container in containers)
        {
            var testClass = new AutoSyncTestClass();

            var registry = new AutoSyncTestClassRegistry(container.Resolve<IRegistryCollection>());

            registry.RegisterExistingObject(instanceId, testClass);

            var autosyncBuilder = container.Resolve<IAutoSyncBuilder>();

            autosyncBuilder.AddField(AccessTools.Field(typeof(AutoSyncTestClass), nameof(AutoSyncTestClass.MyInt)));
            autosyncBuilder.Build();
        }

        server.Resolve<IAutoSyncPatchCollector>().PatchAll();

        server.Call(() =>
        {
            var testClass = server.GetRegisteredObject<AutoSyncTestClass>(instanceId);

            testClass.SetMyInt(newMyIntValue);
        });


        foreach (var client in TestEnvironment.Clients)
        {
            var testClass = client.GetRegisteredObject<AutoSyncTestClass>(instanceId);

            Assert.Equal(newMyIntValue, testClass.MyInt);
        }
    }
}

