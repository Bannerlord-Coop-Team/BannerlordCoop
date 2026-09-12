using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Fiefs;

class FiefSync : IAutoSync
{
    public FiefSync(AutoSyncRegistry autoSyncBuilder)
    {
        autoSyncBuilder.AddField(AccessTools.Field(typeof(Fief), nameof(Fief.GarrisonPartyComponent)));

        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Fief), nameof(Fief.FoodStocks)));

        autoSyncBuilder.AddTargetMethod(typeof(Fief), AccessTools.Method(typeof(GarrisonPartyComponent), nameof(GarrisonPartyComponent.OnInitialize)));
        autoSyncBuilder.AddTargetMethod(typeof(Fief), AccessTools.Method(typeof(GarrisonPartyComponent), nameof(GarrisonPartyComponent.OnFinalize)));
    }
}
