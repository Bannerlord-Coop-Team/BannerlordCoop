using Autofac;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Serialization;
using E2E.Tests.Environment;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.AutoSync;
using GameInterface.Utils.AutoSync.Template;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using Xunit.Abstractions;
using static TaleWorlds.CampaignSystem.GameMenus.GameMenuEventHandler;

namespace E2E.Tests.Services.Heroes;
public class HeroSetNameAuto
{
    E2ETestEnvironement TestEnvironement { get; }
    public HeroSetNameAuto(ITestOutputHelper output)
    {
        TestEnvironement = new E2ETestEnvironement(output);
    }

    public void Dispose()
    {
        TestEnvironement.Dispose();
    }

    public static string GetHeroId(Hero hero) => hero.StringId;

    [Fact]
    public void ServerSetWeight_SyncAllClients()
    {
        // Arrange
        var server = TestEnvironement.Server;

        var networkId = "CoopHero_1";
        // Creates a new hero and registers it with the objectManager
        // using the networkId as an identifier
        var serverHero = TestEnvironement.Server.CreateRegisteredObject<Hero>(networkId);


        var autoSync = server.Container.Resolve<IAutoSync>();
        var heroNameProperty = AccessTools.Property(typeof(Hero), nameof(Hero.Weight));
        var syncResults = autoSync.SyncProperty<Hero>(heroNameProperty, GetHeroId);

        // Creates and stores heroes on the clients with same id as server

        var disposables = new List<IDisposable>();
        var clientHeroes = new List<Hero>();
        foreach (var client in TestEnvironement.Clients)
        {
            clientHeroes.Add(client.CreateRegisteredObject<Hero>(networkId));

            disposables.Add(SetupClient(client.Container, syncResults, heroNameProperty));
        }

        // Create new text objects for name fields
        float newWeight = 5;

        // Act
        server.Call(() =>
        {
            serverHero.Weight = newWeight;
        });

        // Assert
        Assert.Equal(newWeight, serverHero.Weight);


        foreach (var clientHero in clientHeroes)
        {
            Assert.Equal(newWeight, clientHero.Weight);
        }
    }

    [Fact]
    public void ClientSetWeight_DoesNothing()
    {
        // Arrange
        var server = TestEnvironement.Server;

        var networkId = "CoopHero_1";
        // Creates a new hero and registers it with the objectManager
        // using the networkId as an identifier
        var serverHero = TestEnvironement.Server.CreateRegisteredObject<Hero>(networkId);


        var autoSync = server.Container.Resolve<IAutoSync>();
        var heroNameProperty = AccessTools.Property(typeof(Hero), nameof(Hero.Weight));
        var syncResults = autoSync.SyncProperty<Hero>(heroNameProperty, GetHeroId);

        // Creates and stores heroes on the clients with same id as server

        var disposables = new List<IDisposable>();
        var clientHeroes = new List<Hero>();
        foreach (var client in TestEnvironement.Clients)
        {
            clientHeroes.Add(client.CreateRegisteredObject<Hero>(networkId));

            disposables.Add(SetupClient(client.Container, syncResults, heroNameProperty));
        }

        float oldWeight = serverHero.Weight;
        float newWeight = 5;

        var client1 = TestEnvironement.Clients.First();

        // Act
        client1.Call(() =>
        {
            serverHero.Weight = newWeight;
        });

        // Assert
        Assert.Equal(oldWeight, serverHero.Weight);


        foreach (var clientHero in clientHeroes)
        {
            Assert.Equal(oldWeight, clientHero.Weight);
        }
    }

    private static readonly ILogger Logger = LogManager.GetLogger<HeroSetNameAuto>();
    private IDisposable SetupClient(IContainer container, ISyncResults syncResults, PropertyInfo property)
    {
        var messageBroker = container.Resolve<IMessageBroker>();
        var objectManager = container.Resolve<IObjectManager>();
        var network = container.Resolve<INetwork>();

        var typeMapper = container.Resolve<ISerializableTypeMapper>();

        typeMapper.AddTypes(syncResults.SerializableTypes);

        return (IAutoSyncHandlerTemplate)Activator.CreateInstance(syncResults.HandlerType, 
            new object[] { messageBroker, objectManager, network, Logger, property })!;
    }
}
