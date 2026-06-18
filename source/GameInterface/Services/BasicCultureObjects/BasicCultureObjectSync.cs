using GameInterface.AutoSync;
using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.Core;

namespace GameInterface.Services.BasicCultureObjects
{
    internal class BasicCultureObjectSync : IAutoSync
    {
        public BasicCultureObjectSync(AutoSyncRegistry AutoSyncRegistry)
        {
            // Fields

            // Properties
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.BackgroundColor1)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.BackgroundColor2)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.Banner)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.CanHaveSettlement)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.ClothAlternativeColor)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.ClothAlternativeColor2)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.Color)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.Color2)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.EncounterBackgroundMesh)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.ForegroundColor1)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.ForegroundColor2)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.IsBandit)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.IsMainCulture)));
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.Name)));

            // Targetmethods
        }
    }
}
