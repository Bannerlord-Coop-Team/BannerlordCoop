using Common;
using Common.Messaging;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.UI.Interfaces;

namespace Coop.Core.Server.Services.Connection.Handlers;

internal class TacticalUnitSymbolsConfigSnapshotHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly ITacticalUnitSymbolsConfigInterface tacticalUnitSymbolsConfigInterface;

    public TacticalUnitSymbolsConfigSnapshotHandler(
        IMessageBroker messageBroker,
        ITacticalUnitSymbolsConfigInterface tacticalUnitSymbolsConfigInterface)
    {
        this.messageBroker = messageBroker;
        this.tacticalUnitSymbolsConfigInterface = tacticalUnitSymbolsConfigInterface;

        messageBroker.Subscribe<PlayerCampaignEntered>(Handle_PlayerCampaignEntered);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerCampaignEntered>(Handle_PlayerCampaignEntered);
    }

    private void Handle_PlayerCampaignEntered(MessagePayload<PlayerCampaignEntered> payload)
    {
        if (ModInformation.IsClient) return;

        tacticalUnitSymbolsConfigInterface.SendSnapshot(payload.What.playerId);
    }
}
