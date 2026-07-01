using Common;
using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Guards <see cref="MobileParty.MapFaction"/> against a <see cref="NullReferenceException"/> when a
/// party is reached before its owner / home-settlement chain has finished applying. The vanilla
/// map-event continuity sweep (fired when a replicated war or peace change sets a stance) evaluates
/// MapFaction across every involved party of every map event; an indeterminate party would otherwise
/// crash that sweep. Returning null matches the value vanilla already yields for a party with no
/// resolvable faction, so the sweep simply skips it. Mirrors the transient-sync guard on
/// <see cref="LordPartyComponent.HomeSettlement"/>.
/// </summary>
[HarmonyPatch(typeof(MobileParty), nameof(MobileParty.MapFaction), MethodType.Getter)]
internal class MobilePartyMapFactionPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyMapFactionPatch>();

    [HarmonyFinalizer]
    private static Exception Finalizer(MobileParty __instance, ref IFaction __result, Exception __exception)
    {
        if (__exception == null) return null;

        // A replicated object can be observed mid-sync, with its owner/home-settlement references not
        // yet applied, so the vanilla getter dereferences null. On a client this is a transient sync
        // state, not a real fault: swallow it and report no faction. The server runs with complete
        // state, so let a genuine failure there surface.
        if (ModInformation.IsServer) return __exception;

        Logger.Debug("MobileParty.MapFaction threw during a sync transition (MobileParty: {Party}); returning null. {Message}",
            __instance?.StringId ?? "null",
            __exception.Message);

        __result = null;
        return null;
    }
}
