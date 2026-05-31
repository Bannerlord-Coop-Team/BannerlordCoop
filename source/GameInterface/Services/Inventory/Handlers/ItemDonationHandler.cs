using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Inventory.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Inventory.Handlers;

internal class ItemDonationHandler : IHandler
{
    private static readonly ILogger logger = LogManager.GetLogger<ItemDonationHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public ItemDonationHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<ItemDonated>(Handle_ItemDonated);
        messageBroker.Subscribe<DonateItem>(Handle_DonateItem);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ItemDonated>(Handle_ItemDonated);
        messageBroker.Unsubscribe<DonateItem>(Handle_DonateItem);
    }

    private void Handle_ItemDonated(MessagePayload<ItemDonated> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.TargetItemRoster, out var targetItemRosterId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.Party, out var partyId)) return;

        var message = new DonateItem(
            targetItemRosterId,
            obj.What.EquipmentElement,
            partyId,
            obj.What.TroopRosterElement,
            obj.What.GainedXp);

        network.SendAll(message);
    }

    private void Handle_DonateItem(MessagePayload<DonateItem> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<ItemRoster>(obj.What.TargetItemRosterId, out var targetItemRoster)) return;
        if (!objectManager.TryGetObjectWithLogging<PartyBase>(obj.What.PartyId, out var party)) return;

        targetItemRoster.AddToCounts(obj.What.EquipmentElement, -1);

        if (obj.What.GainedXp > 0 && obj.What.TroopRosterElement.Character != null)
        {
            // TODO
            party.MemberRoster.AddXpToTroop(obj.What.TroopRosterElement.Character, obj.What.GainedXp);
        }
    }
}
