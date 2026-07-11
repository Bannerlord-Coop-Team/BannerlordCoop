using GameInterface.Services.Players.Data;
using GameInterface.Services.Tournaments.Data;
using GameInterface.Services.Tournaments.Messages;
using LiteNetLib;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.Tournaments.Handlers;

internal sealed partial class TournamentSessionHandler
{
    private void ProcessBetRequest(NetPeer peer, Player player, NetworkRequestTournamentBet request)
    {
        if (!TryGetBetSession(peer, request, out var snapshot))
            return;

        BetLedgerEntry ledger = GetOrCreateBetLedger(request.SessionId, player.ControllerId);
        ledger.MatchAmounts.TryGetValue(request.MatchId, out var currentBet);
        if (TryReplayBetResponse(peer, request, ledger))
            return;
        if (!IsBettingOpen(request, player, snapshot, out var hero, out var slot))
        {
            RecordAndSendBetResult(
                peer,
                ledger,
                request,
                snapshot.Revision,
                false,
                "Betting is closed",
                currentBet);
            return;
        }
        if (!tournamentGameInterface.TryGetBetQuote(snapshot, hero, slot.SlotId, out var quote))
        {
            RecordAndSendBetResult(
                peer,
                ledger,
                request,
                snapshot.Revision,
                false,
                "Unable to calculate tournament odds",
                currentBet);
            return;
        }
        if (!TournamentBettingMath.IsValidStake(
                request.Amount,
                currentBet,
                quote.MaximumBet,
                hero.Gold))
        {
            RecordAndSendBetResult(
                peer,
                ledger,
                request,
                snapshot.Revision,
                false,
                "Invalid tournament bet",
                currentBet);
            return;
        }

        GiveGoldAction.ApplyBetweenCharacters(hero, null, request.Amount, true);
        currentBet += request.Amount;
        ledger.MatchAmounts[request.MatchId] = currentBet;
        ledger.ExpectedPayout += (int)(request.Amount * quote.Odd);
        ledger.TotalBettedDenars += request.Amount;
        RecordAndSendBetResult(
            peer,
            ledger,
            request,
            snapshot.Revision,
            true,
            null,
            currentBet);
    }

    private bool TryGetBetSession(
        NetPeer peer,
        NetworkRequestTournamentBet request,
        out TournamentSessionSnapshot snapshot)
    {
        if (!string.IsNullOrEmpty(request.SessionId) &&
            request.SessionId.Length <= 256 &&
            !string.IsNullOrEmpty(request.MatchId) &&
            request.MatchId.Length <= 256 &&
            sessionRegistry.TryGet(request.SessionId, out snapshot))
            return true;

        snapshot = null;
        SendBetResult(
            peer,
            request.SessionId,
            0,
            request.Sequence,
            request.MatchId,
            false,
            "Tournament session not found",
            0,
            0,
            0,
            false);
        return false;
    }

    private BetLedgerEntry GetOrCreateBetLedger(string sessionId, string controllerId)
    {
        string ledgerKey = GetBetKey(sessionId, controllerId);
        if (betLedger.TryGetValue(ledgerKey, out var ledger))
            return ledger;

        ledger = new BetLedgerEntry();
        betLedger.Add(ledgerKey, ledger);
        return ledger;
    }

    private bool TryReplayBetResponse(
        NetPeer peer,
        NetworkRequestTournamentBet request,
        BetLedgerEntry ledger)
    {
        if (!ledger.HasResponse || request.Sequence > ledger.LastRequestSequence)
            return false;

        SendBetResult(
            peer,
            request.SessionId,
            ledger.LastResponseRevision,
            ledger.LastResponseSequence,
            ledger.LastMatchId,
            ledger.LastAccepted,
            ledger.LastReason,
            ledger.LastBettedDenars,
            ledger.LastThisRoundBettedDenars,
            ledger.LastExpectedPayout,
            ledger.LastIsSettlement);
        return true;
    }

    private bool IsBettingOpen(
        NetworkRequestTournamentBet request,
        Player player,
        TournamentSessionSnapshot snapshot,
        out TaleWorlds.CampaignSystem.Hero hero,
        out TournamentContestantData slot)
    {
        hero = null;
        slot = null;
        return request.Sequence > 0 &&
            IsConfirmedEntrant(snapshot, player.ControllerId) &&
            snapshot.Revision == request.ExpectedRevision &&
            snapshot.Phase == TournamentSessionPhase.AwaitingChoices &&
            snapshot.CurrentMatchId == request.MatchId &&
            TryResolvePlayer(player, out hero, out _) &&
            TryGetPlayerSlot(snapshot, player.ControllerId, out slot) &&
            IsSlotInCurrentMatch(snapshot, slot.SlotId);
    }
    private void RecordAndSendBetResult(
        NetPeer peer,
        BetLedgerEntry ledger,
        NetworkRequestTournamentBet request,
        long revision,
        bool accepted,
        string reason,
        int bettedDenars)
    {
        ledger.LastRequestSequence = request.Sequence;
        ledger.LastResponseRevision = revision;
        ledger.LastResponseSequence = request.Sequence;
        ledger.HasResponse = true;
        ledger.LastAccepted = accepted;
        ledger.LastReason = reason;
        ledger.LastBettedDenars = ledger.TotalBettedDenars;
        ledger.LastThisRoundBettedDenars = bettedDenars;
        ledger.LastExpectedPayout = ledger.ExpectedPayout;
        ledger.LastMatchId = request.MatchId;
        ledger.LastIsSettlement = false;
        SendBetResult(
            peer,
            request.SessionId,
            revision,
            request.Sequence,
            request.MatchId,
            accepted,
            reason,
            ledger.TotalBettedDenars,
            bettedDenars,
            ledger.ExpectedPayout,
            false);
    }
}
