#if COOP_NAVAL
using Common;
using Common.Messaging;
using Common.Network;
using Common.Tests.Utils;
using Coop.Naval.CampaignBehaviors.Handlers;
using Coop.Naval.CampaignBehaviors.Messages;
using Coop.Naval.PortScreen;
using Coop.Naval.PortScreen.Data;
using Coop.Naval.PortScreen.Interfaces;
using GameInterface.Services.Players.Interfaces;
using HarmonyLib;
using LiteNetLib;
using Moq;
using Serilog;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;
using static Coop.Tests.Naval.PortTestObjects;
using static NavalDLC.ViewModelCollection.Port.PortScreenHandlers.PortScreenHandler;

namespace Coop.Tests.Naval;

/// <summary>
/// A port confirm made on a stale client balance, e.g. a double click, is refused on the server before anything changes hands.
/// </summary>
public class PortAffordabilityTests
{
    private readonly FakeShipCostInterface shipCosts = new FakeShipCostInterface();
    private readonly Mock<IPlayerMessageInterface> playerMessages = new Mock<IPlayerMessageInterface>();
    private readonly PartyBase player = CreateParty();
    private readonly PartyBase port = CreateParty();

    // The client's total missed the upgrade on a ship bought in the same confirm, which the server would charge after the purchase
    [Fact]
    public void Trade_CostingMoreThanTheServerBalance_IsRefusedWithNothingBought()
    {
        var hero = CreateHero(gold: 500);
        var bought = CreateShip(port);
        var piece = CreatePiece();
        shipCosts.TradeValues[(bought, player)] = 400;
        shipCosts.UpgradeCosts[(bought, piece)] = 300;
        var portScreen = new PortScreenHandlerInterface(playerMessages.Object, new PortTransactionPricer(shipCosts));

        portScreen.OnConfirmChanges(PortScreenMode.Trade, TradeChanges(hero, bought, new ShipUpgradePieceInfo(bought, "sail", piece)));

        Assert.Same(port, bought.Owner);
        Assert.Single(port.Ships);
        Assert.Empty(player.Ships);
        playerMessages.Verify(messages => messages.SendInformationMessageToHero(hero, It.Is<string>(text => text.Contains("not have enough gold"))), Times.Once);
    }

    // The client still gets its reply so its menu refreshes to the real balance
    [Fact]
    public void Repair_CostingMoreThanTheServerBalance_IsRefusedAndReplied()
    {
        var messageBroker = new TestMessageBroker();
        var objectManager = new global::GameInterface.Services.ObjectManager.ObjectManager(Mock.Of<ILogger>());
        var network = new Mock<INetwork>();
        var pricer = new FakePortTransactionPricer { RepairCost = 150 };
        var hero = CreateHero(gold: 100);
        var damaged = CreateShip(player, hitPoints: 10f);
        var mainParty = CreateMobileParty(player, hero);
        var settlement = (Settlement)RuntimeHelpers.GetUninitializedObject(typeof(Settlement));
        Assert.True(objectManager.AddExisting("MobileParty_1", mainParty, 1));
        Assert.True(objectManager.AddExisting("Settlement_2", settlement, 2));
        using var handler = new RepairShipsAtPortHandler(messageBroker, objectManager, network.Object, pricer, playerMessages.Object);

        messageBroker.Publish(this, new NetworkRequestRepairShipsAtPort(1, 2));
        GameThread.Run(() => { }, blocking: true);

        Assert.Equal(10f, damaged.HitPoints);
        playerMessages.Verify(messages => messages.SendInformationMessageToHero(hero, It.Is<string>(text => text.Contains("not have enough gold"))), Times.Once);
        network.Verify(net => net.Send(It.IsAny<NetPeer>(), It.Is<IMessage>(message => message is NetworkRepairShipsAtPortReply)), Times.Once);
    }

    private PortScreenChanges TradeChanges(Hero hero, Ship bought, ShipUpgradePieceInfo upgrade) => new PortScreenChanges(
        player,
        port,
        hero,
        null,
        null,
        new MBReadOnlyList<Ship> { bought },
        new MBReadOnlyList<Ship>(),
        new MBReadOnlyList<Ship>(),
        new MBReadOnlyList<ShipRenameInfo>(),
        new MBReadOnlyList<ShipUpgradePieceInfo> { upgrade },
        new MBReadOnlyList<ShipFigureheadInfo>(),
        new MBReadOnlyList<Ship>());

    private static MobileParty CreateMobileParty(PartyBase party, Hero leader)
    {
        var component = new Mock<PartyComponent>();
        component.Setup(c => c.Leader).Returns(leader);
        var mobileParty = (MobileParty)RuntimeHelpers.GetUninitializedObject(typeof(MobileParty));
        AccessTools.Field(typeof(MobileParty), "_partyComponent").SetValue(mobileParty, component.Object);
        Traverse.Create(mobileParty).Property(nameof(MobileParty.Party)).SetValue(party);
        return mobileParty;
    }
}
#endif
