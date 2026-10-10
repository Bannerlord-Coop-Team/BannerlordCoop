using Common.Messaging;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.TroopSupply;

namespace GameInterface.Services.Hideouts.Handlers;

internal sealed class HideoutTroopSelectionHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IHideoutTroopSelection selection;
    private readonly IBattleTroopReserveBuilder reserveBuilder;

    public HideoutTroopSelectionHandler(IMessageBroker messageBroker, IHideoutTroopSelection selection,
        IBattleTroopReserveBuilder reserveBuilder)
    {
        this.messageBroker = messageBroker;
        this.selection = selection;
        this.reserveBuilder = reserveBuilder;
        messageBroker.Subscribe<MapEventFinalized>(Handle_MapEventFinalized);
    }

    public void Dispose() => messageBroker.Unsubscribe<MapEventFinalized>(Handle_MapEventFinalized);

    private void Handle_MapEventFinalized(MessagePayload<MapEventFinalized> payload)
    {
        if (!payload.What.MapEvent.IsHideoutBattle) return;
        reserveBuilder.ForgetMapEvent(payload.What.MapEvent);
        selection.ForgetMapEvent(payload.What.MapEvent);
    }
}
