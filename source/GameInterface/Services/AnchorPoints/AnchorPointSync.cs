using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Naval;

namespace GameInterface.Services.AnchorPoints;
internal class AnchorPointSync : IAutoSync
{
    public AnchorPointSync(AutoSyncRegistry AutoSyncRegistry)
    {
        //// Properties
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(AnchorPoint), nameof(AnchorPoint.Owner)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(AnchorPoint), nameof(AnchorPoint.Position)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(AnchorPoint), nameof(AnchorPoint.TargetPosition)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(AnchorPoint), nameof(AnchorPoint.ArrivalTime)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(AnchorPoint), nameof(AnchorPoint.IsDisabled)));

        //// Fields
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(AnchorPoint), nameof(AnchorPoint._lastUsedDisembarkPosition)));
    }
}
