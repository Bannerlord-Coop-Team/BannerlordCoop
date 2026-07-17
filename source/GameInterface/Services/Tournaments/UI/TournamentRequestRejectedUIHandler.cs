using Common;
using Common.Messaging;
using GameInterface.Services.Tournaments.Messages;
using Common.Network;
using TaleWorlds.Library;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services.Tournaments.UI;

internal sealed class TournamentRequestRejectedUIHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IRelayNetwork[] relayNetworks;

    public TournamentRequestRejectedUIHandler(
        IMessageBroker messageBroker,
        IEnumerable<IRelayNetwork> relayNetworks = null)
    {
        this.messageBroker = messageBroker;
        this.relayNetworks = relayNetworks?.ToArray() ?? Array.Empty<IRelayNetwork>();

        messageBroker.Subscribe<NetworkTournamentRequestRejected>(Handle_Rejected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkTournamentRequestRejected>(Handle_Rejected);
    }

    private void Handle_Rejected(MessagePayload<NetworkTournamentRequestRejected> payload)
    {
        if (ModInformation.IsServer ||
            !TournamentServerMessageGuard.IsTrusted(payload.Who, relayNetworks) ||
            string.IsNullOrEmpty(payload.What.Reason))
            return;

        GameThread.RunSafe(() => InformationManager.DisplayMessage(
            new InformationMessage(payload.What.Reason)),
            context: nameof(TournamentRequestRejectedUIHandler));
    }
}
