using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Alleys;

public class AlleySync : IDynamicSync
{
    public AlleySync(DynamicSyncRegistry registry) 
    {
        // Fields
        registry.AddField(AccessTools.Field(typeof(Alley), nameof(Alley._name)));
        registry.AddField(AccessTools.Field(typeof(Alley), nameof(Alley._settlement)));
        registry.AddField(AccessTools.Field(typeof(Alley), nameof(Alley._tag)));
        registry.AddField(AccessTools.Field(typeof(Alley), nameof(Alley._owner)));
    }
}
