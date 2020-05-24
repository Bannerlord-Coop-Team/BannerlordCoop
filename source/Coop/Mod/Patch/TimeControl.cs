using HarmonyLib;
using Sync;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Patch
{
    [Patch]
    public static class TimeControl
    {
        public static SyncField<Campaign, CampaignTimeControlMode> TimeControlMode { get; } =
            new SyncField<Campaign, CampaignTimeControlMode>(
                AccessTools.Field(typeof(Campaign), "_timeControlMode"));

        [SyncWatch(typeof(Campaign), nameof(Campaign.TimeControlMode), MethodType.Setter)]
        private static void Patch_TimeControlMode(Campaign __instance)
        {
            if (Coop.DoSync)
            {
                TimeControlMode.Watch(__instance);
            }
        }
    }
}
