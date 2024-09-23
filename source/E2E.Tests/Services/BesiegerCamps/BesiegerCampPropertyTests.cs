using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static Common.Extensions.ReflectionExtensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Localization;
using Xunit.Abstractions;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace E2E.Tests.Services.BesiegerCamps
{
    public class BesiegerCampPropertyTests : IDisposable
    {
        private List<MethodBase> disabledMethods = new();

        E2ETestEnvironment TestEnvironment { get; }

        EnvironmentInstance Server => TestEnvironment.Server;
        IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

        readonly string besiegerCampId;
        readonly string siegeEventId;
        readonly string siegeEnginesId;
        readonly string siegePreparationsId;

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        private void DisableMethods()
        {
            disabledMethods = new List<MethodBase> {
                AccessTools.Method(typeof (MobileParty), nameof(MobileParty.OnPartyJoinedSiegeInternal)),
                AccessTools.Method(typeof (BesiegerCamp), nameof(BesiegerCamp.SetSiegeCampPartyPosition)),
                AccessTools.Method(typeof (BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide))
        };

            disabledMethods.AddRange(AccessTools.GetDeclaredConstructors(typeof(SiegeEvent)));
        }

        private T ServerCreateObject<T>(out string objectId)
        {
            string? id = null;
            T? obj = default;

            Server.Call(() =>
            {
                obj = GameObjectCreator.CreateInitializedObject<T>();
                Assert.True(Server.ObjectManager.TryGetId(obj, out id)); // will fail with SiegeStrategy
            }, disabledMethods);

            objectId = id!;
            return obj ?? throw new InvalidOperationException("Failed to create object.");
        }

        public BesiegerCampPropertyTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);
            DisableMethods();

            ServerCreateObject<BesiegerCamp>(out besiegerCampId);
            ServerCreateObject<SiegeEvent>(out siegeEventId);
            ServerCreateObject<SiegeEnginesContainer>(out siegeEnginesId);

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
            Server.Call((Action)(() =>
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
           // Assert.True(Server.ObjectManager.TryGetObject<SiegeEngineConstructionProgress>(siegePreparationsId, out var serverSiegePreparations));

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
