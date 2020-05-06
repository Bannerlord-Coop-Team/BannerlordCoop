using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace Coop.Game.Patch
{
    public static class TimeControl
    {
        private static bool IsRemoteControlled => Coop.IsClient;
        public static event Action<CampaignTimeControlMode> OnTimeControlChangeAttempt;

        public static void SetForced_Campaign_TimeControlMode(CampaignTimeControlMode eMode)
        {
            if (Campaign.Current != null)
            {
                Utils.InvokePrivateMethod(
                    typeof(Campaign),
                    "SetTimeControlMode",
                    Campaign.Current,
                    new object[] {eMode});
            }
        }

        [HarmonyPatch(typeof(Campaign))]
        [HarmonyPatch(nameof(Campaign.TimeControlMode), MethodType.Setter)]
        [HarmonyPatch(new[] {typeof(CampaignTimeControlMode)})]
        private static class Campaign_TimeControlMode
        {
            private static bool Prefix(CampaignTimeControlMode value)
            {
                if (IsRemoteControlled)
                {
                    OnTimeControlChangeAttempt?.Invoke(value);
                    return false;
                }

                return true;
            }
        }
    }
}
