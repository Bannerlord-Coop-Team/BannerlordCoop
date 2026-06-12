using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Actions.Patches;

/// <summary>
/// Keeps a flood of hero teleports from crashing connected machines during a join.
/// <see cref="TeleportHeroAction"/> raises <see cref="CampaignEventDispatcher.OnHeroTeleportationRequested"/>
/// as the first thing it does, before it moves the hero. The dispatcher invokes every campaign-event
/// receiver in a bare loop with no per-receiver try/catch, so if one receiver dereferences a hero,
/// party, settlement or faction that is only half-built — which happens while the world is being
/// fast-forwarded and teleport messages flood in ahead of sync completing — the throw aborts the
/// whole dispatch and unwinds through the teleport action, taking the machine down. The reported
/// crash is in a nameplate view model, but other receivers (e.g. the notifications behavior) deref
/// the same incomplete state, so the guard belongs on the dispatch, not on a single receiver.
///
/// These receivers only do cosmetic / notification work; swallowing a dispatch that throws is safe:
/// the teleport itself still applies (it runs after the dispatch returns) and any skipped cosmetic
/// update self-heals on the next refresh.
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
            Logger.Verbose(__exception, "Suppressed {Method} thrown while hero/party sync state was incomplete", $"{nameof(CampaignEventDispatcher)}.OnHeroTeleportationRequested");
        }

        return null;
    }
}
