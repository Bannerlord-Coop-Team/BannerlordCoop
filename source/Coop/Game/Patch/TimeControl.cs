using Coop.Sync;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace Coop.Game.Patch
{
    [Patch]
    public static class TimeControl
    {
        public static SyncField TimeControlMode { get; } =
            new SyncField(AccessTools.Field(typeof(Campaign), "_timeControlMode"));

        [SyncWatch(typeof(Campaign), nameof(Campaign.TimeControlMode), MethodType.Setter)]
        private static void Patch_TimeControlMode(Campaign __instance)
        {
            TimeControlMode.Watch(__instance);
        }
    }
}
