using Common.Util;
using E2E.Tests.Environment;
using HarmonyLib;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;
using GameInterface.Tests.Bootstrap.Modules;
using System.ComponentModel;
using Autofac;
using NuGet.Frameworks;
using Newtonsoft.Json.Serialization;

namespace E2E.Tests.Services.ItemObjectService
{
    public class ItemObjectSyncTest : IDisposable
    {
        E2ETestEnvironment TestEnvironment { get; }
        public ItemObjectSyncTest(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void ServerCreateItemObject_SyncAllClients()
        {
            // Arrange
            var server = TestEnvironment.Server;

            // Act
            string? itemObjectId = null;

            var typeField = AccessTools.Field(typeof(ItemObject), nameof(ItemObject.Type));
            var typeIntercept = TestEnvironment.GetIntercept(typeField);

            var culture = new BasicCultureObject() { Name = new TextObject("Battania") };
            var weaponDesign = ObjectHelper.SkipConstructor<WeaponDesign>();
            var holsterPositionShift = new Vec3(1, 2, 3, 4);
            string[] itemHolsters = { "holster1", "holster2", "holster3" };

            server.Call(() =>
            {
                ItemObject itemObject = new ItemObject();
                Assert.True(server.ObjectManager.TryGetId(itemObject, out itemObjectId));

                itemObject.Weight = 3.5f;
                itemObject.Name = new TextObject("Name");
                itemObject.MultiMeshName = "MultiMeshName";
                itemObject.HolsterMeshName = "HolsterMeshName";
                itemObject.HolsterWithWeaponMeshName = "WithWeaponMeshName";
                //itemObject.HolsterPositionShift = holsterPositionShift;
                itemObject.FlyingMeshName = "FlyingMeshName";
                itemObject.BodyName = "BodyName";
                itemObject.HolsterBodyName = "HolsterBodyName";
                itemObject.CollisionBodyName = "CollisionBodyName";
                itemObject.RecalculateBody = false;
                itemObject.Culture = culture;
                itemObject.Difficulty = 1;
                itemObject.ScaleFactor = 3.5f;
                itemObject.ItemFlags = ItemFlags.Civilian;
                itemObject.Appearance = 3.5f;
                itemObject.WeaponDesign = weaponDesign;
                itemObject.ItemHolsters = itemHolsters;
            });

            // Assert
            Assert.True(server.ObjectManager.TryGetObject(itemObjectId, out ItemObject itemObject));

            server.Call(() =>
            {
                typeIntercept.Invoke(null, new object[] { itemObject, 69 });
            });

            server.ObjectManager.TryGetId(itemObject, out string serverObjectId);
            server.ObjectManager.TryGetId(itemObject.Culture, out string serverCultureId);
            server.ObjectManager.TryGetId(itemObject.WeaponDesign, out string serverWeaponDesignId);
            server.ObjectManager.TryGetId(itemObject.ItemHolsters, out string  serverItemHolstersId);

            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject(serverObjectId, out ItemObject clientItemObject));
                
                //Field
                Assert.Equal(itemObject.Type, clientItemObject.Type);

                client.ObjectManager.TryGetId(clientItemObject, out string clientObjectId);
                client.ObjectManager.TryGetId(clientItemObject.Culture, out string clientCultureId);
                client.ObjectManager.TryGetId(clientItemObject.WeaponDesign, out string clientWeaponDesignId);
                client.ObjectManager.TryGetId(clientItemObject.ItemHolsters, out string clientItemHolstersId);
                Assert.Equal(serverObjectId, clientObjectId);

                //Properties
                Assert.Equal(3.5f, clientItemObject.Weight);
                Assert.Equal("Name", clientItemObject.Name.ToString());
                Assert.Equal("MultiMeshName", clientItemObject.MultiMeshName);
                Assert.Equal("HolsterMeshName", clientItemObject.HolsterMeshName);
                Assert.Equal("WithWeaponMeshName", clientItemObject.HolsterWithWeaponMeshName);
                //Assert.Equal(holsterPositionShift, clientItemObject.HolsterPositionShift);
                Assert.Equal("FlyingMeshName", clientItemObject.FlyingMeshName);
                Assert.Equal("BodyName", clientItemObject.BodyName);
                Assert.Equal("HolsterBodyName", clientItemObject.HolsterBodyName);
                Assert.Equal("CollisionBodyName", clientItemObject.CollisionBodyName);
                Assert.False(clientItemObject.RecalculateBody);
                Assert.Equal(clientCultureId, serverCultureId);
                Assert.Equal(1, clientItemObject.Difficulty);
                Assert.Equal(3.5f, clientItemObject.ScaleFactor);
                Assert.Equal(ItemFlags.Civilian, clientItemObject.ItemFlags);
                Assert.Equal(3.5f, clientItemObject.Appearance);
                Assert.Equal(clientWeaponDesignId, serverWeaponDesignId);

                //Collection
                Assert.Equal(itemHolsters, clientItemObject.ItemHolsters);
                Assert.Equal(itemHolsters[0], clientItemObject.ItemHolsters[0]);
                Assert.Equal(serverItemHolstersId, clientItemHolstersId);
            }
        }
    }
}
