using Common;
using Common.Logging;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Alleys.Patches;

/// <summary>
/// Controls which parts of the vanilla <see cref="AlleyCampaignBehavior"/> run in co-op.
/// The host is a dedicated server with no main hero, and the behavior derefs
/// <c>Hero.MainHero</c> throughout (menus, daily ticks, fights), so it stays fully off on the
/// server. On clients only the co-op-safe UI is re-subscribed: <c>OnSessionLaunched</c> (the
/// manage-alley menus and dialogs) and <c>LocationCharactersAreReadyToSpawn</c> (the alley
/// scene NPCs). The AI-churn and authoritative-ownership handlers (daily ticks, new-game
/// seeding, occupied/cleared, hero-killed) are intentionally left off so alley ownership can
/// only change server-authoritatively (see AlleyHandler / AlleyManagementHandler), never from
/// divergent per-client RNG.
/// </summary>
[HarmonyPatch(typeof(AlleyCampaignBehavior))]
internal class AlleyCampaignBehaviorPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<AlleyCampaignBehaviorPatches>();

    [HarmonyPatch(nameof(AlleyCampaignBehavior.RegisterEvents))]
    [HarmonyPrefix]
    private static bool RegisterEventsPrefix(AlleyCampaignBehavior __instance)
    {
        // Behavior is meaningless on the host (no main hero); leave every listener off.
        if (ModInformation.IsServer) return false;

        // OnSessionLaunched wires the manage-alley game menus and dialogs.
        SubscribeListener<CampaignGameStarter>(__instance, nameof(AlleyCampaignBehavior.OnSessionLaunched),
            handler => CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(__instance, handler));

        // LocationCharactersAreReadyToSpawn populates the alley scene with its NPCs when the owner visits.
        SubscribeListener<Dictionary<string, int>>(__instance, "LocationCharactersAreReadyToSpawn",
            handler => CampaignEvents.LocationCharactersAreReadyToSpawnEvent.AddNonSerializedListener(__instance, handler));

        // Skip the rest of RegisterEvents (AI churn + authoritative ownership handlers).
        return false;
    }

    private static void SubscribeListener<T>(AlleyCampaignBehavior behavior, string methodName, Action<Action<T>> subscribe)
    {
        var method = AccessTools.Method(typeof(AlleyCampaignBehavior), methodName);
        if (method == null)
        {
            Logger.Warning("Could not find {Handler} on AlleyCampaignBehavior; alley UI may be incomplete", methodName);
            return;
        }

        var handler = (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), behavior, method);
        subscribe(handler);
    }
}
