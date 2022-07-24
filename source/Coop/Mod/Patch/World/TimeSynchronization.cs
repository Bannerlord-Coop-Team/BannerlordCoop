using System;
using System.Reflection;
using Common;
using CoopFramework;
using HarmonyLib;
using NLog;
using RailgunNet.System.Types;
using Sync;
using Sync.Behaviour;
using Sync.Patch;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Patch
{
    public static class TimeSynchronization
    {
        /// <summary>
        ///     Average offset to the hosts campaign time that had to be compensated. Measured in CampaignTime seconds.
        /// </summary>
        public static MovingAverage Delay { get; set; } = new MovingAverage(200);

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        /// <summary>
        ///     Called on slave clients to get the campaign time as dictated by the server.
        /// </summary>
        public static Func<CampaignTime> GetAuthoritativeTime;

        /// <summary>
        ///     Harmony patch for MapTimeTracker.Tick. As the class is internal, we need to do this at runtime with
        ///     a manual patch.
        /// </summary>
        private class MapTimeTrackerPatch
        {
            public static bool TickPrefix(ref float seconds)
            {
                if (Coop.IsServer)
                {
                    // The host is the authority for the campaign time. Go ahead.
                    return true;
                }
                
                // Take the predicted server side campaign time
                if (GetAuthoritativeTime == null)
                {
                    // TODO maybe remove
                    //Logger.Warn("Invalid state. Please set GetAuthoritativeTime during initialization.");
                    return true;
                }

                CampaignTime serverTime = GetAuthoritativeTime.Invoke();
                if (serverTime != CampaignTime.Never)
                {
                    // Correct local time
                    float secondsBehindServer = serverTime.RemainingSecondsFromNow;
                    Delay.Push((int) Math.Round(secondsBehindServer));
                    seconds = Math.Max(secondsBehindServer, 0f);
                }
                return true;
            }
        }

        [PatchInitializer]
        public static void Init()
        {
            var declaringClass =
                typeof(CampaignTime).Assembly.GetType("TaleWorlds.CampaignSystem.MapTimeTracker", true);
            var original = declaringClass.GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
            var prefix = typeof(MapTimeTrackerPatch).GetMethod(nameof(MapTimeTrackerPatch.TickPrefix), BindingFlags.Static | BindingFlags.Public);

            lock (Patcher.HarmonyLock)
            {
                Patcher.HarmonyInstance.Patch(original, new HarmonyMethod(prefix));
            }
        }
    }
}
