using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Hideouts
{
    internal class HideoutSync : IAutoSync
    {
        public HideoutSync(AutoSyncRegistry autoSyncBuilder)
        {
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Hideout), nameof(Hideout._isSpotted)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Hideout), nameof(Hideout._nextPossibleAttackTime)));
        }
    }
}
