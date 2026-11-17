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
            // A hideout is spotted when its settlement is visible.
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.IsVisible)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Hideout), nameof(Hideout._nextPossibleAttackTime)));
        }
    }
}
