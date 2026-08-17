using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.StanceLinks;

internal class StanceLinkSync : IAutoSync
{
    public StanceLinkSync(AutoSyncRegistry autoSyncBuilder)
    {
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(StanceLink), nameof(StanceLink.TroopCasualties1)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(StanceLink), nameof(StanceLink.TroopCasualties2)));

        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(StanceLink), nameof(StanceLink.ShipCasualties1)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(StanceLink), nameof(StanceLink.ShipCasualties2)));

        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(StanceLink), nameof(StanceLink.SuccessfulSieges1)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(StanceLink), nameof(StanceLink.SuccessfulSieges2)));

        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(StanceLink), nameof(StanceLink.SuccessfulRaids1)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(StanceLink), nameof(StanceLink.SuccessfulRaids2)));

        autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink._totalTributePaidFrom1To2)));

        autoSyncBuilder.AddField(AccessTools.Field(typeof(StanceLink), nameof(StanceLink._dailyTributeFrom1To2)));

        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(StanceLink), nameof(StanceLink.DailyTributeInstallments)));

        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(StanceLink), nameof(StanceLink.SuccessfulTownSieges1)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(StanceLink), nameof(StanceLink.SuccessfulTownSieges2)));
    }
}