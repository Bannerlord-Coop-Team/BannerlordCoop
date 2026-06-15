using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.GameMenus.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Services.UI.Notifications.Messages;
using LiteNetLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Party.Handlers;

internal class SellPrisonersHandler : IHandler
{
    private static readonly ILogger logger = LogManager.GetLogger<SellPrisonersHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ITroopRosterInterface troopRosterInterface;

    public SellPrisonersHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ITroopRosterInterface troopRosterInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.troopRosterInterface = troopRosterInterface;

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
        var data = obj.What;
        var peer = obj.Who as NetPeer;

        GameLoopRunner.RunOnMainThread(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<PartyBase>(data.SellingPartyId, out var sellingParty)) return;

                TroopRoster leftPrisonerRoster = new();
                troopRosterInterface.UpdateWithData(leftPrisonerRoster, data.LeftPrisonerRosterData, sellingParty.LeaderHero);

                int initialGold = sellingParty.LeaderHero.Gold;
                SellPrisonersAction.ApplyForSelectedPrisoners(sellingParty, null, leftPrisonerRoster);

                // Give client notification of changed gold
                network.Send(peer, new NotifyGoldChange(sellingParty.LeaderHero.Gold - initialGold));

                // Refresh the menu to show updated menu items
                if (!objectManager.TryGetIdWithLogging(sellingParty.LeaderHero, out var heroId)) return;
                network.Send(peer, new RefreshGameMenu(heroId, "town_backstreet"));
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to apply {Message}", nameof(SellPrisoners));
            }
        });
    }
}