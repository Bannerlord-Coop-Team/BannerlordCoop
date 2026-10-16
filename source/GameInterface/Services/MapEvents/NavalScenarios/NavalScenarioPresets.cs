#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services.MapEvents.NavalScenarios;

/// <summary>Where a scenario moves each player's hull once that player's deployment is over.</summary>
public enum NavalScenarioPlacement
{
    /// <summary>The hull stays where vanilla deployed it.</summary>
    Deployment = 0,

    /// <summary>Side by side with an AI hull, <c>spacing</c> metres between the hulls, then a rope is thrown at it.</summary>
    Boarding = 1,

    /// <summary>Bow-on to an AI hull's side, <c>spacing</c> metres away, then the helm drives at it.</summary>
    Ramming = 2,
}

/// <summary>DEBUG: one naval battle test setup, as plain data.</summary>
public sealed class NavalScenarioPreset
{
    public string Name { get; set; }

    public string Description { get; set; }

    public int MinPlayers { get; set; } = 1;

    public int MaxPlayers { get; set; } = NavalScenarioPresets.MaxPlayers;

    public int ShipsPerPlayer { get; set; } = 1;

    /// <summary>How many 11-troop add_troops templates each player's party is topped up to.</summary>
    public int TroopTemplatesPerPlayer { get; set; } = 3;

    /// <summary>The AI party's ship count; 0 for a preset without an AI enemy.</summary>
    public int EnemyHulls { get; set; } = 3;

    public NavalScenarioPlacement Placement { get; set; }

    /// <summary>Metres between the placed hulls; unused by <see cref="NavalScenarioPlacement.Deployment"/>.</summary>
    public float Spacing { get; set; }

    /// <summary>
    /// One AI ship starts at <see cref="NavalScenarioPresets.SinkingHullHitPointRatio"/> of its hit points, and the battle
    /// host hits it <c>delay</c> seconds after its deployment.
    /// </summary>
    public bool SinkingEnemyHull { get; set; }

    /// <summary>The battle host client disconnects <c>delay</c> seconds after its deployment and reconnects later.</summary>
    public bool DisconnectBattleHost { get; set; }

    /// <summary>The second player joins the map event <c>delay</c> seconds after the first one started it.</summary>
    public bool LateJoin { get; set; }

    /// <summary>The second player's party attacks the first one; no AI enemy.</summary>
    public bool Duel { get; set; }

    /// <summary>The nearest friendly AI lord party joins the players' side with a ship.</summary>
    public bool AlliedNpc { get; set; }

    /// <summary>The second player retreats through Tab and Retreat <c>delay</c> seconds after its deployment.</summary>
    public bool RetreatSecondPlayer { get; set; }
}

/// <summary>DEBUG: the naval scenario presets <c>coop.debug.naval.scenario</c> accepts.</summary>
public static class NavalScenarioPresets
{
    /// <summary>The most players start_naval puts on one side.</summary>
    public const int MaxPlayers = 6;

    public const int MaxShipsPerPlayer = 4;

    public const int MaxEnemyHulls = 8;

    public const float SinkingHullHitPointRatio = 0.05f;

    /// <summary>
    /// The raw hull damage the battle host deals that ship, as a share of its maximum: twice what it has left, so the
    /// owner's damage reduction cannot keep it afloat, while a healthy hull would survive it.
    /// </summary>
    public const float SinkingHitRatio = 2f * SinkingHullHitPointRatio;

    public const float DefaultDelaySeconds = 20f;

    public const float DefaultReconnectSeconds = 60f;

    public const string DefaultHullId = "northern_light_ship";

    public static IReadOnlyList<NavalScenarioPreset> All { get; } = new[]
    {
        new NavalScenarioPreset
        {
            Name = "solo",
            Description = "One player against one AI hull.",
            MaxPlayers = 1,
            EnemyHulls = 1,
        },
        new NavalScenarioPreset
        {
            Name = "vs_pirates",
            Description = "Every player on one side with one ship each, against three AI hulls.",
        },
        new NavalScenarioPreset
        {
            Name = "boarding",
            Description = "vs_pirates; each player's hull is placed beside an AI hull and throws a rope at it.",
            Placement = NavalScenarioPlacement.Boarding,
            Spacing = 10f,
        },
        new NavalScenarioPreset
        {
            Name = "ramming",
            Description = "vs_pirates; each player's hull is placed bow-on to an AI hull and its helm drives at it.",
            Placement = NavalScenarioPlacement.Ramming,
            Spacing = 60f,
        },
        new NavalScenarioPreset
        {
            Name = "sinking",
            Description = "vs_pirates; one AI ship starts at 5% hit points and the battle host hits it delay seconds after deploying.",
            SinkingEnemyHull = true,
        },
        new NavalScenarioPreset
        {
            Name = "migration",
            Description = "vs_pirates; the battle host client disconnects after deploying and reconnects later.",
            MinPlayers = 2,
            DisconnectBattleHost = true,
        },
        new NavalScenarioPreset
        {
            Name = "late_join",
            Description = "The first player starts against the AI fleet; the second joins the map event later.",
            MinPlayers = 2,
            MaxPlayers = 2,
            LateJoin = true,
        },
        new NavalScenarioPreset
        {
            Name = "fleet",
            Description = "vs_pirates with three ships and twice the troops per player.",
            ShipsPerPlayer = 3,
            TroopTemplatesPerPlayer = 6,
        },
        new NavalScenarioPreset
        {
            Name = "duel",
            Description = "Two players on opposite sides, no AI enemy.",
            MinPlayers = 2,
            MaxPlayers = 2,
            EnemyHulls = 0,
            Duel = true,
        },
        new NavalScenarioPreset
        {
            Name = "allied_npc",
            Description = "vs_pirates plus a friendly AI lord party with a ship on the players' side.",
            AlliedNpc = true,
        },
        new NavalScenarioPreset
        {
            Name = "retreat",
            Description = "vs_pirates; the second player retreats through Tab and Retreat delay seconds after deploying. " +
                "Run it with three players, so two stay to watch the hulls leave.",
            MinPlayers = 2,
            RetreatSecondPlayer = true,
        },
    };

    public static bool TryGet(string name, out NavalScenarioPreset preset)
    {
        preset = All.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
        return preset != null;
    }

    public static string Names => string.Join(", ", All.Select(preset => preset.Name));
}
#endif
