using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.GuantletMapEventVisuals.Messages;
using HarmonyLib;
using SandBox.GauntletUI.Map;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.GuantletMapEventVisuals.Patches;

[HarmonyPatch(typeof(GauntletMapEventVisual))]
internal class GauntletMapEventVisualPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<GauntletMapEventVisualPatches>();

    [HarmonyPatch(nameof(GauntletMapEventVisual.Initialize))]
    [HarmonyPrefix]
    private static void PrefixInitialize(GauntletMapEventVisual __instance, CampaignVec2 position, bool isVisible)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client called managed {Method}", nameof(GauntletMapEventVisual.Initialize));
            return;
        }

        var message = new GauntletMapEventVisualInitialized(__instance, position, isVisible);
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(GauntletMapEventVisual.OnMapEventEnd))]
    [HarmonyPrefix]
    private static bool PrefixOnMapEventEnd(GauntletMapEventVisual __instance)
    {
        // Skip vanilla teardown when the map event never synced (null MapEvent): nothing was initialized to tear
        // down and vanilla's end path throws on the unresolved event. Only fires on a client; the server's is never null.
        if (__instance.MapEvent != null) return true;

        Logger.Warning("Skipping OnMapEventEnd for GauntletMapEventVisual: its MapEvent did not resolve on this client");
        return false;
    }

    [HarmonyPatch("GetBattleSizeValue")]
    [HarmonyPrefix]
    private static bool PrefixGetBattleSizeValue(GauntletMapEventVisual __instance, ref int __result)
    {
        // On the client a map event's sides - and the parties within them - sync in over several
        // messages, so a replicated visual init can run before the battle-size data is ready. Until
        // both sides exist and every involved party is resolved, report the smallest size so the
        // ambient-sound setup still completes instead of dereferencing un-synced state and aborting
        // the rest of Initialize (the battle icon is set up earlier). Once everything has synced this
        // is a no-op (always so on the server) and vanilla computes the real size.
        if (BattleSizeComputable(__instance.MapEvent)) return true;

        __result = 0;
        return false;
    }

    // The battle-size calc walks both sides and dereferences each involved party's underlying Party.
    // On the client those are populated after the map event is created (sides via assignment, each
    // party's Party via sync), so all must be present before it can run without hitting un-ready state.
    internal static bool BattleSizeComputable(MapEvent mapEvent)
    {
        if (mapEvent?.AttackerSide == null || mapEvent.DefenderSide == null) return false;

        return PartiesResolved(mapEvent.AttackerSide) && PartiesResolved(mapEvent.DefenderSide);
    }

    private static bool PartiesResolved(MapEventSide side)
    {
        foreach (var party in side.Parties)
        {
            if (party?.Party == null) return false;
        }

        return true;
    }
}
