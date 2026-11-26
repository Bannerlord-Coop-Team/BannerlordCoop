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
        messageBroker.Subscribe<NetworkTournamentDisconnectLoss>(Handle_DisconnectLoss);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkTournamentRequestRejected>(Handle_Rejected);
        messageBroker.Unsubscribe<NetworkTournamentDisconnectLoss>(Handle_DisconnectLoss);
    }

    private void Handle_Rejected(MessagePayload<NetworkTournamentRequestRejected> payload)
        => ShowNotice(payload.Who, payload.What.Reason);

    private void Handle_DisconnectLoss(MessagePayload<NetworkTournamentDisconnectLoss> payload)
        => ShowNotice(payload.Who, "You lost your tournament match while you were away.");

    private void ShowNotice(object sender, string message)
    {
        if (ModInformation.IsServer ||
            !TournamentServerMessageGuard.IsTrusted(sender, relayNetworks) ||
            string.IsNullOrEmpty(message))
            return;

        GameThread.RunSafe(() => InformationManager.DisplayMessage(
            new InformationMessage(message)),
            context: nameof(TournamentRequestRejectedUIHandler));
    }
}
