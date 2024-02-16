using Autofac;
using Common.Messaging;
using Common.Tests.Utils;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface;
using GameInterface.Tests.Bootstrap;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Environment;

/// <summary>
/// Testing environment for End to End testing
/// </summary>
internal class E2ETestEnvironement : IDisposable
{
    public TestEnvironment IntegrationEnvironment { get; }

    public ITestOutputHelper Output { get; }

    public IEnumerable<EnvironmentInstance> Clients => IntegrationEnvironment.Clients;
    public EnvironmentInstance Server => IntegrationEnvironment.Server;
    
    public E2ETestEnvironement(ITestOutputHelper output, int numClients = 2)
    {
        GameBootStrap.Initialize();
        IntegrationEnvironment = new TestEnvironment(numClients, registerGameInterface: true);

        Server.Resolve<TestMessageBroker>().SetStaticInstance();
        var gameInterface = Server.Container.Resolve<IGameInterface>();

        gameInterface.PatchAll();

        foreach (var settlement in Campaign.Current.CampaignObjectManager.Settlements)
        {
            Server.ObjectManager.AddExisting(settlement.StringId, settlement);
        }

        Output = output;

        SetupMainHero();
    }

    public void SetupMainHero()
    {
        // Setup main hero
        Server.Call(() =>
        {
            var characterObject = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            MBObjectManager.Instance.RegisterObject(characterObject);
            var mainHero = HeroCreator.CreateSpecialHero(characterObject);
            AccessTools.Property(typeof(CharacterObject), nameof(CharacterObject.HeroObject)).SetValue(characterObject, mainHero);
            Game.Current.PlayerTroop = characterObject;
        });
    }

    public void Dispose()
    {
    }
}
