#if DEBUG
using Common.Messaging;
using ProtoBuf;
using System;

namespace GameInterface.Services.MapEvents.NavalScenarios;

/// <summary>DEBUG: a resolved naval scenario, with every preset value and knob the clients act on.</summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NavalScenarioConfig
{
    [ProtoMember(1)]
    public string ScenarioId { get; set; }

    [ProtoMember(2)]
    public string Preset { get; set; }

    /// <summary>The players in staging order; the first one is attacked and enters the battle for everyone.</summary>
    [ProtoMember(3)]
    public string[] ControllerIds { get; set; } = Array.Empty<string>();

    [ProtoMember(4)]
    public int ShipsPerPlayer { get; set; }

    [ProtoMember(5)]
    public int TroopTemplatesPerPlayer { get; set; }

    [ProtoMember(6)]
    public int EnemyHulls { get; set; }

    [ProtoMember(7)]
    public string HullId { get; set; }

    [ProtoMember(8)]
    public NavalScenarioPlacement Placement { get; set; }

    [ProtoMember(9)]
    public float Spacing { get; set; }

    [ProtoMember(10)]
    public float DelaySeconds { get; set; }

    [ProtoMember(11)]
    public float ReconnectSeconds { get; set; }

    [ProtoMember(12)]
    public bool SinkingEnemyHull { get; set; }

    [ProtoMember(13)]
    public bool DisconnectBattleHost { get; set; }

    [ProtoMember(14)]
    public bool LateJoin { get; set; }

    [ProtoMember(15)]
    public bool Duel { get; set; }

    [ProtoMember(16)]
    public bool AlliedNpc { get; set; }

    /// <summary>The staged map event; empty until the server started it.</summary>
    [ProtoMember(17)]
    public string MapEventId { get; set; }

    [ProtoMember(18)]
    public bool RetreatSecondPlayer { get; set; }

    public string InitiatorControllerId => ControllerIds.Length == 0 ? null : ControllerIds[0];

    /// <summary>The player who joins the running battle, or null.</summary>
    public string LateJoinerControllerId => LateJoin && ControllerIds.Length > 1 ? ControllerIds[1] : null;

    /// <summary>The player who retreats from the running battle, or null.</summary>
    public string RetreatingControllerId => RetreatSecondPlayer && ControllerIds.Length > 1 ? ControllerIds[1] : null;
}

/// <summary>DEBUG, server to clients: a naval scenario was staged; clients run its automatic steps.</summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNavalScenarioStarted : ICommand
{
    [ProtoMember(1)]
    public readonly NavalScenarioConfig Config;

    public NetworkNavalScenarioStarted(NavalScenarioConfig config)
    {
        Config = config;
    }
}
#endif
