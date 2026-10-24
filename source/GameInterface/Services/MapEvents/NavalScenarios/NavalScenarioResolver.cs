#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GameInterface.Services.MapEvents.NavalScenarios;

/// <summary>DEBUG: turns <c>coop.debug.naval.scenario</c> arguments into a <see cref="NavalScenarioConfig"/>.</summary>
public interface INavalScenarioResolver
{
    /// <summary>
    /// The scenario <paramref name="args"/> ask for (the preset, then <c>key=value</c> knobs and player ids); without ids the
    /// connected players in order.
    /// </summary>
    bool TryResolve(IReadOnlyList<string> args, IReadOnlyList<string> connectedControllerIds,
        out NavalScenarioConfig config, out string error);
}

/// <inheritdoc cref="INavalScenarioResolver"/>
public class NavalScenarioResolver : INavalScenarioResolver
{
    internal const string Usage = "Usage: coop.debug.naval.scenario <preset> [hulls=n] [hull=id] [spacing=m] [ships=n] " +
        "[delay=s] [reconnect=s] [who=host|nonhost] [drop=hard|leave] [town=id] [weapons=n] [ram=n] [reserve=n] [controllerId...]; " +
        "delay counts from this client's deployment end (sink, disconnect, retreat) or, for late_join, from staging";

    public bool TryResolve(IReadOnlyList<string> args, IReadOnlyList<string> connectedControllerIds,
        out NavalScenarioConfig config, out string error)
    {
        config = null;
        if (args == null || args.Count == 0)
        {
            error = Usage;
            return false;
        }

        if (!NavalScenarioPresets.TryGet(args[0], out var preset))
        {
            error = $"Unknown preset {args[0]}; presets: {NavalScenarioPresets.Names}.";
            return false;
        }

        var candidate = FromPreset(preset);
        var controllerIds = new List<string>();
        foreach (var arg in args.Skip(1))
        {
            int separator = arg.IndexOf('=');
            error = separator < 0 ? null : ApplyKnob(candidate, arg.Substring(0, separator), arg.Substring(separator + 1));
            if (error != null) return false;
            if (separator < 0) controllerIds.Add(arg);
        }

        error = ResolvePlayers(preset, controllerIds, connectedControllerIds ?? Array.Empty<string>(), out var players);
        if (error != null) return false;

        candidate.ControllerIds = players;
        config = candidate;
        return true;
    }

    // A fresh scenario with the preset's values and the default knobs.
    private static NavalScenarioConfig FromPreset(NavalScenarioPreset preset) => new NavalScenarioConfig
    {
        ScenarioId = Guid.NewGuid().ToString("N"),
        Preset = preset.Name,
        ShipsPerPlayer = preset.ShipsPerPlayer,
        TroopTemplatesPerPlayer = preset.TroopTemplatesPerPlayer,
        EnemyHulls = preset.EnemyHulls,
        HullId = NavalScenarioPresets.DefaultHullId,
        Placement = preset.Placement,
        Spacing = preset.Spacing,
        DelaySeconds = NavalScenarioPresets.DefaultDelaySeconds,
        ReconnectSeconds = NavalScenarioPresets.DefaultReconnectSeconds,
        SinkingEnemyHull = preset.SinkingEnemyHull,
        DisconnectBattleHost = preset.DisconnectBattleHost,
        LateJoin = preset.LateJoin,
        Entry = preset.Entry,
        AlliedNpc = preset.AlliedNpc,
        RetreatSecondPlayer = preset.RetreatSecondPlayer,
        RammedShipsPerParty = preset.RammedShipsPerParty,
        BlockadeTownId = "",
        MapEventId = "",
    };

