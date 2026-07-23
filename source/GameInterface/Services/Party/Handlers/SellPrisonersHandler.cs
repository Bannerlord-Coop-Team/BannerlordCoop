using Common;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using GameInterface.Services.GameMenus.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.TroopRosters.Interfaces;
using LiteNetLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using static GameInterface.Services.ObjectManager.ObjectManager;

namespace GameInterface.Services.Party.Handlers;

internal class SellPrisonersHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ITroopRosterInterface troopRosterInterface;
    private readonly IPrisonerSaleProcessor prisonerSaleProcessor;
    private readonly ISendCoalescer sendCoalescer;

    public SellPrisonersHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ITroopRosterInterface troopRosterInterface,
        IPrisonerSaleProcessor prisonerSaleProcessor,
        ISendCoalescer sendCoalescer = null)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.troopRosterInterface = troopRosterInterface;
        this.prisonerSaleProcessor = prisonerSaleProcessor;
        this.sendCoalescer = sendCoalescer;

        messageBroker.Subscribe<PrisonersSold>(Handle_PrisonersSold);
        messageBroker.Subscribe<SellPrisoners>(Handle_SellPrisoners);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PrisonersSold>(Handle_PrisonersSold);
        messageBroker.Unsubscribe<SellPrisoners>(Handle_SellPrisoners);
    }

    private void Handle_PrisonersSold(MessagePayload<PrisonersSold> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.SellingParty, out var sellingPartyId)) return;

        var packedData = troopRosterInterface.PackTroopRosterData(obj.What.LeftPrisonerRoster);

        var message = new SellPrisoners(sellingPartyId, packedData);
        network.SendAll(message);
    }

    private void Handle_SellPrisoners(MessagePayload<SellPrisoners> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(obj.What.SellingPartyId, out var sellingParty)) return;

            TroopRoster leftPrisonerRoster = new();
            troopRosterInterface.UpdateWithData(leftPrisonerRoster, obj.What.LeftPrisonerRosterData, sellingParty.LeaderHero);
            prisonerSaleProcessor.Sell(sellingParty, leftPrisonerRoster);

            objectManager.TryGetId(sellingParty.PrisonRoster, out var rosterId);
            var compactId = Compact(rosterId, typeof(TroopRoster));

            sendCoalescer?.FlushInstance(compactId, network);

            // Refresh the menu to show updated menu items
            if (!objectManager.TryGetIdWithLogging(sellingParty.LeaderHero, out var heroId)) return;
            network.Send(obj.Who as NetPeer, new RefreshGameMenu(heroId, "town_backstreet"));
        });
    }
}
