using Common.Messaging;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Alleys.Messages;

// --- Local triggers: published on the server from the gated vanilla-handler patches so the
// authoritative alley daily simulation runs server-side (over CoopSession-tracked alleys, not the host's
// empty _playerOwnedCommonAreaData) instead of the divergent per-client vanilla body. ---

/// <summary>Vanilla <c>DailyTick</c> fired; run the server-authoritative daily alley sim.</summary>
public readonly struct AlleyDailyTickTriggered : IEvent { }

/// <summary>Vanilla <c>DailyTickSettlement</c> fired for a settlement; run the gang-vs-gang alley ownership tick.</summary>
public readonly struct AlleyDailyTickSettlementTriggered : IEvent
{
    public readonly Settlement Settlement;
    public AlleyDailyTickSettlementTriggered(Settlement settlement)
    {
        Settlement = settlement;
    }
}

/// <summary>A hero was killed; apply the alley-side consequences (overseer death, gang-leader alleys freed).</summary>
public readonly struct AlleyHeroKilledTriggered : IEvent
{
    public readonly Hero Victim;
    public AlleyHeroKilledTriggered(Hero victim)
    {
        Victim = victim;
    }
}

/// <summary>
/// Published on the owning client from the alley-fight-result patch when the player wins or loses a
/// defense fight, so the authoritative outcome is sent to the server (the fight is a local mission).
/// </summary>
public readonly struct AlleyDefenseResolvedRequested : IEvent
{
    public readonly Alley Alley;
    public readonly bool Won;
    // The post-fight garrison on a win (defenders may have died in the mission), so the server's roster
    // stays in sync; null on a loss (the alley is destroyed).
    public readonly TroopRoster Garrison;
    public AlleyDefenseResolvedRequested(Alley alley, bool won, TroopRoster garrison)
    {
        Alley = alley;
        Won = won;
        Garrison = garrison;
    }
}

/// <summary>Debug cheat: force an AI attack on a specific player alley now, bypassing the daily RNG roll.</summary>
public readonly struct ForceAlleyAttackRequested : IEvent
{
    public readonly Alley Alley;
    public ForceAlleyAttackRequested(Alley alley)
    {
        Alley = alley;
    }
}

// --- Networked broadcast (server to owning client): a player alley came under AI attack ---

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAlleyUnderAttack : ICommand
{
    [ProtoMember(1)]
    public readonly string AlleyId;
    [ProtoMember(2)]
    public readonly string AttackerAlleyId;
    [ProtoMember(3)]
    public readonly CampaignTime DueDate;
    public NetworkAlleyUnderAttack(string alleyId, string attackerAlleyId, CampaignTime dueDate)
    {
        AlleyId = alleyId;
        AttackerAlleyId = attackerAlleyId;
        DueDate = dueDate;
    }
}

// --- Networked request (owning client to server): the client resolved a defense fight ---

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestAlleyDefenseResolved : ICommand
{
    [ProtoMember(1)]
    public readonly string AlleyId;
    [ProtoMember(2)]
    public readonly bool Won;
    [ProtoMember(3)]
    public readonly TroopRosterElementData[] Garrison;
    public RequestAlleyDefenseResolved(string alleyId, bool won, TroopRosterElementData[] garrison)
    {
        AlleyId = alleyId;
        Won = won;
        Garrison = garrison;
    }
}