    // Null when the knob applied, otherwise why it was refused.
    private static string ApplyKnob(NavalScenarioConfig config, string key, string value)
    {
        switch (key)
        {
            case "hulls":
                if (config.Entry == NavalScenarioEntry.Duel) return "duel has no AI enemy, so it takes no hulls knob.";
                if (!TryParseInt(value, 1, NavalScenarioPresets.MaxEnemyHulls, out int hulls))
                    return $"hulls must be a whole number from 1 to {NavalScenarioPresets.MaxEnemyHulls}.";
                config.EnemyHulls = hulls;
                return null;
            case "hull":
                if (string.IsNullOrWhiteSpace(value)) return "hull must name a ship hull id.";
                config.HullId = value;
                return null;
            case "spacing":
                if (config.Placement == NavalScenarioPlacement.Deployment)
                    return $"{config.Preset} keeps the vanilla deployment, so it takes no spacing knob.";
                if (!TryParseFloat(value, 1f, 200f, out float spacing)) return "spacing must be a number of metres from 1 to 200.";
                config.Spacing = spacing;
                return null;
            case "ships":
                if (!TryParseInt(value, 1, NavalScenarioPresets.MaxShipsPerPlayer, out int ships))
                    return $"ships must be a whole number from 1 to {NavalScenarioPresets.MaxShipsPerPlayer}.";
                config.ShipsPerPlayer = ships;
                return null;
            case "delay":
                if (!TryParseFloat(value, 1f, 600f, out float delay)) return "delay must be a number of seconds from 1 to 600.";
                config.DelaySeconds = delay;
                return null;
            case "reconnect":
                if (!TryParseFloat(value, 5f, 600f, out float reconnect)) return "reconnect must be a number of seconds from 5 to 600.";
                config.ReconnectSeconds = reconnect;
                return null;
            case "town":
                if (config.Entry != NavalScenarioEntry.Blockade) return $"{config.Preset} stages no blockade, so it takes no town knob.";
                if (string.IsNullOrWhiteSpace(value)) return "town must name a coastal town's settlement id.";
                config.BlockadeTownId = value.Trim();
                return null;
            case "weapons":
                if (!TryParseInt(value, 0, NavalScenarioPresets.MaxShipsPerPlayer, out int weapons))
                    return $"weapons must be a whole number from 0 to {NavalScenarioPresets.MaxShipsPerPlayer}.";
                config.ArmedShipsPerParty = weapons;
                return null;
            case "ram":
                if (!TryParseInt(value, 0, NavalScenarioPresets.MaxShipsPerPlayer, out int rams))
                    return $"ram must be a whole number from 0 to {NavalScenarioPresets.MaxShipsPerPlayer}.";
                config.RammedShipsPerParty = rams;
                return null;
            case "reserve":
                if (!TryParseInt(value, 0, NavalScenarioPresets.MaxReserveTroops, out int reserve))
                    return $"reserve must be a whole number from 0 to {NavalScenarioPresets.MaxReserveTroops}.";
                config.ReserveTroops = reserve;
                return null;
            case "who":
                if (!config.DisconnectBattleHost) return $"{config.Preset} disconnects nobody, so it takes no who knob.";
                if (value != "host" && value != "nonhost") return "who must be host or nonhost.";
                config.DisconnectNonHost = value == "nonhost";
                return null;
            case "drop":
                if (!config.DisconnectBattleHost) return $"{config.Preset} disconnects nobody, so it takes no drop knob.";
                if (value != "hard" && value != "leave") return "drop must be hard or leave.";
                config.GracefulDisconnect = value == "leave";
                return null;
            default:
                return $"Unknown knob {key}. {Usage}";
        }
    }

    // The players the scenario runs for, or null with why they can't be used.
    private static string ResolvePlayers(NavalScenarioPreset preset, List<string> requested,
        IReadOnlyList<string> connected, out string[] players)
    {
        players = null;
        if (requested.Count > 0)
        {
            var duplicate = requested.GroupBy(id => id).FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null) return $"Player {duplicate.Key} is listed twice.";

            var missing = requested.FirstOrDefault(id => !connected.Contains(id));
            if (missing != null) return $"Player {missing} is not connected; coop.debug.players.list shows the current ids.";

            if (requested.Count > preset.MaxPlayers)
                return $"{preset.Name} takes at most {preset.MaxPlayers} players, got {requested.Count}.";
        }

        var chosen = requested.Count > 0 ? requested : connected.Take(preset.MaxPlayers).ToList();
        if (chosen.Count < preset.MinPlayers)
            return $"{preset.Name} needs at least {preset.MinPlayers} connected players, has {chosen.Count}.";

        players = chosen.ToArray();
        return null;
    }

    // Parses a whole number within [min, max].
    private static bool TryParseInt(string value, int min, int max, out int result) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) && result >= min && result <= max;

    // Parses a finite number within [min, max].
    private static bool TryParseFloat(string value, float min, float max, out float result) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) && !float.IsNaN(result) &&
        result >= min && result <= max;
}
#endif
