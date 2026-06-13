using Common.Logging;
using HarmonyLib;
using SandBox.ViewModelCollection.Nameplate;
using Serilog;
using System;

namespace GameInterface.Services.UI.Patches;

/// <summary>
/// The map party nameplate listens for hero teleports and compares the teleported hero's
/// faction against its own party's faction (<c>arg1.MapFaction == Party.MapFaction</c>). When a
/// client applies a teleport received from the server, one of its nameplates can reference a
/// party whose faction chain is not fully synced yet (for example a notable-owned party with no
/// home settlement, or a party whose backing PartyBase is still null), so reading
/// <c>MobileParty.MapFaction</c> throws. The teleport action raises this event before it
/// performs the actual move, so the unhandled exception aborts the whole teleport and crashes
/// the client. Swallow and log it so the event dispatch, and therefore the teleport, completes.
/// </summary>
[HarmonyPatch]
internal class NameplateRobustnessPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<NameplateRobustnessPatches>();

    [HarmonyPatch(typeof(PartyNameplateVM), "OnHeroTeleportationRequested")]
    [HarmonyFinalizer]
    private static Exception Finalizer_OnHeroTeleportationRequested(Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error(__exception, "Failed to run {Method}", $"{nameof(PartyNameplateVM)}.OnHeroTeleportationRequested");
        }

        return null;
    }
}
