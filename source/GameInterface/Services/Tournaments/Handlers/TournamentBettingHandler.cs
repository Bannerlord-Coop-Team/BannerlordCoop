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
        if (string.IsNullOrEmpty(request.SessionId) ||
            request.SessionId.Length > 256 ||
            string.IsNullOrEmpty(request.MatchId) ||
            request.MatchId.Length > 256 ||
            !sessionRegistry.TryGet(request.SessionId, out var snapshot))
        {
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
            return;
        }

        string ledgerKey = GetBetKey(request.SessionId, player.ControllerId);

        if (!betLedger.TryGetValue(ledgerKey, out var ledger))
        {
            ledger = new BetLedgerEntry();
            betLedger.Add(ledgerKey, ledger);
        }

        ledger.MatchAmounts.TryGetValue(request.MatchId, out var currentBet);
        if (ledger.HasResponse && request.Sequence <= ledger.LastRequestSequence)
        {
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
            return;
        }

        if (request.Sequence <= 0 ||
            !IsConfirmedEntrant(snapshot, player.ControllerId) ||
            snapshot.Revision != request.ExpectedRevision ||
            snapshot.Phase != TournamentSessionPhase.AwaitingChoices ||
            snapshot.CurrentMatchId != request.MatchId ||
            !TryResolvePlayer(player, out var hero, out _) ||
            !TryGetPlayerSlot(snapshot, player.ControllerId, out var slot) ||
            !IsSlotInCurrentMatch(snapshot, slot.SlotId))
        {
            RecordAndSendBetResult(
                peer,
                ledger,
                request,
                snapshot?.Revision ?? 0,
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

        int amount = request.Amount;
        if (!TournamentBettingMath.IsValidStake(amount, currentBet, quote.MaximumBet, hero.Gold))
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

        GiveGoldAction.ApplyBetweenCharacters(hero, null, amount, true);
        currentBet += amount;
        ledger.MatchAmounts[request.MatchId] = currentBet;
        ledger.ExpectedPayout += (int)(amount * quote.Odd);
        ledger.TotalBettedDenars += amount;
        RecordAndSendBetResult(
            peer,
            ledger,
            request,
            snapshot.Revision,
            true,
            null,
            currentBet);
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
