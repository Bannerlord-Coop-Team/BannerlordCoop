using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.Messages;
using System;

namespace GameInterface.Services.Tournaments.UI;

internal interface ITournamentUIController
{
    event Action<TournamentSessionSnapshot> StateChanged;
    event Action<string> SessionRemoved;
    event Action<NetworkTournamentBetResult> BetResultReceived;

    string LocalControllerId { get; }

    bool TryGetTownSession(string townId, out TournamentSessionSnapshot snapshot);
    bool TryGetBetQuote(TournamentSessionSnapshot snapshot, out TournamentBetQuote quote);

    string GetPreparationPrizeName(string townId);
    string GetPreparationPlayerNames(string townId);
    bool CanStartPreparation(string townId);
    bool CanLeavePreparation(string townId);
    bool CanSpectate(string townId);

    void RequestJoin(string townId, string sessionId, long expectedRevision);
    void RequestStart(string townId);
    void RequestLeavePreparation(string townId);
    void RequestSpectate(string townId);
    void RequestLeaveActive(TournamentSessionSnapshot snapshot);
    void RequestChoice(TournamentSessionSnapshot snapshot, TournamentPlayerChoice choice);
    void RequestBet(TournamentSessionSnapshot snapshot, int amount);
}
