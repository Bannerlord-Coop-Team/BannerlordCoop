using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace Coop.Game.Patch
{
    public static class TimeControl
    {
        public static bool IsRemoteControlled = false;
        public static event Func<CampaignTimeControlMode, bool> On_Campaign_TimeControlMode;

        public static void SetForced_Campaign_TimeControlMode(CampaignTimeControlMode eMode)
        {
            Utils.InvokePrivateMethod(
                typeof(Campaign),
                "SetTimeControlMode",
                Campaign.Current,
                new object[] {eMode});
        }

        [HarmonyPatch(typeof(Campaign))]
        [HarmonyPatch(nameof(Campaign.TimeControlMode), MethodType.Setter)]
        [HarmonyPatch(new[] {typeof(CampaignTimeControlMode)})]
        private static class Campaign_TimeControlMode
        {
            private static bool Prefix(CampaignTimeControlMode value)
            {
                On_Campaign_TimeControlMode?.Invoke(value);
                return !IsRemoteControlled;
            }
        }
    }
}
