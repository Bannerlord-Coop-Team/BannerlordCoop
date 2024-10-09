using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Fiefs;

class FiefSync : IAutoSync
{
    public FiefSync(IAutoSyncBuilder autoSyncBuilder)
    {
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Fief), nameof(Fief.GarrisonPartyComponent)));

        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Fief), nameof(Fief.FoodStocks)));
    }
}
