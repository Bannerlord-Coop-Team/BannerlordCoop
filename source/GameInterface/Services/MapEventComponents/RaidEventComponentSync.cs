using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventComponents;

internal class RaidEventComponentSync : IAutoSync
{
    public RaidEventComponentSync(AutoSyncRegistry autoSyncBuilder)
    {
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(RaidEventComponent), nameof(RaidEventComponent.RaidDamage)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(RaidEventComponent), "<RaidDamage>k__BackingField"));

        autoSyncBuilder.AddField(AccessTools.Field(typeof(RaidEventComponent), nameof(RaidEventComponent._nextSettlementDamage)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(RaidEventComponent), nameof(RaidEventComponent._lootedItemCount)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(RaidEventComponent), nameof(RaidEventComponent._raidProductionRewards)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(RaidEventComponent), nameof(RaidEventComponent._isMilitiaResistanceFight)));
    }
}
