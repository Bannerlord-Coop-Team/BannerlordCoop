using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Ships;

public class ShipUpgradeSyncTests : ShipTestBase
{
    private const string PieceId = "ShipUpgradePiece_coop_test_piece";
    private const string FigureheadId = "Figurehead_coop_test_figurehead";

    public ShipUpgradeSyncTests(ITestOutputHelper output) : base(output)
    {
        foreach (var instance in AllInstances)
        {
            var piece = instance.CreateRegisteredObject<ShipUpgradePiece>(PieceId);
            piece.MaxHitPointsBonusMultiplier = 0.5f;
            instance.CreateRegisteredObject<Figurehead>(FigureheadId);
        }
    }

    [Fact]
    public void ServerEquipUpgradePiece_SetsTheSlotAndRescaledHitPointsOnClients()
    {
        var shipId = CreateServerShip();

        Server.Call(() =>
        {
            var ship = Server.GetRegisteredObject<Ship>(shipId);
            ship.HitPoints = HullMaxHitPoints / 2f;
            ship.EquipUpgradePiece(SlotTag, Server.GetRegisteredObject<ShipUpgradePiece>(PieceId));
        });
        TestEnvironment.FlushCoalescer();

        foreach (var client in Clients)
        {
            var ship = client.GetRegisteredObject<Ship>(shipId);
            Assert.Same(client.GetRegisteredObject<ShipUpgradePiece>(PieceId), ship.GetPieceAtSlot(SlotTag));
            Assert.Equal(1500f, ship.MaxHitPoints);
            Assert.Equal(750f, ship.HitPoints);
        }

        Server.Call(() => Server.GetRegisteredObject<Ship>(shipId).EquipUpgradePiece(SlotTag, null));

        foreach (var client in Clients)
        {
            Assert.Null(client.GetRegisteredObject<Ship>(shipId).GetPieceAtSlot(SlotTag));
        }
    }

    [Fact]
    public void ServerChangeFigurehead_SyncsToClients()
    {
        var shipId = CreateServerShip();

        Server.Call(() => Server.GetRegisteredObject<Ship>(shipId).ChangeFigurehead(Server.GetRegisteredObject<Figurehead>(FigureheadId)));

        foreach (var client in Clients)
        {
            Assert.Same(client.GetRegisteredObject<Figurehead>(FigureheadId), client.GetRegisteredObject<Ship>(shipId).Figurehead);
        }

        Server.Call(() => Server.GetRegisteredObject<Ship>(shipId).ChangeFigurehead(null));

        foreach (var client in Clients)
        {
            Assert.Null(client.GetRegisteredObject<Ship>(shipId).Figurehead);
        }
    }

    [Fact]
    public void PlayerOwnedShip_UnlockedPiecesFollowTheServer()
    {
        var playerPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var aiPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        RegisterPlayerOnServer("coop_test_player", playerPartyId);
        var shipId = CreateServerShip();

        Server.Call(() => ChangeShipOwnerAction.ApplyByProduction(
            Server.GetRegisteredObject<MobileParty>(playerPartyId).Party,
            Server.GetRegisteredObject<Ship>(shipId)));

        foreach (var client in Clients)
        {
            var unlocked = client.GetRegisteredObject<Ship>(shipId).UnlockedUpgradePieces;
            Assert.NotNull(unlocked);
            Assert.Empty(unlocked);
        }

        Server.Call(() => Server.GetRegisteredObject<Ship>(shipId)
            .EquipUpgradePiece(SlotTag, Server.GetRegisteredObject<ShipUpgradePiece>(PieceId)));

        foreach (var client in Clients)
        {
            Assert.Equal(new[] { client.GetRegisteredObject<ShipUpgradePiece>(PieceId) },
                client.GetRegisteredObject<Ship>(shipId).UnlockedUpgradePieces);
        }

        Server.Call(() => ChangeShipOwnerAction.ApplyByTransferring(
            Server.GetRegisteredObject<MobileParty>(aiPartyId).Party,
            Server.GetRegisteredObject<Ship>(shipId)));

        Assert.Null(Server.GetRegisteredObject<Ship>(shipId).UnlockedUpgradePieces);
        foreach (var client in Clients)
        {
            Assert.Null(client.GetRegisteredObject<Ship>(shipId).UnlockedUpgradePieces);
        }
    }

    [Fact]
    public void AiOwnedShip_HasNoUnlockedPieces()
    {
        var aiPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var shipId = CreateServerShip();

        Server.Call(() =>
        {
            var ship = Server.GetRegisteredObject<Ship>(shipId);
            ChangeShipOwnerAction.ApplyByProduction(Server.GetRegisteredObject<MobileParty>(aiPartyId).Party, ship);
            ship.EquipUpgradePiece(SlotTag, Server.GetRegisteredObject<ShipUpgradePiece>(PieceId));

            Assert.Null(ship.UnlockedUpgradePieces);
        });

        foreach (var client in Clients)
        {
            Assert.Null(client.GetRegisteredObject<Ship>(shipId).UnlockedUpgradePieces);
        }
    }

    private void RegisterPlayerOnServer(string controllerId, string partyId)
    {
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            var hero = party.LeaderHero;
            Assert.True(Server.ObjectManager.TryGetId(hero, out var heroId));
            Assert.True(Server.ObjectManager.TryGetId(hero.Clan, out var clanId));
            Assert.True(Server.ObjectManager.TryGetId(hero.CharacterObject, out var characterId));
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player(controllerId, heroId, partyId, clanId, characterId)));
        });
    }
}
