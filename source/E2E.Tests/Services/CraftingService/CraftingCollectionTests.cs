using System.Reflection;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.CraftingService.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.CraftingService
{
    public class CraftingCollectionTests : IDisposable
    {
        private readonly List<MethodBase> disabledMethods;
        private E2ETestEnvironment TestEnvironment { get; }
        private EnvironmentInstance Server => TestEnvironment.Server;
        private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;
        private IEnumerable<EnvironmentInstance> AllEnvironmentInstances => Clients.Append(Server);

        private readonly string craftingId;
        private readonly string weaponDesignId;

        public CraftingCollectionTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);

            disabledMethods = new List<MethodBase>();

            craftingId = TestEnvironment.CreateRegisteredObject<Crafting>(disabledMethods);
            weaponDesignId = TestEnvironment.CreateRegisteredObject<WeaponDesign>(disabledMethods);
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        private void ServerAddWeaponDesign_SyncAllClients()
        {
            //Arrange
            Assert.True(Server.ObjectManager.TryGetObject<Crafting>(craftingId, out var serverCrafting));
            Assert.True(Server.ObjectManager.TryGetObject<WeaponDesign>(weaponDesignId, out var serverWeaponDesign));

            //Act
            Server.Call(() =>
            {
                CraftingCollectionPatches.ListAddOverride(serverCrafting._history, serverWeaponDesign, serverCrafting);
                Assert.Contains<WeaponDesign>(serverWeaponDesign, serverCrafting._history);
            });

            //Assert
            foreach (var client in Clients.Append(Server))
            {
                Assert.True(client.ObjectManager.TryGetObject<Crafting>(craftingId, out var clientCrafting));
                Assert.True(client.ObjectManager.TryGetObject<WeaponDesign>(weaponDesignId, out var clientWeaponDesign));
                Assert.Contains<WeaponDesign>(clientWeaponDesign, clientCrafting._history);
            }
        }

        [Fact]
        private void ServerRemoveWeaponDesign_SyncAllClients()
        {
            // Arrange
            Assert.True(Server.ObjectManager.TryGetObject<Crafting>(craftingId, out var serverCrafting));
            Assert.True(Server.ObjectManager.TryGetObject<WeaponDesign>(weaponDesignId, out var serverWeaponDesign));

            Server.Call(() =>
            {
                CraftingCollectionPatches.ListAddOverride(serverCrafting._history, serverWeaponDesign, serverCrafting);
                Assert.Contains<WeaponDesign>(serverWeaponDesign, serverCrafting._history);
            });

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Crafting>(craftingId, out var clientCrafting));
                Assert.True(client.ObjectManager.TryGetObject<WeaponDesign>(weaponDesignId, out var clientWeaponDesign));
                Assert.Contains(clientWeaponDesign, clientCrafting._history);
            }

            // Act
            Server.Call(() =>
            {
                CraftingCollectionPatches.ListRemoveOverride(serverCrafting._history, serverWeaponDesign, serverCrafting);
            });

            // Assert
            foreach (var client in Clients.Append(Server).Append(Server))
            {
                Assert.True(client.ObjectManager.TryGetObject<Crafting>(craftingId, out var clientCrafting));
                Assert.True(client.ObjectManager.TryGetObject<WeaponDesign>(weaponDesignId, out var clientWeaponDesign));
                Assert.Empty(clientCrafting._history);
            }
        }

        [Fact]
        private void ClientAddWeaponDesign_DoesNothing()
        {
            // Arrange
            Assert.True(Server.ObjectManager.TryGetObject<Crafting>(craftingId, out var serverCrafting));
            Assert.True(Server.ObjectManager.TryGetObject<WeaponDesign>(weaponDesignId, out var serverWeaponDesign));

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Crafting>(craftingId, out var syncedCamp));
            }

            // Act
            var firstClient = Clients.First();
            firstClient.Call(() =>
            {
                Assert.True(firstClient.ObjectManager.TryGetObject<Crafting>(craftingId, out var clientCrafting));
                Assert.True(firstClient.ObjectManager.TryGetObject<WeaponDesign>(weaponDesignId, out var clientWeaponDesign));
                CraftingCollectionPatches.ListAddOverride(clientCrafting._history, clientWeaponDesign, clientCrafting);
            });

            // Assert
            foreach (var client in Clients.Where(c => c != firstClient).Append(Server))
            {
                Assert.True(client.ObjectManager.TryGetObject<Crafting>(craftingId, out var clientCrafting));
                Assert.True(client.ObjectManager.TryGetObject<WeaponDesign>(weaponDesignId, out var clientWeaponDesign));
                Assert.DoesNotContain<WeaponDesign>(clientWeaponDesign, clientCrafting._history);
            }
        }

        [Fact]
        private void ClientRemoveWeaponDesign_DoesNothing()
        {
            // Arrange
            Assert.True(Server.ObjectManager.TryGetObject<Crafting>(craftingId, out var serverCrafting));
            Assert.True(Server.ObjectManager.TryGetObject<WeaponDesign>(weaponDesignId, out var serverWeaponDesign));

            Server.Call(() =>
            {
                // Server adds weaponDesign to crafting
                CraftingCollectionPatches.ListAddOverride(serverCrafting._history, serverWeaponDesign, serverCrafting);
                Assert.Contains<WeaponDesign>(serverWeaponDesign, serverCrafting._history);
            });

            foreach (var client in AllEnvironmentInstances)
            {
                // All clients sync
                Assert.True(client.ObjectManager.TryGetObject<Crafting>(craftingId, out var clientCrafting));
                Assert.True(client.ObjectManager.TryGetObject<WeaponDesign>(weaponDesignId, out var clientWeaponDesign));
                Assert.Contains<WeaponDesign>(clientWeaponDesign, clientCrafting._history);
            }

            // Act
            var firstClient = Clients.First();
            firstClient.Call(() =>
            {
                // A client removes weaponDesign from crafting
                Assert.True(firstClient.ObjectManager.TryGetObject<Crafting>(craftingId, out var clientCrafting));
                Assert.True(firstClient.ObjectManager.TryGetObject<WeaponDesign>(weaponDesignId, out var clientWeaponDesign));
                CraftingCollectionPatches.ListRemoveOverride(clientCrafting._history, clientWeaponDesign, clientCrafting);
            });

            // Assert
            foreach (var client in AllEnvironmentInstances.Where(c => c != firstClient))
            {
                // It affects no one
                Assert.True(client.ObjectManager.TryGetObject<Crafting>(craftingId, out var clientCrafting));
                Assert.True(client.ObjectManager.TryGetObject<WeaponDesign>(weaponDesignId, out var clientWeaponDesign));
                Assert.Contains<WeaponDesign>(clientWeaponDesign, clientCrafting._history);
            }
        }
    }
}