using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.PrisonBreaks.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestPrisonBreakStart : ICommand
{
    [ProtoMember(1)]
    public readonly string PlayerHeroId;

    [ProtoMember(2)]
    public readonly string SettlementId;

    [ProtoMember(3)]
    public readonly string PrisonerId;

    public NetworkRequestPrisonBreakStart(string playerHeroId, string settlementId, string prisonerId)
    {
        PlayerHeroId = playerHeroId;
        SettlementId = settlementId;
        PrisonerId = prisonerId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPrisonBreakStartResolved : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string SettlementId;

    [ProtoMember(2)]
    public readonly string PrisonerId;

    [ProtoMember(3)]
    public readonly PrisonBreakStartResult Result;

    public NetworkPrisonBreakStartResolved(string settlementId, string prisonerId, PrisonBreakStartResult result)
    {
        SettlementId = settlementId;
        PrisonerId = prisonerId;
        Result = result;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestPrisonBreakEnd : ICommand
{
    [ProtoMember(1)]
    public readonly PrisonBreakOutcome Outcome;

    public NetworkRequestPrisonBreakEnd(PrisonBreakOutcome outcome)
    {
        Outcome = outcome;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPrisonBreakCooldownChanged : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string SettlementId;

    [ProtoMember(2)]
    public readonly long CooldownEndTicks;

    public NetworkPrisonBreakCooldownChanged(string settlementId, long cooldownEndTicks)
    {
        SettlementId = settlementId;
        CooldownEndTicks = cooldownEndTicks;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPrisonBreakActiveChanged : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string SettlementId;

    [ProtoMember(2)]
    public readonly bool IsActive;

    public NetworkPrisonBreakActiveChanged(string settlementId, bool isActive)
    {
        SettlementId = settlementId;
        IsActive = isActive;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPrisonBreakPrisonerLost : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string PrisonerId;

    public NetworkPrisonBreakPrisonerLost(string prisonerId)
    {
        PrisonerId = prisonerId;
    }
}
