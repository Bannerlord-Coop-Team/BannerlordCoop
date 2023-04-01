using Common.Logging;
using GameInterface.Services.Heroes.Interfaces;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Time.Patches
{
    [HarmonyPatch(typeof(Campaign))]
    internal class TimePatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<TimePatches>();

        //[HarmonyPatch("TimeControlMode")]
        //[HarmonyPatch(MethodType.Setter)]
        //static bool Prefix()
        //{
        //    bool isAllowed = !TimeControlInterface.TimeLock;

        //    Logger.Verbose("Attempting to change time mode. Allowed: {allowed}", isAllowed && !Campaign.Current.TimeControlModeLock);
            
        //    return isAllowed;
        //}
    }
}
