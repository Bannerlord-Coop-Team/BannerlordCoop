using Common.Util;
using E2E.Tests.Environment;
using HarmonyLib;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.CraftingService
{
    public class CraftingSyncTests : IDisposable
    {
        E2ETestEnvironment TestEnvironment { get; }
        public CraftingSyncTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void ServerCreateCrafting_SyncAllClients()
        {
            // Arrange
            var server = TestEnvironment.Server;

            // Act
            string? craftingId = null;

            var currentHistoryIndexField = AccessTools.Field(typeof(Crafting), nameof(Crafting._currentHistoryIndex));
            var craftedItemObjectField = AccessTools.Field(typeof(Crafting), nameof(Crafting._craftedItemObject));

            var currentHistoryIntercept = TestEnvironment.GetIntercept(currentHistoryIndexField);
            var craftingItemObjectIntercept = TestEnvironment.GetIntercept(craftedItemObjectField);
            

            server.Call(() =>
            {
                CraftingTemplate craftingTemplate = new CraftingTemplate();
                CultureObject cultureObject = new CultureObject();
                Crafting crafting = new Crafting(craftingTemplate, cultureObject, new TextObject("test"));
                

                Assert.True(server.ObjectManager.TryGetId(crafting, out craftingId));
            });

            // Assert
            Assert.True(server.ObjectManager.TryGetObject(craftingId, out Crafting serverCrafting));

            server.Call(() =>
            {
                currentHistoryIntercept.Invoke(null, new object[] { serverCrafting, 69 });
                craftingItemObjectIntercept.Invoke(null, new object[] { serverCrafting, new ItemObject("item") });
                ItemModifierGroup img = new ItemModifierGroup();
                ItemModifier modifier = new ItemModifier();
                modifier.Armor = 10;
                modifier.Damage = 10;
                modifier.ChargeDamage = 10;
                modifier.HitPoints = 10;
                modifier.ItemQuality = ItemQuality.Poor;
                modifier.LootDropScore = 10;
                modifier.Maneuver = 10;
                modifier.MissileSpeed = 10;
                modifier.MountHitPoints = 10;
                modifier.MountSpeed = 10;
                modifier.Name = new TextObject("test");
                modifier.PriceMultiplier = 10;
                modifier.ProductionDropScore = 10;
                modifier.Speed = 10;
                modifier.StackCount = 10;
                img.AddItemModifier(modifier);
                serverCrafting.CurrentItemModifierGroup = img;
                serverCrafting.CraftedWeaponName = new TextObject("craftedWeapon");
                WeaponDesign weaponDesign = ObjectHelper.SkipConstructor<WeaponDesign>();
                serverCrafting.CurrentWeaponDesign = weaponDesign;
            });

            server.ObjectManager.TryGetId(serverCrafting._craftedItemObject, out string serverObjectId);

            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject(craftingId, out Crafting clientCrafting));
                Assert.Equal(serverCrafting._currentHistoryIndex, clientCrafting._currentHistoryIndex);

                client.ObjectManager.TryGetId(clientCrafting._craftedItemObject, out string clientObjectId);

                Assert.Equal(serverObjectId, clientObjectId);
                Assert.Equal(serverCrafting.CurrentWeaponDesign, clientCrafting.CurrentWeaponDesign);
                Assert.Equal(serverCrafting.CurrentItemModifierGroup.ItemModifiers.Count, clientCrafting.CurrentItemModifierGroup.ItemModifiers.Count);
                Assert.Equal(serverCrafting.CraftedWeaponName.ToString(), clientCrafting.CraftedWeaponName.ToString());
            }
        }
    }
}
