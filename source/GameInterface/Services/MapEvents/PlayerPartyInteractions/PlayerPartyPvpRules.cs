using GameInterface.Configuration;
using System;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

internal interface IPlayerPartyPvpRules : IGameAbstraction
{
    // Whether either rule is on, so the server checks open meetings at all.
    bool IsEnabled(ModOptions options);
    // atWar is only asked once the other checks pass, so a server with pvpBlockLeave off never looks the parties up.
    bool IsLeaveBlocked(PlayerPartyInteractionSession session, ModOptions options, Func<bool> atWar);
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
    // Lower pvpResponseTimerSeconds values count as this, so a player has time to read the dialog.
    internal const int MinimumResponseTimerSeconds = 10;

    public bool IsEnabled(ModOptions options)
        => options.PvpBlockLeave || HasResponseTimer(options);

    // IsHostile is set when the meeting starts, so a war declared mid-meeting never hides a Leave the responder
    // already saw. Trades keep Leave because barter Cancel is the same Leave.
    public bool IsLeaveBlocked(PlayerPartyInteractionSession session, ModOptions options, Func<bool> atWar)
        => options.PvpBlockLeave &&
           session.IsHostile &&
           session.Proposal != PlayerPartyInteractionProposal.Trade &&
           atWar();

    public int GetIdleSeconds(PlayerPartyInteractionSession session, ModOptions options)
    {
        if (session.Proposal == PlayerPartyInteractionProposal.Trade) return 0;
        if (HasResponseTimer(options)) return Math.Max(MinimumResponseTimerSeconds, options.PvpResponseTimerSeconds);

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
