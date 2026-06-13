using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Actions.Patches;

/// <summary>
/// Stops a hero teleport from taking down any machine that renders nameplates.
/// <see cref="TeleportHeroAction"/> raises <see cref="CampaignEventDispatcher.OnHeroTeleportationRequested"/>
/// (before it moves the hero), and the dispatcher invokes every campaign-event receiver in a bare
/// loop with no per-receiver try/catch. PartyNameplateVM compares the teleported hero's faction
/// against each nameplate's own party faction; under fast-forward churn — constant teleports while
/// parties and factions spawn, change and despawn — one nameplate can be in a transient state
/// where that comparison dereferences null. That single throw aborts the whole dispatch and unwinds
/// through the teleport action, killing the machine — most visibly the host, which renders
/// nameplates and applies both its own AI teleports and the ones it relays from clients.
///
/// This is vanilla UI code we cannot change, and the receivers only do cosmetic / notification
/// work, so the guard belongs on the dispatch: swallowing a throw lets the dispatch and the
/// teleport itself complete, and the skipped cosmetic update self-heals on the next refresh.
/// </summary>
[HarmonyPatch(typeof(CampaignEventDispatcher))]
internal class TeleportHeroEventRobustnessPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<CampaignEventDispatcher>();

    [HarmonyPatch(nameof(CampaignEventDispatcher.OnHeroTeleportationRequested))]
    [HarmonyFinalizer]
    private static Exception Finalizer_OnHeroTeleportationRequested(Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Verbose(__exception, "Suppressed {Method}; a campaign-event receiver threw on transient party/faction state", $"{nameof(CampaignEventDispatcher)}.OnHeroTeleportationRequested");
        }

        return null;
    }
}
