using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Fiefs;

class FiefSync : IDynamicSync
{
    public FiefSync(DynamicSyncRegistry autoSyncBuilder)
    {
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Fief), nameof(Fief.GarrisonPartyComponent)));

        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Fief), nameof(Fief.FoodStocks)));
    }
}
