using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Naval;

namespace GameInterface.Services.Ships;
internal class ShipSync : IAutoSync
{
    public ShipSync(AutoSyncRegistry AutoSyncRegistry)
    {
        //// Fields
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Ship), nameof(Ship.ShipHull)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Ship), nameof(Ship._name)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Ship), nameof(Ship._unlockedUpgradePieces)));

        //// Properties
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Ship), nameof(Ship.Figurehead)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Ship), nameof(Ship.IsInvulnerable)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Ship), nameof(Ship.IsTradeable)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Ship), nameof(Ship.IsUsedByQuest)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Ship), nameof(Ship.RandomValue)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Ship), nameof(Ship.CustomSailPatternId)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Ship), nameof(Ship.Owner)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Ship), nameof(Ship.HitPoints)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Ship), nameof(Ship.SailHitPoints)));
    }
}
