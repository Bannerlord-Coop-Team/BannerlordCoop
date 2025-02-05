using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.Core;

namespace GameInterface.Services.CraftingService
{
    public class CraftingSync : IAutoSync
    {
        public CraftingSync(IAutoSyncBuilder autoSyncBuilder) 
        {
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Crafting), nameof(Crafting._currentHistoryIndex)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Crafting), nameof(Crafting._craftedItemObject)));

            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Crafting), nameof(Crafting.CurrentWeaponDesign)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Crafting), nameof(Crafting.CurrentItemModifierGroup)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Crafting), nameof(Crafting.CraftedWeaponName)));
        }
    }
}
