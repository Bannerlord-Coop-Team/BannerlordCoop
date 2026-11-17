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

/// <summary>How a scenario's map event starts; one entry per scenario.</summary>
public enum NavalScenarioEntry
{
    /// <summary>start_naval's StartBattleAction: the nearest hostile AI fleet attacks the first player.</summary>
    StartBattle = 0,

    /// <summary>The second player's party attacks the first one; no AI enemy.</summary>
    Duel = 1,

    /// <summary>
    /// The AI fleet is placed at the players and ordered to engage the first one; the campaign AI starts the map event, not
    /// start_naval's StartBattleAction.
    /// </summary>
    Encounter = 2,

    /// <summary>An AI lord's siege of a coastal town blockades its port, and the first player attacks the blockade from the sea.</summary>
    Blockade = 3,
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

    /// <summary>
    /// The battle host client, or with <c>who=nonhost</c> the first other player, disconnects <c>delay</c> seconds after its
    /// deployment (a server drop, or with <c>drop=leave</c> a main-menu leave), reconnects <c>reconnect</c> seconds later and
    /// re-enters the running battle through Attack.
    /// </summary>
    public bool DisconnectBattleHost { get; set; }

    /// <summary>The second player joins the map event <c>delay</c> seconds after the first one started it.</summary>
    public bool LateJoin { get; set; }

    /// <summary>How the scenario's map event starts.</summary>
    public NavalScenarioEntry Entry { get; set; }

    /// <summary>The nearest friendly AI lord party joins the players' side with a ship.</summary>
    public bool AlliedNpc { get; set; }

    /// <summary>The second player retreats through Tab and Retreat <c>delay</c> seconds after its deployment.</summary>
    public bool RetreatSecondPlayer { get; set; }

    /// <summary>The default <c>ram=n</c>: how many ships of every player and of the AI fleet carry a bow ram.</summary>
    public int RammedShipsPerParty { get; set; }
}

/// <summary>DEBUG: the naval scenario presets <c>coop.debug.naval.scenario</c> accepts.</summary>
public static class NavalScenarioPresets
{
    /// <summary>The most players start_naval puts on one side.</summary>
    public const int MaxPlayers = 6;

    public const int MaxShipsPerPlayer = 4;

    public const int MaxEnemyHulls = 8;

    /// <summary>The most troops reserve=n puts beyond a party's crew capacity.</summary>
    public const int MaxReserveTroops = 200;

    public const float SinkingHullHitPointRatio = 0.05f;

    /// <summary>
    /// The raw hull damage the battle host deals that ship, as a share of its maximum: twice what it has left, so the
    /// owner's damage reduction cannot keep it afloat, while a healthy hull would survive it.
    /// </summary>
    public const float SinkingHitRatio = 2f * SinkingHullHitPointRatio;

    public const float DefaultDelaySeconds = 20f;

    public const float DefaultReconnectSeconds = 60f;

    public const string DefaultHullId = "northern_light_ship";

    /// <summary>The ship slot a hull's siege weapon goes in; northern_light_ship has none.</summary>
    public const string WeaponSlotTag = "fore";

    /// <summary>The fore slot piece weapons=n equips, which mounts a ShipBallista on the hull.</summary>
    public const string WeaponPieceId = "fore_ballista";

    /// <summary>The Battle Knarr, a medium hull with a fore slot, which replaces a ship weapons=n must arm that has none.</summary>
    public const string WeaponHullId = "nord_mediumballista_ship";

    /// <summary>The ship slot a hull's ram goes in; northern_light_ship has none.</summary>
    public const string RamSlotTag = "bow";

    /// <summary>The bow piece ram=n equips, whose ram_01 prefab carries the MissionShipRam script.</summary>
    public const string RamPieceId = "bow_ram_lvl1";

    /// <summary>A medium hull with a bow slot, which replaces a ship ram=n must fit that has none.</summary>
    public const string RamHullId = "empire_medium_ship";

    /// <summary>
    /// A heavy hull with 40 crew places below deck (120 crew, 80 on deck), which replaces a plain ship with none for reserve=n;
    /// the default hull and the Battle Knarr carry their whole crew on deck, so they hold no reserve.
    /// </summary>
    public const string ReserveHullId = "empire_heavy_ship";

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
            RammedShipsPerParty = 1,
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
            Description = "vs_pirates; the battle host client, or with who=nonhost the first other player, is dropped by the " +
                "server (drop=leave: leaves to the main menu) after deploying, then reconnects and re-enters the battle late.",
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
            Entry = NavalScenarioEntry.Duel,
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
        new NavalScenarioPreset
        {
            Name = "encounter",
            Description = "A hostile AI fleet, a pirate one where there is one, is placed at the players and ordered to engage the " +
                "first one: the campaign AI starts the battle and the attack goes through the encounter's mission start, " +
                "no start_naval. The other players join the defending side.",
            Entry = NavalScenarioEntry.Encounter,
        },
        new NavalScenarioPreset
        {
            Name = "blockade",
            Description = "An AI lord at war with the players besieges a coastal town with hulls ships, which blockades its port; " +
                "the players are placed at the port and the first one attacks the blockade through the server's blockade battle " +
                "path (town=id picks the town).",
            Entry = NavalScenarioEntry.Blockade,
        },
    };

    /// <summary>The preset named <paramref name="name"/>, case-insensitively.</summary>
    public static bool TryGet(string name, out NavalScenarioPreset preset)
    {
        preset = All.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
        return preset != null;
    }

    public static string Names => string.Join(", ", All.Select(preset => preset.Name));
}
#endif
