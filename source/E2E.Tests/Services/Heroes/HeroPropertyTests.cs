using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Serialization;
using Coop.IntegrationTests.Environment;
using E2E.Tests.Environment;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.AutoSync.Template;
using GameInterface.Utils.AutoSync;
using HarmonyLib;
using Serilog;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;
using Autofac;
using TaleWorlds.Core;

namespace E2E.Tests.Services.Heroes
{
    /// <summary>
    /// Tests for hero properties.
    /// </summary>
    public class HeroPropertyTests : IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetLogger<HeroPropertyTests>();
        E2ETestEnvironment TestEnvironment { get; }

        /// <summary>
        /// Constructor for HeroSetNameAuto class.
        /// </summary>
        /// <param name="output">The output interface for writing test results.</param>
        public HeroPropertyTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);
        }

        /// <summary>
        /// Disposes the test environment.
        /// </summary>
        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        /// <summary>
        /// Retrieves the hero ID.
        /// </summary>
        /// <param name="hero">The hero object.</param>
        /// <returns>The hero ID.</returns>
        public static string GetHeroId(Hero hero) => hero.StringId;

        /// <summary>
        /// Test method to verify that the server sets StaticBodyProperties and syncs all clients.
        /// </summary>
        [Fact]
        public void ServerSetStaticBodyProperties_SyncAllClients()
        {
            // Arrange
            var server = TestEnvironment.Server;

            var networkId = "CoopHero_1";
            var serverHero = TestEnvironment.Server.CreateRegisteredObject<Hero>(networkId);

            var autoSync = server.Container.Resolve<IAutoSync>();
            var heroProperty = AccessTools.Property(typeof(Hero), nameof(Hero.StaticBodyProperties));
            var syncResults = autoSync.SyncProperty<Hero>(heroProperty, GetHeroId);

            var disposables = new List<IDisposable>();
            var clientHeroes = new List<Hero>();
            foreach (var client in TestEnvironment.Clients)
            {
                clientHeroes.Add(client.CreateRegisteredObject<Hero>(networkId));
                disposables.Add(SetupClient(client.Container, syncResults, heroProperty));
            }

            StaticBodyProperties newProperty = new StaticBodyProperties(1,2,3,4,5,6,7,8);

            // Act
            server.Call(() =>
            {
                serverHero.StaticBodyProperties = newProperty;
            });

            // Assert
            Assert.Equal(newProperty, serverHero.StaticBodyProperties);

            foreach (var clientHero in clientHeroes)
            {
                Assert.Equal(newProperty, clientHero.StaticBodyProperties);
            }
        }

        /// <summary>
        /// Test method to verify that the client setting StaticBodyProperties does nothing.
        /// </summary>
        [Fact]
        public void ClientSetStaticBodyProperties_DoesNothing()
        {
            // Arrange
            var server = TestEnvironment.Server;

            var networkId = "CoopHero_1";
            var serverHero = TestEnvironment.Server.CreateRegisteredObject<Hero>(networkId);

            var autoSync = server.Container.Resolve<IAutoSync>();
            var heroProperty = AccessTools.Property(typeof(Hero), nameof(Hero.StaticBodyProperties));
            var syncResults = autoSync.SyncProperty<Hero>(heroProperty, GetHeroId);

            var disposables = new List<IDisposable>();
            var clientHeroes = new List<Hero>();
            foreach (var client in TestEnvironment.Clients)
            {
                clientHeroes.Add(client.CreateRegisteredObject<Hero>(networkId));
                disposables.Add(SetupClient(client.Container, syncResults, heroProperty));
            }

            StaticBodyProperties oldProperty = serverHero.StaticBodyProperties;
            StaticBodyProperties newProperty = new StaticBodyProperties(1, 2, 3, 4, 5, 6, 7, 8);

            var client1 = TestEnvironment.Clients.First();

            // Act
            client1.Call(() =>
            {
                serverHero.StaticBodyProperties = newProperty;
            });

            // Assert
            Assert.Equal(oldProperty, serverHero.StaticBodyProperties);

            foreach (var clientHero in clientHeroes)
            {
                Assert.Equal(oldProperty, clientHero.StaticBodyProperties);
            }
        }

        /// <summary>
        /// Test method to verify that the server sets Weight and syncs all clients.
        /// </summary>
        [Fact]
        public void ServerSetWeight_SyncAllClients()
        {
            // Arrange
            var server = TestEnvironment.Server;

            var networkId = "CoopHero_1";
            var serverHero = TestEnvironment.Server.CreateRegisteredObject<Hero>(networkId);

            var autoSync = server.Container.Resolve<IAutoSync>();
            var heroProperty = AccessTools.Property(typeof(Hero), nameof(Hero.Weight));
            var syncResults = autoSync.SyncProperty<Hero>(heroProperty, GetHeroId);

            var disposables = new List<IDisposable>();
            var clientHeroes = new List<Hero>();
            foreach (var client in TestEnvironment.Clients)
            {
                clientHeroes.Add(client.CreateRegisteredObject<Hero>(networkId));
                disposables.Add(SetupClient(client.Container, syncResults, heroProperty));
            }

            var newProperty = 5;

            // Act
            server.Call(() =>
            {
                serverHero.Weight = newProperty;
            });

            // Assert
            Assert.Equal(newProperty, serverHero.Weight);

            foreach (var clientHero in clientHeroes)
            {
                Assert.Equal(newProperty, clientHero.Weight);
            }
        }

        /// <summary>
        /// Test method to verify that the client setting Weight does nothing.
        /// </summary>
        [Fact]
        public void ClientSetWeight_DoesNothing()
        {
            // Arrange
            var server = TestEnvironment.Server;

            var networkId = "CoopHero_1";
            var serverHero = TestEnvironment.Server.CreateRegisteredObject<Hero>(networkId);

            var autoSync = server.Container.Resolve<IAutoSync>();
            var heroProperty = AccessTools.Property(typeof(Hero), nameof(Hero.Weight));
            var syncResults = autoSync.SyncProperty<Hero>(heroProperty, GetHeroId);

            var disposables = new List<IDisposable>();
            var clientHeroes = new List<Hero>();
            foreach (var client in TestEnvironment.Clients)
            {
                clientHeroes.Add(client.CreateRegisteredObject<Hero>(networkId));
                disposables.Add(SetupClient(client.Container, syncResults, heroProperty));
            }

            var oldProperty = serverHero.Weight;
            var newProperty = 0;

            var client1 = TestEnvironment.Clients.First();

            // Act
            client1.Call(() =>
            {
                serverHero.Weight = newProperty;
            });

            // Assert
            Assert.Equal(oldProperty, serverHero.Weight);

            foreach (var clientHero in clientHeroes)
            {
                Assert.Equal(oldProperty, clientHero.Weight);
            }
        }

        /// <summary>
        /// Sets up the client for synchronization.
        /// </summary>
        /// <param name="container">The dependency injection container.</param>
        /// <param name="syncResults">The synchronization results.</param>
        /// <param name="property">The property to sync.</param>
        /// <returns>An IDisposable object for cleanup.</returns>
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
}