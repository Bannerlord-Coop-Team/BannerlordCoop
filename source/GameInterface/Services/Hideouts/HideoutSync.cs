using GameInterface.AutoSync;
using GameInterface.Services.Hideouts.Patches.Disable;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Hideouts
{
    internal class HideoutSync : IAutoSync
    {
        public HideoutSync(AutoSyncRegistry autoSyncBuilder)
        {
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Hideout), nameof(Hideout.IsSpotted)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Hideout), nameof(Hideout._nextPossibleAttackTime)));
        }
    }
}
