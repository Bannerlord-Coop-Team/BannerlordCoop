using Common.Util;
using E2E.Tests.Environment;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.ItemObjects
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
            string[] itemHolsters = { "holster12", "holster23", "holster34" };
            var itemComponent = new TradeItemComponent();
            var itemCategory = ObjectHelper.SkipConstructor<ItemCategory>();
            var prerequisiteItem = new ItemObject();
            var itemTypeEnum = ItemObject.ItemTypeEnum.OneHandedWeapon;

            server.Call(() =>
            {
                ItemObject itemObject = new ItemObject();
                Assert.True(server.ObjectManager.TryGetId(itemObject, out itemObjectId));

                itemObject.Weight = 3.5f;
                itemObject.Name = new TextObject("Name");
                itemObject.MultiMeshName = "MultiMeshName";
                itemObject.HolsterMeshName = "HolsterMeshName";
                itemObject.HolsterWithWeaponMeshName = "WithWeaponMeshName";
                itemObject.HolsterPositionShift = holsterPositionShift;
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

                //Rest of Properties
                itemObject.ItemComponent = itemComponent;
                itemObject.HasLowerHolsterPriority = true;
                itemObject.PrefabName = "PrefabName";
                itemObject.ItemCategory = itemCategory;
                itemObject.Value = 1;
                itemObject.Effectiveness = 3.5f;
                itemObject.IsUsingTableau = true;
                itemObject.ArmBandMeshName = "ArmBandMeshName";
                itemObject.IsFood = true;
                itemObject.IsUniqueItem = true;
                itemObject.MultiplayerItem = true;
                itemObject.NotMerchandise = true;
                itemObject.IsCraftedByPlayer = true;
                itemObject.LodAtlasIndex = 1;
                itemObject.PrerequisiteItem = prerequisiteItem;
                itemObject.ItemType = itemTypeEnum;
            });

            // Assert
            Assert.True(server.ObjectManager.TryGetObject(itemObjectId, out ItemObject itemObject));

            server.Call(() =>
            {
                //typeIntercept.Invoke(null, new object[] { itemObject, 69 });
            });

            Assert.True(server.ObjectManager.TryGetId(itemObject, out string serverObjectId));
            Assert.True(server.ObjectManager.TryGetId(itemObject.Culture, out string serverCultureId));
            Assert.True(server.ObjectManager.TryGetId(itemObject.WeaponDesign, out string serverWeaponDesignId));
            Assert.True(server.ObjectManager.TryGetId(itemObject.ItemHolsters, out string  serverItemHolstersId));
            Assert.True(server.ObjectManager.TryGetId(itemObject.ItemComponent, out string serverItemComponentId));
            Assert.True(server.ObjectManager.TryGetId(itemObject.ItemCategory, out string serverItemCategoryId));
            Assert.True(server.ObjectManager.TryGetId(itemObject.PrerequisiteItem, out string serverPrerequisiteItemId));

            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject(serverObjectId, out ItemObject clientItemObject));
                
                //Field
                Assert.Equal(itemObject.Type, clientItemObject.Type);

                Assert.True(client.ObjectManager.TryGetId(clientItemObject, out string clientObjectId));
                Assert.True(client.ObjectManager.TryGetId(clientItemObject.Culture, out string clientCultureId));
                Assert.True(client.ObjectManager.TryGetId(clientItemObject.WeaponDesign, out string clientWeaponDesignId));
                Assert.True(client.ObjectManager.TryGetId(clientItemObject.ItemHolsters, out string clientItemHolstersId));
                Assert.True(client.ObjectManager.TryGetId(clientItemObject.ItemComponent, out string clientItemComponentId));
                Assert.True(client.ObjectManager.TryGetId(clientItemObject.ItemCategory, out string clientItemCategoryId));
                Assert.True(client.ObjectManager.TryGetId(clientItemObject.PrerequisiteItem, out string clientPrerequisiteItemId));
                Assert.Equal(serverObjectId, clientObjectId);


                //Properties
                Assert.Equal(3.5f, clientItemObject.Weight);
                Assert.Equal("Name", clientItemObject.Name.ToString());
                Assert.Equal("MultiMeshName", clientItemObject.MultiMeshName);
                Assert.Equal("HolsterMeshName", clientItemObject.HolsterMeshName);
                Assert.Equal("WithWeaponMeshName", clientItemObject.HolsterWithWeaponMeshName);
                Assert.Equal(holsterPositionShift, clientItemObject.HolsterPositionShift);
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

                //Rest of Properties
                Assert.Equal(serverItemComponentId, clientItemComponentId);
                Assert.True(clientItemObject.HasLowerHolsterPriority);
                Assert.Equal("PrefabName", clientItemObject.PrefabName);
                Assert.Equal(serverItemCategoryId, clientItemCategoryId);
                Assert.Equal(1, clientItemObject.Value);
                Assert.Equal(3.5f, clientItemObject.Effectiveness);
                Assert.True(clientItemObject.IsUsingTableau);
                Assert.Equal("ArmBandMeshName", clientItemObject.ArmBandMeshName);
                Assert.True(clientItemObject.IsFood);
                Assert.True(clientItemObject.IsUniqueItem);
                Assert.True(clientItemObject.MultiplayerItem);
                Assert.True(clientItemObject.NotMerchandise);
                Assert.True(clientItemObject.IsCraftedByPlayer);
                Assert.Equal(1, clientItemObject.LodAtlasIndex);
                Assert.Equal(serverPrerequisiteItemId, clientPrerequisiteItemId);
                Assert.Equal(ItemObject.ItemTypeEnum.OneHandedWeapon, clientItemObject.ItemType);

                //Collection
                Assert.Equal(itemHolsters, clientItemObject.ItemHolsters);
                Assert.Equal(itemHolsters[0], clientItemObject.ItemHolsters[0]);
                Assert.Equal(serverItemHolstersId, clientItemHolstersId);
            }
        }
    }
}
