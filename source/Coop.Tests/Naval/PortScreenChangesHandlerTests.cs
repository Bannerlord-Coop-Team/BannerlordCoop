#if COOP_NAVAL
using Common;
using Common.Messaging;
using Common.Network;
using Common.Tests.Utils;
using Coop.Naval.PortScreen.Data;
using Coop.Naval.PortScreen.Handlers;
using Coop.Naval.PortScreen.Interfaces;
using Coop.Naval.PortScreen.Messages;
using Moq;
using ProtoBuf;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;
using static NavalDLC.ViewModelCollection.Port.PortScreenHandlers.PortScreenHandler;

namespace Coop.Tests.Naval;

/// <summary>
/// A client's confirmed port screen changes are packed into handles, sent, and unpacked into the same objects on the server.
/// </summary>
public class PortScreenChangesHandlerTests : IDisposable
{
    private readonly TestMessageBroker messageBroker = new TestMessageBroker();
    private readonly global::GameInterface.Services.ObjectManager.ObjectManager objectManager = new(Mock.Of<ILogger>());
    private readonly Mock<INetwork> network = new Mock<INetwork>();
    private readonly RecordingPortScreenHandlerInterface portScreenHandlerInterface = new RecordingPortScreenHandlerInterface();
    private readonly PortScreenChangesHandler handler;

    private readonly PartyBase rightOwner;
    private readonly PartyBase leftOwner;
    private readonly Hero mainHero;
    private readonly Clan playerClan;
    private readonly MobileParty mainParty;
    private readonly Ship boughtShip;
    private readonly Ship soldShip;
    private readonly Ship repairedShip;
    private readonly Ship renamedShip;
    private readonly Ship upgradedShip;
    private readonly Ship sentShip;
    private readonly ShipUpgradePiece piece;
    private readonly Figurehead figurehead;

    private NetworkPortScreenChangesRequested? sentMessage;

    public PortScreenChangesHandlerTests()
    {
        handler = new PortScreenChangesHandler(messageBroker, objectManager, network.Object, portScreenHandlerInterface);

        network
            .Setup(net => net.SendAll(It.IsAny<IMessage>()))
            .Callback<IMessage>(message => sentMessage = (NetworkPortScreenChangesRequested)message);

        rightOwner = Register<PartyBase>(1);
        leftOwner = Register<PartyBase>(2);
        mainHero = Register<Hero>(3);
        playerClan = Register<Clan>(4);
        mainParty = Register<MobileParty>(5);
        boughtShip = Register<Ship>(6);
        soldShip = Register<Ship>(7);
        repairedShip = Register<Ship>(8);
        renamedShip = Register<Ship>(9);
        upgradedShip = Register<Ship>(10);
        sentShip = Register<Ship>(11);
        piece = Register<ShipUpgradePiece>(12);
        figurehead = Register<Figurehead>(13);
    }

    public void Dispose() => handler.Dispose();

    [Fact]
    public void ConfirmedChanges_ReachTheServerAsTheSameObjects()
    {
        var request = new PortScreenChangesRequested(
            PortScreenMode.Trade,
            rightOwner,
            leftOwner,
            mainHero,
            playerClan,
            mainParty,
            new MBReadOnlyList<Ship> { boughtShip },
            new MBReadOnlyList<Ship> { soldShip },
            new MBReadOnlyList<Ship> { repairedShip },
            new MBReadOnlyList<ShipRenameInfo> { new ShipRenameInfo(renamedShip, "Sea Wolf") },
            new MBReadOnlyList<ShipUpgradePieceInfo> { new ShipUpgradePieceInfo(upgradedShip, "sail", piece) },
            new MBReadOnlyList<ShipFigureheadInfo> { new ShipFigureheadInfo(upgradedShip, figurehead) },
            new MBReadOnlyList<Ship> { sentShip });

        SendAndReceive(request);

        var (mode, changes) = Assert.Single(portScreenHandlerInterface.Confirmed);
        Assert.Equal(PortScreenMode.Trade, mode);
        Assert.Same(rightOwner, changes.RightOwner);
        Assert.Same(leftOwner, changes.LeftOwner);
        Assert.Same(mainHero, changes.MainHero);
        Assert.Same(playerClan, changes.PlayerClan);
        Assert.Same(mainParty, changes.MainParty);
        Assert.Same(boughtShip, Assert.Single(changes.ShipsToBuy));
        Assert.Same(soldShip, Assert.Single(changes.ShipsToSell));
        Assert.Same(repairedShip, Assert.Single(changes.ShipsToRepair));
        Assert.Same(sentShip, Assert.Single(changes.ShipsToSend));

        var rename = Assert.Single(changes.ShipsToRename);
        Assert.Same(renamedShip, rename.Ship);
        Assert.Equal("Sea Wolf", rename.NewName);

        var upgrade = Assert.Single(changes.SelectedShipPieces);
        Assert.Same(upgradedShip, upgrade.Ship);
        Assert.Equal("sail", upgrade.ShipSlotTag);
        Assert.Same(piece, upgrade.Piece);

        var figureheadChange = Assert.Single(changes.SelectedFigureheads);
        Assert.Same(upgradedShip, figureheadChange.Ship);
        Assert.Same(figurehead, figureheadChange.Figurehead);
    }

