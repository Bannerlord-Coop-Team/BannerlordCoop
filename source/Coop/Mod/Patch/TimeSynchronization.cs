using System;
using HarmonyLib;
using NLog;
using Sync;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Patch
{
    public static class TimeSynchronization
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        /// <summary>
        /// Called on slave clients to get the campaign time as dictated by the server.
        /// </summary>
        public static Func<CampaignTime> GetAuthoritativeTime;
        
        // Patched method: internal void MapTimeTracker.Tick(float seconds)
        private static readonly MethodPatch Patch =
            new MethodPatch(typeof(CampaignTime).Assembly.GetType("TaleWorlds.CampaignSystem.MapTimeTracker", true)).Intercept("Tick");

        [PatchInitializer]
        public static void Init()
        {
            if (!Patch.TryGetMethod("Tick", out MethodAccess access))
            {
                throw new Exception("Patching failed. Was MapTimeTracker.Tick(float seconds) in the game DLLs changed?");
            }
            
            // Only relevant for slave clients. The host is the authority for the campaign time.
            access.Condition = o => Coop.IsClientConnected && !Coop.IsArbiter; 
            access.SetGlobalHandler(CreateTickHandler(access));
        }

        /// <summary>
        /// Creates a handler for the MapTimeTracker.Tick(float seconds) patch.
        /// </summary>
        /// <param name="access">Method access for Tick</param>
        /// <returns>Handler</returns>
        private static Action<object, object> CreateTickHandler(MethodAccess access)
        {
            return (instance, arg) =>
            {
                object[] args = (object[]) arg;
                if (args.Length == 0 || !(args[0] is float))
                {
                    throw new ArgumentException(
                        "Unexpected function signature, expected MapTimeTracker.Tick(float seconds). Patch needs to be adjusted.");
                }
                
                // Take the predicted server side campaign time
                if (GetAuthoritativeTime == null)
                {
                    throw new Exception("Invalid state. Please set GetAuthoritativeTime during initialization.");
                }

                float fOriginalArg = (float) args[0];
                float secondsBehindServer = GetAuthoritativeTime.Invoke().RemainingSecondsFromNow;
                float fDiff = secondsBehindServer - fOriginalArg;
                Logger.Trace("Time correction: {diff}.", fDiff);
                args[0] = Math.Min(secondsBehindServer, 0f);
                access.CallOriginal(instance, args);
            };
        }
    }
}
