using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.Core;

namespace GameInterface.Services.BasicCultureObjects
{
    internal class BasicCultureObjectSync : IAutoSync
    {
        public BasicCultureObjectSync(IAutoSyncBuilder autoSyncBuilder)
        {
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.BackgroundColor1)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.BackgroundColor2)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.BannerKey)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.CanHaveSettlement)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.ClothAlternativeColor)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.ClothAlternativeColor2)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.Color)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.Color2)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.EncounterBackgroundMesh)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.ForegroundColor1)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.ForegroundColor2)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.IsBandit)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.IsMainCulture)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(BasicCultureObject), nameof(BasicCultureObject.Name)));
        }
    }
}
