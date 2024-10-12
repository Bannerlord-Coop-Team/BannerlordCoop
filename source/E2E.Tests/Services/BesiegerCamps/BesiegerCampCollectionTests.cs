using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.BesiegerCamps.Patches;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using Xunit.Abstractions;

namespace E2E.Tests.Services.BesiegerCamps
{
    public class BesiegerCampCollectionTests : IDisposable
    {
        private List<MethodBase> disabledMethods = new();

        private E2ETestEnvironment TestEnvironment { get; }

        private EnvironmentInstance Server => TestEnvironment.Server;
        private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

        private IEnumerable<EnvironmentInstance> AllEnvironmentInstances => Clients.Append(Server);

        public BesiegerCampCollectionTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);
            DisableMethods();
        }

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
        }

        private T ServerCreateObject<T>(out string objectId)
        {
            string? id = null;
            T? obj = default;

            Server.Call(() =>
            {
                obj = GameObjectCreator.CreateInitializedObject<T>();
                Assert.True(Server.ObjectManager.TryGetId(obj, out id));
            }, disabledMethods);

            objectId = id!;
            return obj ?? throw new InvalidOperationException("Failed to create object.");
        }

        [Fact]
        private void ServerAddBesiegerParty_SyncAllClients()
        {
            //Arrange
            var serverBesiegerCamp = ServerCreateObject<BesiegerCamp>(out string besiegerCampId);
            var serverBesiegerParty = ServerCreateObject<MobileParty>(out string besiegerPartyId);

            //Act
            Server.Call(() =>
            {
                BesiegerCampCollectionPatches.ListAddOverride(serverBesiegerCamp._besiegerParties, serverBesiegerParty, serverBesiegerCamp);
                Assert.Contains<MobileParty>(serverBesiegerParty, serverBesiegerCamp._besiegerParties);
            });

            //Assert
            foreach (var client in Clients.Append(Server))
            {
                Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var clientBesiegerCamp));
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(besiegerPartyId, out var clientBesiegerParty));
                Assert.Contains<MobileParty>(clientBesiegerParty, clientBesiegerCamp._besiegerParties);
            }
        }

        [Fact]
        private void ServerRemoveBesiegerParty_SyncAllClients()
        {
            // Arrange
            var serverBesiegerCamp = ServerCreateObject<BesiegerCamp>(out string besiegerCampId);
            var serverBesiegerParty = ServerCreateObject<MobileParty>(out string besiegerPartyId);
            Server.Call(() =>
            {
                BesiegerCampCollectionPatches.ListAddOverride(serverBesiegerCamp._besiegerParties, serverBesiegerParty, serverBesiegerCamp);
                Assert.Contains<MobileParty>(serverBesiegerParty, serverBesiegerCamp._besiegerParties);
            });
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var clientBesiegerCamp));
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(besiegerPartyId, out var clientBesiegerParty));
                Assert.Contains(clientBesiegerParty, clientBesiegerCamp._besiegerParties);
            }

            // Act
            Server.Call(() =>
            {
                BesiegerCampCollectionPatches.ListRemoveOverride(serverBesiegerCamp._besiegerParties, serverBesiegerParty, serverBesiegerCamp);
            });

            // Assert
            foreach (var client in Clients.Append(Server).Append(Server))
            {
                Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var clientBesiegerCamp));
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(besiegerPartyId, out var clientBesiegerParty));
                Assert.Empty(clientBesiegerCamp._besiegerParties);
            }
        }

        [Fact]
        private void ClientAddBesiegerParty_DoesNothing()
        {
            // Arrange
            var serverBesiegerCamp = ServerCreateObject<BesiegerCamp>(out string besiegerCampId);
            var serverBesiegerParty = ServerCreateObject<MobileParty>(out string besiegerPartyId);
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var syncedCamp));
            }

            // Act
            var firstClient = Clients.First();
            firstClient.Call(() =>
            {
                Assert.True(firstClient.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var clientBesiegerCamp));
                Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(besiegerPartyId, out var clientBesiegerParty));
                BesiegerCampCollectionPatches.ListAddOverride(clientBesiegerCamp._besiegerParties, clientBesiegerParty, clientBesiegerCamp);
            });

            // Assert
            foreach (var client in Clients.Where(c => c != firstClient).Append(Server))
            {
                Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var clientBesiegerCamp));
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(besiegerPartyId, out var clientBesiegerParty));
                Assert.DoesNotContain<MobileParty>(clientBesiegerParty, clientBesiegerCamp._besiegerParties);
            }
        }

        [Fact]
        private void ClientRemoveBesiegerParty_DoesNothing()
        {
            // Arrange
            var serverBesiegerCamp = ServerCreateObject<BesiegerCamp>(out string besiegerCampId);
            var serverBesiegerParty = ServerCreateObject<MobileParty>(out string besiegerPartyId);
            Server.Call(() =>
            {
                /// Server adds besiegerParty to besiegerCamp
                BesiegerCampCollectionPatches.ListAddOverride(serverBesiegerCamp._besiegerParties, serverBesiegerParty, serverBesiegerCamp);
                Assert.Contains<MobileParty>(serverBesiegerParty, serverBesiegerCamp._besiegerParties);
            });

            foreach (var client in AllEnvironmentInstances)
            {
                /// All clients sync
                Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var clientBesiegerCamp));
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(besiegerPartyId, out var clientBesiegerParty));
                Assert.Contains<MobileParty>(clientBesiegerParty, clientBesiegerCamp._besiegerParties);
            }

            // Act
            var firstClient = Clients.First();
            firstClient.Call(() =>
            {
                /// A client removes besiegerParty from besiegerCamp
                Assert.True(firstClient.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var clientBesiegerCamp));
                Assert.True(firstClient.ObjectManager.TryGetObject<MobileParty>(besiegerPartyId, out var clientBesiegerParty));
                BesiegerCampCollectionPatches.ListRemoveOverride(clientBesiegerCamp._besiegerParties, clientBesiegerParty, clientBesiegerCamp);
            });

            // Assert
            foreach (var client in AllEnvironmentInstances.Where(c => c != firstClient))
            {
                /// It affects no one
                Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var clientBesiegerCamp));
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(besiegerPartyId, out var clientBesiegerParty));
                Assert.Contains<MobileParty>(clientBesiegerParty, clientBesiegerCamp._besiegerParties);
            }
        }
    }
}