    // Loot has no owner on either side, and a removed piece or figurehead is null; protobuf also drops the empty lists
    [Fact]
    public void LootWithRemovals_ArrivesWithNullSidesAndEmptyLists()
    {
        var request = new PortScreenChangesRequested(
            PortScreenMode.Loot,
            null,
            null,
            mainHero,
            playerClan,
            mainParty,
            new MBReadOnlyList<Ship>(),
            new MBReadOnlyList<Ship>(),
            new MBReadOnlyList<Ship>(),
            new MBReadOnlyList<ShipRenameInfo>(),
            new MBReadOnlyList<ShipUpgradePieceInfo> { new ShipUpgradePieceInfo(upgradedShip, "sail", null) },
            new MBReadOnlyList<ShipFigureheadInfo> { new ShipFigureheadInfo(upgradedShip, null) },
            new MBReadOnlyList<Ship>());

        SendAndReceive(request);

        var (mode, changes) = Assert.Single(portScreenHandlerInterface.Confirmed);
        Assert.Equal(PortScreenMode.Loot, mode);
        Assert.Null(changes.RightOwner);
        Assert.Null(changes.LeftOwner);
        Assert.Empty(changes.ShipsToBuy);
        Assert.Empty(changes.ShipsToSell);
        Assert.Empty(changes.ShipsToRepair);
        Assert.Empty(changes.ShipsToRename);
        Assert.Empty(changes.ShipsToSend);
        Assert.Null(Assert.Single(changes.SelectedShipPieces).Piece);
        Assert.Null(Assert.Single(changes.SelectedFigureheads).Figurehead);
    }

    // A ship the client can't address would turn into a partial confirm, so nothing is sent
    [Fact]
    public void UnregisteredShip_SendsNothing()
    {
        var unregisteredShip = (Ship)RuntimeHelpers.GetUninitializedObject(typeof(Ship));

        messageBroker.Publish(this, CreateRequest(new MBReadOnlyList<Ship> { boughtShip, unregisteredShip }));

        network.Verify(net => net.SendAll(It.IsAny<IMessage>()), Times.Never);
    }

    // A ship sold or destroyed on the server before the confirm arrives would apply only part of the changes
    [Fact]
    public void ShipGoneOnTheServer_ConfirmsNothing()
    {
        messageBroker.Publish(this, CreateRequest(new MBReadOnlyList<Ship> { boughtShip }));
        Assert.True(objectManager.Remove(boughtShip));

        messageBroker.Publish(this, RoundTrip(sentMessage.Value));
        DrainGameThread();

        Assert.Empty(portScreenHandlerInterface.Confirmed);
    }

    private PortScreenChangesRequested CreateRequest(MBReadOnlyList<Ship> shipsToBuy) => new PortScreenChangesRequested(
        PortScreenMode.Trade,
        rightOwner,
        leftOwner,
        mainHero,
        playerClan,
        mainParty,
        shipsToBuy,
        new MBReadOnlyList<Ship>(),
        new MBReadOnlyList<Ship>(),
        new MBReadOnlyList<ShipRenameInfo>(),
        new MBReadOnlyList<ShipUpgradePieceInfo>(),
        new MBReadOnlyList<ShipFigureheadInfo>(),
        new MBReadOnlyList<Ship>());

    private void SendAndReceive(PortScreenChangesRequested request)
    {
        messageBroker.Publish(this, request);
        Assert.NotNull(sentMessage);

        messageBroker.Publish(this, RoundTrip(sentMessage.Value));
        DrainGameThread();
    }

    private T Register<T>(uint handle) where T : class
    {
        var obj = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        Assert.True(objectManager.AddExisting($"{typeof(T).Name}_{handle}", obj, handle));
        return obj;
    }

    private static T RoundTrip<T>(T message)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, message);
        stream.Position = 0;
        return Serializer.Deserialize<T>(stream);
    }

    private static void DrainGameThread() => GameThread.Run(() => { }, blocking: true);

    private sealed class RecordingPortScreenHandlerInterface : IPortScreenHandlerInterface
    {
        public readonly List<(PortScreenMode Mode, PortScreenChanges Changes)> Confirmed = new();

        public void OnConfirmChanges(PortScreenMode mode, PortScreenChanges changes) => Confirmed.Add((mode, changes));
    }
}
#endif
