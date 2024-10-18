using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using Xunit.Abstractions;
using static Common.Extensions.ReflectionExtensions;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace E2E.Tests.Services.BesiegerCamps
{
    public class BesiegerCampPropertyTests : IDisposable
    {
        private readonly List<MethodBase> disabledMethods;
        private E2ETestEnvironment TestEnvironment { get; }
        private EnvironmentInstance Server => TestEnvironment.Server;
        private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

        private readonly string besiegerCampId;
        private readonly string siegeEventId;
        private readonly string siegeEnginesId;

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        public BesiegerCampPropertyTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);

            disabledMethods = new List<MethodBase> {
                AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyJoinedSiegeInternal)),
                AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.SetSiegeCampPartyPosition)),
                AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide))
            };

            besiegerCampId = TestEnvironment.CreateRegisteredObject<BesiegerCamp>(disabledMethods);
            siegeEventId = TestEnvironment.CreateRegisteredObject<SiegeEvent>(disabledMethods);
            siegeEnginesId = TestEnvironment.CreateRegisteredObject<SiegeEnginesContainer>(disabledMethods);

            foreach (var client in Clients)
            {
                var _besiegerCamp = new BesiegerCamp(null);
                client.ObjectManager.AddExisting(this.besiegerCampId, _besiegerCamp);
            }
        }

        [Fact]
        public void ServerChangeBesiegerCampNumber_SyncAllClients()
        {
            // Arrange
            Assert.True(Server.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var serverBesiegerCamp));

            // Act
            Server.Call(() =>
            {
                serverBesiegerCamp.NumberOfTroopsKilledOnSide = Random<int>();
            });

            // Assert
            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var clientBesiegerCamp));
                Assert.Equal(serverBesiegerCamp.NumberOfTroopsKilledOnSide, clientBesiegerCamp.NumberOfTroopsKilledOnSide);
            }
        }

        [Fact]
        public void ServerChangeBesiegerCampSiegeEvent_SyncAllClients()
        {
            // Arrange
            Assert.True(Server.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var serverBesiegerCamp));
            Assert.True(Server.ObjectManager.TryGetObject<SiegeEvent>(siegeEventId, out var serverSiegeEvent));

            // Act
            Server.Call(() =>
            {
                serverBesiegerCamp.SiegeEvent = serverSiegeEvent;
            });

            // Assert
            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var clientBesiegerCamp));
                client.ObjectManager.TryGetId(clientBesiegerCamp.SiegeEvent, out string clientSiegeEventId);
                Assert.Equal(clientSiegeEventId, siegeEventId);
            }
        }

        [Fact]
        public void ServerChangeBesiegerCampSiegeStrategy_SyncAllClients()
        {
            // Arrange
            Assert.True(Server.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var serverBesiegerCamp));
            var serverSiegeStrategy = GameObjectCreator.CreateInitializedObject<SiegeStrategy>();

            // Act
            Server.Call(() =>
            {
                serverBesiegerCamp.SiegeStrategy = serverSiegeStrategy;
            });

            // Assert
            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var clientBesiegerCamp));
                Assert.Equal(clientBesiegerCamp.SiegeStrategy.StringId, serverSiegeStrategy.StringId);
            }
        }

        [Fact]
        public void ServerChangeBesiegerCampSiegeEngines_SyncAllClients()
        {
            // Arrange
            Assert.True(Server.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var serverBesiegerCamp));
            Assert.True(Server.ObjectManager.TryGetObject<SiegeEnginesContainer>(siegeEnginesId, out var serverSiegeEngines));

            // Act
            Server.Call(() =>
            {
                serverBesiegerCamp.SiegeEngines = serverSiegeEngines;
            });

            // Assert
            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var clientBesiegerCamp));
                client.ObjectManager.TryGetId(clientBesiegerCamp.SiegeEngines, out string clientSiegeEnginesId);
                Assert.Equal(clientSiegeEnginesId, siegeEnginesId);
            }
        }

        [Fact]
        public void ServerCreate_SiegeEngines_SyncAllClients()
        {
            // Arrange
            string? siegeEngineId = null;

            // Act
            Server.Call((() =>
            {
                var siegeEngines = GameObjectCreator.CreateInitializedObject<SiegeEnginesContainer>();

                Assert.True(Server.ObjectManager.TryGetId(siegeEngines, out siegeEngineId));
            }),
            disabledMethods: disabledMethods);

            // Assert
            Assert.NotNull(siegeEngineId);

            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<SiegeEnginesContainer>(siegeEngineId, out var _));
            }
        }

        [Fact]
        public void ServerChangeBesiegerCampPreparationProgress_SyncAllClients()
        {
            // Arrange
            Assert.True(Server.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var serverBesiegerCamp));
            Assert.True(Server.ObjectManager.TryGetObject<SiegeEnginesContainer>(siegeEnginesId, out var serverSiegeEngines));

            float testVal = 51;

            // Act
            Server.Call(() =>
            {
                serverBesiegerCamp.SiegeEngines = serverSiegeEngines;

                serverBesiegerCamp.SiegeEngines.SiegePreparations.SetProgress(testVal);
            });

            // Assert
            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var clientBesiegerCamp));
                client.ObjectManager.TryGetId(clientBesiegerCamp.SiegeEngines, out string clientSiegeEnginesId);
                Assert.Equal(clientSiegeEnginesId, siegeEnginesId);
                Assert.Equal(testVal, clientBesiegerCamp.SiegeEngines.SiegePreparations.Progress);
            }
        }
    }
}