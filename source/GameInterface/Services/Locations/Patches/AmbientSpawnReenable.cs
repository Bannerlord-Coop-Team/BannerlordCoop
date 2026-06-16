using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Locations.Patches;

/// <summary>
/// Re-subscribes only the scene-spawn listener of an ambient campaign behavior. Some of the disabled
/// behaviors wire other listeners in <c>RegisterEvents</c> (session-launched dialog setup, owner-change
/// handling) that are not co-op safe, so their disable patch re-routes through here to bring back the
/// ambient crowd without reviving the rest of the behavior. Behaviors whose <c>RegisterEvents</c> only
/// wires the spawn listener have their disable patch removed outright instead.
/// </summary>
internal static class AmbientSpawnReenable
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(AmbientSpawnReenable));

    private const string SpawnHandlerName = "LocationCharactersAreReadyToSpawn";

    public static void SubscribeSpawnListenerOnly(CampaignBehaviorBase behavior)
    {
        var method = AccessTools.Method(behavior.GetType(), SpawnHandlerName);
        if (method == null)
        {
            // A game update renamed/removed the handler; warn rather than silently dropping the crowd.
            Logger.Warning("Could not find {Handler} on {Behavior}; its ambient crowd will not spawn",
                SpawnHandlerName, behavior.GetType().Name);
            return;
        }

        var handler = (Action<Dictionary<string, int>>)Delegate.CreateDelegate(
            typeof(Action<Dictionary<string, int>>), behavior, method);

        // Run the handler inside an ambient scope so the crowd it spawns can be recognised (and made static /
        // non-interactable) without affecting other NPCs spawned by un-wrapped behaviors in the same pass.
        CampaignEvents.LocationCharactersAreReadyToSpawnEvent.AddNonSerializedListener(behavior,
            unusedUsablePointCount =>
            {
                using (new AmbientCrowd.SpawnScope())
                {
                    handler(unusedUsablePointCount);
                }
            });
    }
}
