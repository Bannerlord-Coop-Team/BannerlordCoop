using GameInterface.Configuration;
using System;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

internal interface IPlayerPartyPvpRules : IGameAbstraction
{
    // Whether either rule is on, so the server checks open meetings at all.
    bool IsEnabled(ModOptions options);
    // Whether pvpBlockLeave can hide Leave in this meeting, before asking whether the parties are still at war.
    bool CanBlockLeave(PlayerPartyInteractionSession session, ModOptions options);
    // How long the meeting can sit without a new state, 0 when it isn't timed.
    int GetIdleSeconds(PlayerPartyInteractionSession session, ModOptions options);
    PlayerPartyInteractionOutcomeType GetTimeoutOutcome(PlayerPartyInteractionSession session, bool atWar);
}

/// <summary>
/// The host's pvpBlockLeave and pvpResponseTimerSeconds rules for a meeting between two players.
/// </summary>
internal class PlayerPartyPvpRules : IPlayerPartyPvpRules
{
    // How long an attacker can do nothing while pvpBlockLeave hides Leave and pvpResponseTimerSeconds is off.
    internal const int BlockLeaveIdleSeconds = 30;

    public bool IsEnabled(ModOptions options)
        => options.PvpBlockLeave || HasResponseTimer(options);

    // IsHostile is set when the meeting starts, so a war declared mid-meeting never hides a Leave the responder
    // already saw. Trades keep Leave because barter Cancel is the same Leave.
    public bool CanBlockLeave(PlayerPartyInteractionSession session, ModOptions options)
        => options.PvpBlockLeave &&
           session.IsHostile &&
           session.Proposal != PlayerPartyInteractionProposal.Trade;

    public int GetIdleSeconds(PlayerPartyInteractionSession session, ModOptions options)
    {
        if (session.Proposal == PlayerPartyInteractionProposal.Trade) return 0;
        if (HasResponseTimer(options)) return Math.Max(ModOptionsData.MinimumPvpResponseTimerSeconds, options.PvpResponseTimerSeconds);

        // Only pvpBlockLeave is on: an idle attacker is released, otherwise a responder without Leave has nothing to pick.
        if (session.IsResponderTurn() || !session.IsLeaveHidden) return 0;
        return BlockLeaveIdleSeconds;
    }

    // An unanswered hostile demand at war ends like Refuse, so the battle starts. Anything else ends as Left,
    // so the timer never starts a war.
    public PlayerPartyInteractionOutcomeType GetTimeoutOutcome(PlayerPartyInteractionSession session, bool atWar)
    {
        if (!atWar) return PlayerPartyInteractionOutcomeType.Left;
        if (session.Proposal != PlayerPartyInteractionProposal.HostileDemand) return PlayerPartyInteractionOutcomeType.Left;
        if (!session.IsResponderTurn()) return PlayerPartyInteractionOutcomeType.Left;

        return PlayerPartyInteractionOutcomeType.HostileDemandAccepted;
    }

    private static bool HasResponseTimer(ModOptions options)
        => options.PvpResponseTimerSeconds > 0;
}
