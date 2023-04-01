using Common.Logging;
using GameInterface.Services.Heroes.Interfaces;
using HarmonyLib;
using Serilog;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Time.Patches
{
    [HarmonyPatch(typeof(Campaign))]
    internal class TimePatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<TimePatches>();
        private static readonly FieldInfo _timeControlMode = typeof(Campaign).GetField("_timeControlMode", BindingFlags.NonPublic | BindingFlags.Instance);

        [HarmonyPatch("TimeControlMode")]
        [HarmonyPatch(MethodType.Setter)]
        private static bool Prefix(ref Campaign __instance, ref CampaignTimeControlMode value)
        {
            bool isAllowed = !TimeControlInterface.TimeLock;

            Logger.Verbose("Attempting to change time mode. Allowed: {allowed}", isAllowed);

            if (TimeControlInterface.TimeLock == false && 
                __instance.TimeControlModeLock == false &&
                value != (CampaignTimeControlMode)_timeControlMode.GetValue(__instance))
            {
                _timeControlMode.SetValue(__instance, value);
            }

            return false;
        }
    }
}
