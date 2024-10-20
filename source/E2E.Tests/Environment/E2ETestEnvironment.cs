using Common;
using Common.Logging;
using Common.Tests.Utils;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface;
using GameInterface.AutoSync;
using GameInterface.Tests.Bootstrap;
using Serilog;
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
        LogManager.Configuration = new LoggerConfiguration().WriteTo.TestOutput(output);

        GameLoopRunner.Instance.SetGameLoopThread();

        GameBootStrap.Initialize();
        IntegrationEnvironment = new TestEnvironment(output, numClients, registerGameInterface: true);


        Server.Resolve<TestMessageBroker>().SetStaticInstance();
        Server.Resolve<IGameInterface>().PatchAll();

        SetupAutoSync();

        foreach (var settlement in Campaign.Current.CampaignObjectManager.Settlements)
        {
            Server.ObjectManager.AddExisting(settlement.StringId, settlement);
        }

        Output = output;

        SetupMainHero();
    }

    private void SetupAutoSync()
    {
        Server.Resolve<IAutoSyncBuilder>().Build();
        Server.Resolve<IAutoSyncPatchCollector>().PatchAll();

        foreach (var client in Clients)
        {
            client.Resolve<IAutoSyncBuilder>().Build();
        }
    }

    private void SetupMainHero()
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

    /// <summary>
    /// Creates a new object of type <typeparamref name="T"/> that is registered on the server and all clients.
    /// </summary>
    /// <remarks>
    /// This uses the <see cref="GameObjectCreator"/> to generate objects.
    /// </remarks>
    /// <typeparam name="T">Type of object to create</typeparam>
    /// <param name="disabledMethods">Methods to disable while calling this method</param>
    /// <returns>New object of type <typeparamref name="T"/></returns>
    /// <exception cref="Exception">Failed to create object exception</exception>
    public string CreateRegisteredObject<T>(IEnumerable<MethodBase>? disabledMethods = null) where T : class
    {
        string? id = null;
        Server.Call(() =>
        {
            var obj = GameObjectCreator.CreateInitializedObject<T>();

            if (Server.ObjectManager.TryGetId(obj, out id) == false)
            {
                throw new Exception($"Server object manager failed to register new object {typeof(T).Name}");
            }
        }, disabledMethods);

        if (id == null)
        {
            throw new Exception($"Failed to create {typeof(T).Name} on Server");
        }

        return id;
    }

    /// <summary>
    /// Gets the field changed intercept from the given <paramref name="field"/>
    /// </summary>
    /// <param name="field">Field to get intercept from</param>
    /// <returns>Field intercept as <see cref="MethodInfo"/></returns>
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
