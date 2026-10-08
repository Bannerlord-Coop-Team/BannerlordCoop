using E2E.Tests.Util;
using E2E.Tests.Environment.Instance;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using HarmonyLib;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Ships;

public class ShipSyncTests : SyncTestBase
{
    string shipId;
    string shipHullId;
    string shipOwnerId;
    string upgradePieceId;
    string figureheadId;

    public ShipSyncTests(ITestOutputHelper output) : base(output)
    {
        shipId = TestEnvironment.CreateRegisteredObject<Ship>();

        TestEnvironment.CreateRegisteredObject<ShipSlot>();
        shipHullId = TestEnvironment.CreateRegisteredObject<ShipHull>();
        upgradePieceId = TestEnvironment.CreateRegisteredObject<ShipUpgradePiece>();
        figureheadId = TestEnvironment.CreateRegisteredObject<Figurehead>();
        shipOwnerId = TestEnvironment.CreateRegisteredObject<PartyBase>();
    }

    [Fact]
    public void Server_SetPieceAtSlot_SyncsPiecesAndEmptySlots()
    {
        SetShipHullLimits();

        AssertClientPieces(("slot1", null), ("slot2", null));

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(shipId, out Ship ship));
            Assert.True(Server.ObjectManager.TryGetObject(upgradePieceId, out ShipUpgradePiece piece));
            ship.SetPieceAtSlot("slot1", piece);
        });

        AssertClientPieces(("slot1", upgradePieceId), ("slot2", null));

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(shipId, out Ship ship));
            ship.SetPieceAtSlot("slot1", null);
        });

        AssertClientPieces(("slot1", null), ("slot2", null));
    }

    [Fact]
    public void Server_SetPieceAtSlot_InvalidatesClientVersion()
    {
        SetShipHullLimits();
        WarmClientVersions();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(shipId, out Ship ship));
            Assert.True(Server.ObjectManager.TryGetObject(upgradePieceId, out ShipUpgradePiece piece));
            ship.SetPieceAtSlot("slot1", piece);
        });

        AssertClientVersionsDirty();
    }

    [Fact]
    public void Server_ChangeFigurehead_InvalidatesClientVersion()
    {
        WarmClientVersions();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(shipId, out Ship ship));
            Assert.True(Server.ObjectManager.TryGetObject(figureheadId, out Figurehead figurehead));
            ship.ShipHull.CanEquipFigurehead = true;
            ship.ChangeFigurehead(figurehead);
        });

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject(shipId, out Ship ship));
            Assert.True(client.ObjectManager.TryGetObject(figureheadId, out Figurehead figurehead));
            Assert.Same(figurehead, ship.Figurehead);
        }

        AssertClientVersionsDirty();
    }

    private void AssertClientPieces(params (string SlotTag, string? PieceId)[] expected)
    {
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject(shipId, out Ship ship));
            Assert.Equal(expected.Select(slot => slot.SlotTag), ship._shipPieces.Keys);

            foreach (var (slotTag, pieceId) in expected)
            {
                if (pieceId == null)
                {
                    Assert.Null(ship._shipPieces[slotTag]);
                    continue;
                }

                Assert.True(client.ObjectManager.TryGetObject(pieceId, out ShipUpgradePiece piece));
                Assert.Same(piece, ship._shipPieces[slotTag]);
            }
        }
    }

    private void WarmClientVersions()
    {
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject(shipId, out Ship ship));
                _ = ship.VersionNo;
                Assert.False(ship._isVersionDirty);
            });
        }
    }

    private void AssertClientVersionsDirty()
    {
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject(shipId, out Ship ship));
            Assert.True(ship._isVersionDirty);
        }
    }

    [Fact]
    public void Server_Ship_Fields()
    {
        Assert.True(Server.ObjectManager.TryGetObject(shipId, out Ship ship));

        TestEnvironment.AssertReferenceField<Ship, ShipHull>(nameof(Ship.ShipHull), referenceStringId: shipHullId, defaultValue: ship.ShipHull);
        AssertShipNameField();
        TestEnvironment.AssertCollectionReferenceField<Ship, ShipUpgradePiece>(nameof(Ship._unlockedUpgradePieces));
    }

    [Fact]
    public void Server_Ship_Properties()
    {
        SetShipHullLimits();
        SetShipOwnerPartyAsMainParty();

        TestEnvironment.AssertReferenceProperty<Ship, Figurehead>(nameof(Ship.Figurehead));
        TestEnvironment.AssertProperty<Ship, bool>(nameof(Ship.IsInvulnerable), false);
        TestEnvironment.AssertProperty<Ship, bool>(nameof(Ship.IsTradeable), false, defaultValue: true);
        TestEnvironment.AssertProperty<Ship, bool>(nameof(Ship.IsUsedByQuest), false);
        TestEnvironment.AssertProperty<Ship, int>(nameof(Ship.RandomValue), 3);
        TestEnvironment.AssertProperty<Ship, string>(nameof(Ship.CustomSailPatternId), "sailPatternId11");
        TestEnvironment.AssertReferenceProperty<Ship, PartyBase>(nameof(Ship.Owner), referenceStringId: shipOwnerId);
        TestEnvironment.AssertProperty<Ship, float>(nameof(Ship.HitPoints), 93.2f);
        TestEnvironment.AssertProperty<Ship, float>(nameof(Ship.SailHitPoints), 57.7f);
    }

    private void AssertShipNameField()
    {
        var field = AccessTools.Field(typeof(Ship), nameof(Ship._name));
        var intercept = TestEnvironment.GetIntercept(field);
        var expectedName = new TextObject("A ship");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject(shipId, out Ship ship));
            Assert.Equal("Test Ship", ship._name.Value);
            intercept.Invoke(null, new object[] { ship, expectedName });
            Assert.Equal(expectedName.Value, ship._name.Value);
        });

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject(shipId, out Ship ship));
            Assert.Equal(expectedName.Value, ship._name.Value);
        }
    }

    private void SetShipOwnerPartyAsMainParty()
    {
        Server.Call(() => SetShipOwnerPartyAsMainParty(Server));

        foreach (var client in Clients)
        {
            client.Call(() => SetShipOwnerPartyAsMainParty(client));
        }
    }

    private void SetShipHullLimits()
    {
        Server.Call(() => SetShipHullLimits(Server));

        foreach (var client in Clients)
        {
            client.Call(() => SetShipHullLimits(client));
        }
    }

    private void SetShipHullLimits(EnvironmentInstance instance)
    {
        Assert.True(instance.ObjectManager.TryGetObject(shipId, out Ship ship));
        ship.ShipHull.MaxHitPoints = 100;
        ship.ShipHull.MaxSailHitPoints = 100;
    }

    private void SetShipOwnerPartyAsMainParty(EnvironmentInstance instance)
    {
        Assert.True(instance.ObjectManager.TryGetObject(shipOwnerId, out PartyBase owner));
        owner.MobileParty.Party = owner;
        Campaign.Current.MainParty = owner.MobileParty;
        Assert.Same(owner, PartyBase.MainParty);
    }
}
