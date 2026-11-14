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
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(AnchorPoint), nameof(AnchorPoint.ArrivalTime)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(AnchorPoint), nameof(AnchorPoint.IsDisabled)));

        //// Fields
        // v1.5 keeps where the fleet is, or is sailing to, in _position and _settlement, which its setters, CallFleet
        // and ResetPosition write directly
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(AnchorPoint), nameof(AnchorPoint._position)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(AnchorPoint), nameof(AnchorPoint._settlement)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(AnchorPoint), nameof(AnchorPoint._lastUsedDisembarkPosition)));
    }
}
