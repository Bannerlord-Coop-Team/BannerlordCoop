using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Tournaments.Messages;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.Tournaments.UI;

internal sealed class TournamentBetResultUIHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IRelayNetwork[] relayNetworks;
    private readonly Dictionary<string, long> lastSequences = new();

    public TournamentBetResultUIHandler(
        IMessageBroker messageBroker,
        IEnumerable<IRelayNetwork> relayNetworks = null)
    {
        this.messageBroker = messageBroker;
        this.relayNetworks = relayNetworks?.ToArray() ?? new IRelayNetwork[0];

        messageBroker.Subscribe<NetworkTournamentBetResult>(Handle_NetworkTournamentBetResult);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkTournamentBetResult>(Handle_NetworkTournamentBetResult);
        lastSequences.Clear();
    }

    private void Handle_NetworkTournamentBetResult(MessagePayload<NetworkTournamentBetResult> payload)
    {
        if (ModInformation.IsServer ||
            !TournamentServerMessageGuard.IsTrusted(payload.Who, relayNetworks))
            return;

        GameThread.RunSafe(() =>
        {
            if (!TryAccept(payload.What)) return;

            InformationManager.DisplayMessage(
                new InformationMessage(GetMessage(payload.What)));
        });
    }

    internal bool TryAccept(NetworkTournamentBetResult result)
    {
        if (string.IsNullOrEmpty(result.SessionId)) return false;
        if (lastSequences.TryGetValue(result.SessionId, out long lastSequence) &&
            result.Sequence <= lastSequence)
        {
            return false;
        }

        lastSequences[result.SessionId] = result.Sequence;
        return true;
    }

    internal static string GetMessage(NetworkTournamentBetResult result)
    {
        if (result.IsSettlement)
        {
            return string.IsNullOrEmpty(result.Reason)
                ? new TextObject("{=coop_tournament_bet_settled}Tournament bet settled.").ToString()
                : result.Reason;
        }

        return result.Accepted
            ? new TextObject("{=coop_tournament_bet_accepted}Bet accepted: {BETTED} denars, expected payout {PAYOUT}.")
                .SetTextVariable("BETTED", result.BettedDenars)
                .SetTextVariable("PAYOUT", result.ExpectedPayout)
                .ToString()
            : new TextObject("{=coop_tournament_bet_rejected}Bet rejected: {REASON}")
                .SetTextVariable("REASON", string.IsNullOrEmpty(result.Reason)
                    ? new TextObject("{=coop_tournament_bet_rejected_unknown}The wager is no longer available.").ToString()
                    : result.Reason)
                .ToString();
    }
}
