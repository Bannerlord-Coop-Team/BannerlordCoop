using Autofac;
using E2E.Tests.Environment;
using GameInterface.AutoSync;
using GameInterface.AutoSync.Builders;
using GameInterface.DynamicSync;
using GameInterface.Registry;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Xunit.Abstractions;

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

            var objectManager = container.Resolve<IObjectManager>();

            objectManager.AddExisting(instanceId, testClass);
        }

        server.Resolve<IDynamicSyncPatchCollector>().PatchAll();

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

