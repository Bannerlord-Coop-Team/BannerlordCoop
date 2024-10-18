using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
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
        private readonly List<MethodBase> disabledMethods;
        private E2ETestEnvironment TestEnvironment { get; }
        private EnvironmentInstance Server => TestEnvironment.Server;
        private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;
        private IEnumerable<EnvironmentInstance> AllEnvironmentInstances => Clients.Append(Server);

        private readonly string besiegerCampId;
        private readonly string besiegerPartyId;

        public BesiegerCampCollectionTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);

            disabledMethods = new List<MethodBase> {
                AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyJoinedSiegeInternal)),
                AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.SetSiegeCampPartyPosition)),
                AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide))
            };

            besiegerCampId = TestEnvironment.CreateRegisteredObject<BesiegerCamp>(disabledMethods);
            besiegerPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>(disabledMethods);
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        private void ServerAddBesiegerParty_SyncAllClients()
        {
            //Arrange
            Assert.True(Server.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var serverBesiegerCamp));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(besiegerPartyId, out var serverBesiegerParty));

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
            Assert.True(Server.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var serverBesiegerCamp));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(besiegerPartyId, out var serverBesiegerParty));

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
            Assert.True(Server.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var serverBesiegerCamp));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(besiegerPartyId, out var serverBesiegerParty));

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
            Assert.True(Server.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var serverBesiegerCamp));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(besiegerPartyId, out var serverBesiegerParty));

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