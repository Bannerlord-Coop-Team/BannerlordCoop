using System;
using NLog;
using Sync;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Patch
{
    public static class MapTimeTracker
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        /// <summary>
        /// Called on slave clients to get the campaign time as dictated by the server.
        /// </summary>
        public static Func<CampaignTime> GetAuthoritativeTime;
        
        // Patched method: internal void MapTimeTracker.Tick(float seconds)
        private static readonly MethodPatch Patch =
            new MethodPatch(typeof(MapTimeTracker)).Intercept("Tick");

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
                float SecondsFromAuthoritativeState = GetAuthoritativeTime.Invoke().RemainingSecondsFromNow;
                float fDiff = SecondsFromAuthoritativeState - fOriginalArg;
                Logger.Trace("Time correction: {diff}.", fDiff);
                
                args[0] = SecondsFromAuthoritativeState;
                access.CallOriginal(instance, args);
            };
        }
    }
}
