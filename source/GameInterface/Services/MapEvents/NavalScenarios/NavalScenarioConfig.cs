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

    /// <summary>With <see cref="DisconnectBattleHost"/>, the first player that is not the battle host disconnects instead.</summary>
    [ProtoMember(19)]
    public bool DisconnectNonHost { get; set; }

    [ProtoMember(20)]
    public bool CampaignEngage { get; set; }

    [ProtoMember(21)]
    public bool Blockade { get; set; }

    /// <summary>The coastal town the blockade preset uses; empty to let the server pick one.</summary>
    [ProtoMember(22)]
    public string BlockadeTownId { get; set; }

    /// <summary>
    /// How many of each player's and the AI fleet's ships carry a fore ballista; 0 leaves the ships as they are. The blockade's
    /// besieger is not armed.
    /// </summary>
    [ProtoMember(23)]
    public int ArmedShipsPerParty { get; set; }

    /// <summary>
    /// With <see cref="DisconnectBattleHost"/>, the player leaves through the main menu (<c>drop=leave</c>) instead of the
    /// server dropping its connection (<c>drop=hard</c>, the default), which takes the real disconnect path.
    /// </summary>
    [ProtoMember(24)]
    public bool GracefulDisconnect { get; set; }

    // This machine's fixed answer to who disconnects; local, never sent.
    private bool disconnectingResolved;
    private string resolvedDisconnecting;

    public string InitiatorControllerId => ControllerIds.Length == 0 ? null : ControllerIds[0];

    /// <summary>The player who joins the running battle, or null.</summary>
    public string LateJoinerControllerId => LateJoin && ControllerIds.Length > 1 ? ControllerIds[1] : null;

    /// <summary>The player who retreats from the running battle, or null.</summary>
    public string RetreatingControllerId => RetreatSecondPlayer && ControllerIds.Length > 1 ? ControllerIds[1] : null;

    /// <summary>
    /// The player who disconnects and reconnects, given the battle's host: the host itself, or with
    /// <see cref="DisconnectNonHost"/> the first other player; null without a disconnect.
    /// </summary>
    public string DisconnectingControllerId(string hostControllerId)
    {
        if (!DisconnectBattleHost || string.IsNullOrEmpty(hostControllerId)) return null;
        if (!DisconnectNonHost) return hostControllerId;

        foreach (var controllerId in ControllerIds)
        {
            if (controllerId != hostControllerId) return controllerId;
        }

        return null;
    }

    /// <summary>
    /// The player who disconnects, fixed the first time a host is known: after a migration the successor would match
    /// <c>who=host</c> too (R41a-2 E). A client that first sees a later host epoch never disconnects, as the drop came before.
    /// </summary>
    public string ResolveDisconnectingControllerId(string hostControllerId, int hostEpoch)
    {
        if (disconnectingResolved) return resolvedDisconnecting;
        if (string.IsNullOrEmpty(hostControllerId)) return null;

        disconnectingResolved = true;
        resolvedDisconnecting = hostEpoch <= 1 ? DisconnectingControllerId(hostControllerId) : null;
        return resolvedDisconnecting;
    }
}

/// <summary>DEBUG, server to clients: a naval scenario was staged; clients run its automatic steps.</summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNavalScenarioStarted : ICommand
{
    [ProtoMember(1)]
    public readonly NavalScenarioConfig Config;

    /// <summary>Re-sent to a player that reconnected while the scenario's battle runs: its disconnect already happened.</summary>
    [ProtoMember(2)]
    public readonly bool Rejoining;

    public NetworkNavalScenarioStarted(NavalScenarioConfig config, bool rejoining = false)
    {
        Config = config;
        Rejoining = rejoining;
    }
}
#endif
