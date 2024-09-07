using Common;
using Common.Tests.Utils;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface;
using GameInterface.AutoSync;
using GameInterface.Tests.Bootstrap;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Environment;

/// <summary>
/// Testing environment for End to End testing
/// </summary>
internal class E2ETestEnvironment : IDisposable
{
    public ITestOutputHelper Output { get; }

    public IEnumerable<EnvironmentInstance> Clients => IntegrationEnvironment.Clients;
    public EnvironmentInstance Server => IntegrationEnvironment.Server;

    private TestEnvironment IntegrationEnvironment { get; }

    public E2ETestEnvironment(ITestOutputHelper output, int numClients = 2)
    {
        GameLoopRunner.Instance.SetGameLoopThread();

        GameBootStrap.Initialize();
        IntegrationEnvironment = new TestEnvironment(numClients, registerGameInterface: true);

        SetupAutoSync();

        Server.Resolve<TestMessageBroker>().SetStaticInstance();
        Server.Resolve<IGameInterface>().PatchAll();

        foreach (var settlement in Campaign.Current.CampaignObjectManager.Settlements)
        {
            Server.ObjectManager.AddExisting(settlement.StringId, settlement);
        }

        Output = output;

        SetupMainHero();
    }

    public void SetupAutoSync()
    {
        Server.Resolve<IAutoSyncBuilder>().Build();

        foreach (var client in Clients)
        {
            client.Resolve<IAutoSyncBuilder>().Build();
        }
    }

    public void SetupMainHero()
    {
        // Setup main hero
        Server.Call(() =>
        {
            var characterObject = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            MBObjectManager.Instance.RegisterObject(characterObject);
            var mainHero = HeroCreator.CreateSpecialHero(characterObject);
            characterObject.HeroObject = mainHero;
            Game.Current.PlayerTroop = characterObject;
        });
    }

    // TODO add comments
    public string CreateRegisteredObject<T>() where T : class
    {
        string? id = null;
        Server.Call(() =>
        {
            var obj = GameObjectCreator.CreateInitializedObject<T>();

            if (Server.ObjectManager.TryGetId(obj, out id) == false)
            {
                throw new Exception($"Server object manager failed to register new object {typeof(T).Name}");
            }
        });

        if (id == null)
        {
            throw new Exception($"Failed to create {typeof(T).Name} on Server");
        }

        return id;
    }

    public MethodInfo GetIntercept(FieldInfo field)
    {
        Assert.True(Server.Resolve<IAutoSyncBuilder>().TryGetIntercept(field, out var intercept));

        return intercept;
    }

    public void Dispose()
    {
        Server.Resolve<IAutoSyncPatchCollector>().UnpatchAll();
    }
}
