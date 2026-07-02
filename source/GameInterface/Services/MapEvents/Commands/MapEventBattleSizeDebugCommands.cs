using Autofac;
using Common.Logging;
using GameInterface.Services.GuantletMapEventVisuals;
using GameInterface.Services.ObjectManager;
using SandBox.GauntletUI.Map;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.MapEvents.Commands;

// TEMPORARY debug commands for verifying issue #1449 (client ambient battle-size correction). NOT part of
// the PR. Run these on a CLIENT: the ambient sound is client-local, so nothing here changes authoritative
// state. set_battle_size is the load-bearing check - it confirms the engine honors a live battle_size
// change on an already-playing ambient sound, which is the one runtime assumption the fix rests on.
public class MapEventBattleSizeDebugCommands
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventBattleSizeDebugCommands>();

    // coop.debug.mapevent.list_battles
    /// <summary>
    /// Lists active map events with their type, involved headcount, the ambient battle-size that maps to,
    /// and whether the visual's ambient sound is live - so you can pick a target for set_battle_size.
    /// </summary>
    [CommandLineArgumentFunction("list_battles", "coop.debug.mapevent")]
    public static string ListBattles(List<string> args)
    {
        if (Campaign.Current?.MapEventManager == null) return "No campaign / map event manager";
        TryGetObjectManager(out var objectManager);

        var sb = new StringBuilder();
        sb.AppendLine("Active map events (nearest first):");
        var player = MobileParty.MainParty;
        var events = Campaign.Current.MapEventManager.MapEvents.AsEnumerable();
        if (player != null)
            events = events.OrderBy(e => player.GetPosition2D.Distance(e.Position.ToVec2()));
        foreach (var mapEvent in events)
        {
            string id = null;
            objectManager?.TryGetId(mapEvent, out id);
            var menStr = SafeInvolvedMen(mapEvent, out var men);
            var expected = men < 0 ? "<unready>" : MapEventBattleSizeCorrection.ComputeBattleSize(men).ToString();
            var dist = player != null ? player.GetPosition2D.Distance(mapEvent.Position.ToVec2()).ToString("F0") : "?";
            sb.AppendLine($"  id={id ?? "<null>"}  type={DescribeType(mapEvent)}  dist={dist}  involvedMen={menStr}  expectedBattleSize={expected}  sound={DescribeSound(mapEvent)}");
        }

        var result = sb.ToString();
        Logger.Debug("{Result}", result);
        return result;
    }

    // coop.debug.mapevent.set_battle_size <0-3> [mapEventId]
    /// <summary>
    /// Forces the ambient battle_size on a field battle / sally-out's already-playing sound. Run it with 0
    /// (should go quiet) then 3 (should swell) to confirm the engine re-applies the parameter live.
    /// </summary>
    [CommandLineArgumentFunction("set_battle_size", "coop.debug.mapevent")]
    public static string SetBattleSize(List<string> args)
    {
        if (args.Count < 1 || !int.TryParse(args[0], out var size))
            return "Usage: coop.debug.mapevent.set_battle_size <0-3> [mapEventId]";

        if (!TryResolveBattle(args.Count > 1 ? args[1] : null, out var mapEvent, out var reason))
            return reason;

        if (mapEvent.MapEventVisual is not GauntletMapEventVisual visual)
            return "Map event has no GauntletMapEventVisual";

        var sound = visual._mapEventSoundEvent;
        if (sound == null || !sound.IsValid)
            return "Map event visual has no live ambient sound (is it a field battle in progress?)";

        var menStr = SafeInvolvedMen(mapEvent, out var men);
        var expected = men < 0 ? "<unready>" : MapEventBattleSizeCorrection.ComputeBattleSize(men).ToString();

        sound.SetParameter("battle_size", (float)size);

        var pausedNote = sound.IsPaused()
            ? " NOTE: this sound is PAUSED (battle not visible / out of earshot) - move next to a VISIBLE battle on the map or you won't hear the change."
            : "";

        return $"Set battle_size={size} on {DescribeType(mapEvent)} (involvedMen={menStr}, real size would be {expected}). " +
               "It's the campaign-map ambient combat din; listen for it getting louder/denser, and re-run with a different value to A/B it." +
               pausedNote;
    }

    private static bool TryResolveBattle(string id, out MapEvent mapEvent, out string reason)
    {
        mapEvent = null;
        reason = null;

        if (!string.IsNullOrEmpty(id))
        {
            if (!TryGetObjectManager(out var om) || !om.TryGetObject(id, out mapEvent))
            {
                reason = $"Failed to find MapEvent with id: {id}";
                return false;
            }
            return true;
        }

        // No id: prefer the local party's own battle, else the NEAREST field battle / sally-out with a
        // live sound (the one you're most likely standing next to and hearing).
        var player = MobileParty.MainParty;
        mapEvent = player?.MapEvent;
        if (mapEvent != null) return true;

        var candidates = Campaign.Current?.MapEventManager?.MapEvents?
            .Where(e => (e.IsFieldBattle || e.IsSallyOut)
                && e.MapEventVisual is GauntletMapEventVisual v
                && v._mapEventSoundEvent != null && v._mapEventSoundEvent.IsValid);
        if (player != null && candidates != null)
            candidates = candidates.OrderBy(e => player.GetPosition2D.Distance(e.Position.ToVec2()));
        mapEvent = candidates?.FirstOrDefault();

        if (mapEvent == null)
        {
            reason = "No field battle / sally-out with a live ambient sound found (pass a mapEventId; see coop.debug.mapevent.list_battles)";
            return false;
        }
        return true;
    }

    private static string DescribeType(MapEvent e)
    {
        if (e.IsFieldBattle) return "FieldBattle";
        if (e.IsSallyOut) return "SallyOut";
        if (e.IsSiegeAssault) return "SiegeAssault";
        if (e.IsRaid) return "Raid";
        if (e.IsHideoutBattle) return "HideoutBattle";
        return "Other";
    }

    private static string SafeInvolvedMen(MapEvent e, out int count)
    {
        count = -1;
        try
        {
            count = e.GetNumberOfInvolvedMen();
            return count.ToString();
        }
        catch (Exception ex)
        {
            return $"<unready: {ex.GetType().Name}>";
        }
    }

    private static string DescribeSound(MapEvent e)
    {
        if (e.MapEventVisual is not GauntletMapEventVisual v) return "<no GauntletMapEventVisual>";
        var s = v._mapEventSoundEvent;
        if (s == null) return "<no sound yet>";
        if (!s.IsValid) return "invalid";
        return s.IsPaused() ? "PAUSED (not visible/in range - inaudible)" : "playing (audible if near)";
    }

    private static bool TryGetObjectManager(out IObjectManager objectManager)
    {
        objectManager = null;
        if (!ContainerProvider.TryGetContainer(out var container)) return false;
        return container.TryResolve(out objectManager);
    }
}
