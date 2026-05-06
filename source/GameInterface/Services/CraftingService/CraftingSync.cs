//using GameInterface.AutoSync;
//using GameInterface.DynamicSync;
//using HarmonyLib;
//using TaleWorlds.Core;

//namespace GameInterface.Services.CraftingService
//{
//    public class CraftingSync : IDynamicSync
//    {
//        public CraftingSync(DynamicSyncRegistry dynamicSyncRegistry) 
//        {
//            dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Crafting), nameof(Crafting._currentHistoryIndex)));
//            dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Crafting), nameof(Crafting._craftedItemObject)));

//            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Crafting), nameof(Crafting.CurrentWeaponDesign)));
//            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Crafting), nameof(Crafting.CurrentItemModifierGroup)));
//            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Crafting), nameof(Crafting.CraftedWeaponName)));
//        }
//    }
//}
