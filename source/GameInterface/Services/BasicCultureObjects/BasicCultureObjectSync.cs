using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.Core;

namespace GameInterface.Services.BasicCultureObjects
{
    internal class BasicCultureObjectSync : IDynamicSync
    {
        public BasicCultureObjectSync(DynamicSyncRegistry dynamicSyncRegistry)
        {
            // Fields

            // Properties
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.BackgroundColor1)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.BackgroundColor2)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.Banner)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.CanHaveSettlement)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.ClothAlternativeColor)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.ClothAlternativeColor2)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.Color)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.Color2)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.EncounterBackgroundMesh)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.ForegroundColor1)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.ForegroundColor2)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.IsBandit)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.IsMainCulture)));
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.Name)));

            // Targetmethods
        }
    }
}
