using Common;
using Common.Messaging;
using GameInterface.Services.PlayerCaptivityService.Messages;

namespace GameInterface.Services.Ships.Handlers;

/// <summary>
/// [Server] Sweeps the loot ship hold on the campaign tick, which runs while time is paused too; LootShipsHandler releases it.
/// </summary>
internal class LootShipHoldHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly ILootShipHold lootShipHold;

    // Takes the broker it subscribes on and the hold.
    public LootShipHoldHandler(IMessageBroker messageBroker, ILootShipHold lootShipHold)
    {
        this.messageBroker = messageBroker;
        this.lootShipHold = lootShipHold;

        messageBroker.Subscribe<CampaignTick>(Handle_CampaignTick);
    }

    // Drops the handler's subscription.
    public void Dispose()
    {
        messageBroker.Unsubscribe<CampaignTick>(Handle_CampaignTick);
    }

    // Published from the Campaign.Tick postfix, so RunSafe runs the sweep inline on the game thread; an empty hold returns at once.
    private void Handle_CampaignTick(MessagePayload<CampaignTick> payload)
    {
        if (ModInformation.IsClient) return;

        GameThread.RunSafe(lootShipHold.Sweep, context: nameof(Handle_CampaignTick));
    }
}
