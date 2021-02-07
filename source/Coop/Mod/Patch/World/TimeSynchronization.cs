using System;
using Common;
using CoopFramework;
using HarmonyLib;
using NLog;
using RailgunNet.System.Types;
using Sync;
using Sync.Behaviour;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Patch
{
    public static class TimeSynchronization
    {
        private class TPatch
        {
            // TODO: Replace TimeControl with a CoopManaged
        }
        /// <summary>
        /// Average offset to the hosts campaign time that had to be compensated. Measured in CampaignTime seconds.
        /// </summary>
        public static MovingAverage Delay { get; set; } = new MovingAverage(200);

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        /// <summary>
        /// Called on slave clients to get the campaign time as dictated by the server.
        /// </summary>
        public static Func<CampaignTime> GetAuthoritativeTime;
        
        // Patched method: internal void MapTimeTracker.Tick(float seconds)
        private static readonly MethodPatch<TPatch> Patch =
            new MethodPatch<TPatch>(typeof(CampaignTime).Assembly.GetType("TaleWorlds.CampaignSystem.MapTimeTracker", true))
                .Intercept("Tick");

        [PatchInitializer]
        public static void Init()
        {
            if (!Patch.TryGetMethod("Tick", out MethodAccess access))
            {
                throw new Exception("Patching failed. Was MapTimeTracker.Tick(float seconds) in the game DLLs changed?");
            }
            access.Prefix.SetGlobalHandler((instance, args) =>
            {
                if (!Coop.IsCoopGameSession())
                {
                    return ECallPropagation.CallOriginal; 
                }
                
                if (args.Length == 0 || !(args[0] is float))
                {
                    throw new ArgumentException(
                        "Unexpected function signature, expected MapTimeTracker.Tick(float seconds). Patch needs to be adjusted.");
                }
                
                if (Coop.IsArbiter)
                {
                    // The host is the authority for the campaign time. Go ahead.
                    return ECallPropagation.CallOriginal;
                }
                
                // Take the predicted server side campaign time
                if (GetAuthoritativeTime == null)
                {
                    throw new Exception("Invalid state. Please set GetAuthoritativeTime during initialization.");
                }

                CampaignTime serverTime = GetAuthoritativeTime.Invoke();
                if (serverTime != CampaignTime.Never)
                {
                    // Correct local time
                    float secondsBehindServer = serverTime.RemainingSecondsFromNow;
                    Delay.Push((int) Math.Round(secondsBehindServer));
                    args[0] = Math.Max(secondsBehindServer, 0f);
                }
                access.Call(EOriginator.RemoteAuthority, instance, args);
                return ECallPropagation.Skip;
            });
        }
    }
}
