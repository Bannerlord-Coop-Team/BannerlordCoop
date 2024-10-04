using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using Xunit.Abstractions;
using GameInterface.Services.BesiegerCamps.Patches;

namespace E2E.Tests.Services.BesiegerCamps
{
    public class BesiegerCampCollectionTests : IDisposable
    {
        private List<MethodBase> disabledMethods = new();

        private E2ETestEnvironment TestEnvironment { get; }

        private EnvironmentInstance Server => TestEnvironment.Server;
        private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

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
        void ServerAddBesiegerParty_SyncAllClients()
        {
            //Arrange
            var besiegerCamp = ServerCreateObject<BesiegerCamp>(out string besiegerCampId);
            var besiegerParty = ServerCreateObject<MobileParty>(out string besiegerPartyId);

            //Act
            Server.Call(() =>
            {
                //besiegerCamp._besiegerParties.Add(besiegerParty); // Doesn't call override, must call it directly
                BesiegerCampCollectionPatches.ListAddOverride(besiegerCamp._besiegerParties, besiegerParty, besiegerCamp);
            });

            //Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var clientBesiegerCamp));
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(besiegerPartyId, out var clientBesiegerParty));
                Assert.Contains<MobileParty>(clientBesiegerParty, clientBesiegerCamp._besiegerParties);
            }
        }

        [Fact]
        void ServerRemoveBesiegerParty_SyncAllClients()
        {
            // Arrange 
            var besiegerCamp = ServerCreateObject<BesiegerCamp>(out string besiegerCampId);
            var besiegerParty = ServerCreateObject<MobileParty>(out string besiegerPartyId);
            Server.Call(() =>
            {
                BesiegerCampCollectionPatches.ListAddOverride(besiegerCamp._besiegerParties, besiegerParty, besiegerCamp);
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
                BesiegerCampCollectionPatches.ListRemoveOverride(besiegerCamp._besiegerParties, besiegerParty, besiegerCamp);
            });

            // Assert 
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var clientBesiegerCamp));
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(besiegerPartyId, out var clientBesiegerParty));
                Assert.Empty(clientBesiegerCamp._besiegerParties);
            }
        }

        [Fact]
        void ClientAddBesiegerParty_DoesNothing()
        {
            // Arrange 
            var besiegerCamp = ServerCreateObject<BesiegerCamp>(out string besiegerCampId);
            var besiegerParty = ServerCreateObject<MobileParty>(out string besiegerPartyId);
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var syncedCamp));
            }

            // Act
            var firstClient = Clients.First();
            firstClient.Call(() =>
            {
                BesiegerCampCollectionPatches.ListAddOverride(besiegerCamp._besiegerParties, besiegerParty, besiegerCamp);
            });

            // Assert
            foreach (var client in Clients.Where(c => c != firstClient))
            {
                Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var clientBesiegerCamp));
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(besiegerPartyId, out var clientBesiegerParty));
                Assert.DoesNotContain<MobileParty>(clientBesiegerParty, clientBesiegerCamp._besiegerParties);
            }
        }

        [Fact]
        void ClientRemoveBesiegerParty_DoesNothing()
        {
            // Arrange
            var besiegerCamp = ServerCreateObject<BesiegerCamp>(out string besiegerCampId);
            var besiegerParty = ServerCreateObject<MobileParty>(out string besiegerPartyId);
            Server.Call(() =>
            {
                BesiegerCampCollectionPatches.ListAddOverride(besiegerCamp._besiegerParties, besiegerParty, besiegerCamp);
            });
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var clientBesiegerCamp));
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(besiegerPartyId, out var clientBesiegerParty));
                Assert.Contains<MobileParty>(clientBesiegerParty, clientBesiegerCamp._besiegerParties);
            }

            // Act 
            var firstClient = Clients.First();
            firstClient.Call(() =>
            {
                BesiegerCampCollectionPatches.ListRemoveOverride(besiegerCamp._besiegerParties, besiegerParty, besiegerCamp);
            });

            // Assert 
            foreach (var client in Clients.Where(c => c != firstClient))
            {
                Assert.True(client.ObjectManager.TryGetObject<BesiegerCamp>(besiegerCampId, out var clientBesiegerCamp));
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(besiegerPartyId, out var clientBesiegerParty));
                Assert.Contains<MobileParty>(clientBesiegerParty, clientBesiegerCamp._besiegerParties);
            }
        }


    }
}
