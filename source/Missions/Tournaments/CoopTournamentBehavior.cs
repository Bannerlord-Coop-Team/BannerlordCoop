using GameInterface.Services.Tournaments.Data;
using SandBox.Tournaments;
using SandBox.Tournaments.MissionLogics;
using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Library;

namespace Missions.Tournaments;

/// <summary>
/// Native tournament presentation backed by the campaign server's canonical bracket. The native
/// <see cref='TournamentBehavior.AfterStart'/> implementation creates a randomized tree on every client; this
/// override hydrates that tree once from the snapshot and only retains native betting/presentation behavior.
/// </summary>
public class CoopTournamentBehavior : TournamentBehavior
{
    private readonly ITournamentNativeBracketHydrator bracketHydrator;
    private TournamentSessionSnapshot snapshot;
    private Action requestAuthoritativeLeave;

    public CoopTournamentBehavior(
        TournamentGame tournamentGame,
        Settlement settlement,
        ITournamentGameBehavior gameBehavior,
        bool isPlayerParticipating,
        TournamentSessionSnapshot snapshot,
        ITournamentNativeBracketHydrator bracketHydrator)
        : base(tournamentGame, settlement, gameBehavior, isPlayerParticipating)
    {
        this.snapshot = snapshot;
        this.bracketHydrator = bracketHydrator;
    }

    public TournamentSessionSnapshot Snapshot => snapshot;

    public override void AfterStart()
    {
        bracketHydrator.Apply(this, snapshot);
        CalculateBet();
    }

    public override void OnMissionTick(float dt)
    {
        // The campaign server advances matches and broadcasts the next canonical bracket. Native progression
        // here would independently end rounds, pay bets and award tournament rewards on every client.
    }

    public void SetLeaveRequest(Action requestLeave)
    {
        requestAuthoritativeLeave = requestLeave;
    }

    public void RequestAuthoritativeLeave()
    {
        if (!snapshot.IsCompleted)
            requestAuthoritativeLeave?.Invoke();
    }

    public override InquiryData OnEndMissionRequest(out bool canLeave)
    {
        canLeave = snapshot.IsCompleted;
        return null;
    }

    public void ApplySnapshot(TournamentSessionSnapshot updated)
    {
        if (updated == null || updated.SessionId != snapshot.SessionId) return;
        if (updated.BracketRevision < snapshot.BracketRevision) return;

        bool refreshBracket = TournamentMatchTransitionRules.RequiresBracketRefresh(
            snapshot,
            updated);
        snapshot = updated;
        if (refreshBracket)
        {
            bracketHydrator.Apply(this, updated);
            CalculateBet();
        }
    }
}
