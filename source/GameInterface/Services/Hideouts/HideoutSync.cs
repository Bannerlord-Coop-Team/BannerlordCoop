using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Hideouts
{
    internal class HideoutSync : IDynamicSync
    {
        public HideoutSync(DynamicSyncRegistry autoSyncBuilder)
        {
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Hideout), nameof(Hideout._isSpotted)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Hideout), nameof(Hideout._nextPossibleAttackTime)));
        }
    }
}
