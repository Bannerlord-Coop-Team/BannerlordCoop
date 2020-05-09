using Coop.Game.Persistence;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace Coop.Game.Patch
{
    public static class TimeControl
    {
        public static IEnvironmentClient s_Environment = null;

        public static void SetForced_Campaign_TimeControlMode(CampaignTimeControlMode eMode)
        {
            if (Campaign.Current != null)
            {
                Utils.SetPrivateField(
                    typeof(Campaign),
                    "_timeControlMode",
                    Campaign.Current,
                    eMode);
            }
        }

        [HarmonyPatch(typeof(Campaign))]
        [HarmonyPatch(nameof(Campaign.TimeControlMode), MethodType.Setter)]
        [HarmonyPatch(new[] {typeof(CampaignTimeControlMode)})]
        private static class Campaign_TimeControlMode
        {
            private static bool Prefix(CampaignTimeControlMode value)
            {
                if (s_Environment?.TimeControlMode != null)
                {
                    s_Environment.TimeControlMode.Request(value);
                    return false;
                }

                return true;
            }
        }
    }
}
