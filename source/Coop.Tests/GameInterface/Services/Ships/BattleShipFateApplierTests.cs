using GameInterface.Services.ObjectManager;
using GameInterface.Services.Ships;
using GameInterface.Services.Ships.Data;
using Moq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.GameInterface.Services.Ships;

public class BattleShipFateApplierTests
{
    private const string Battle = "MapEvent_1";

    // M6 D1: the fates leave the ledger before any write runs, so one ship's throw must not lose the others or the results.
    [Fact]
    public void ApplyAll_AppliesTheOtherFates_WhenOneShipsWriteThrows()
    {
        var mapEvent = (MapEvent)RuntimeHelpers.GetUninitializedObject(typeof(MapEvent));
        var broken = CreateBrokenShip(CreateParty("party_broken"));
        var ship = CreateShip(CreateParty("party_1"), hitPoints: 600f);
        var ledger = new BattleShipFateLedger();
        ledger.Record(Battle, new BattleShipFate("Ship_broken", 100f, 100f, sunk: false), hostEpoch: 1, revision: 1);
        ledger.Record(Battle, new BattleShipFate("Ship_1", 300f, 200f, sunk: false), hostEpoch: 1, revision: 1);
        var objectManager = new Mock<IObjectManager>();
        string battleId = Battle;
        objectManager.Setup(m => m.TryGetId(mapEvent, out battleId)).Returns(true);
        objectManager.Setup(m => m.TryGetObject("Ship_broken", out broken)).Returns(true);
        objectManager.Setup(m => m.TryGetObject("Ship_1", out ship)).Returns(true);

        new BattleShipFateApplier(ledger, objectManager.Object).ApplyAll(mapEvent);

        Assert.Equal(300f, ship.HitPoints);
        Assert.Equal(200f, ship.SailHitPoints);
        Assert.False(ledger.HasFates(Battle));
    }

    private static PartyBase CreateParty(string partyId)
    {
        var party = (PartyBase)RuntimeHelpers.GetUninitializedObject(typeof(PartyBase));
        party._ships = new MBList<Ship>();
        party.MobileParty = (MobileParty)RuntimeHelpers.GetUninitializedObject(typeof(MobileParty));
        party.MobileParty.StringId = partyId;
        return party;
    }

    private static Ship CreateShip(PartyBase owner, float hitPoints)
    {
        var hull = (ShipHull)RuntimeHelpers.GetUninitializedObject(typeof(ShipHull));
        hull.AvailableSlots = new MBReadOnlyDictionary<string, ShipSlot>(new Dictionary<string, ShipSlot>());
        typeof(ShipHull).GetProperty(nameof(ShipHull.MaxHitPoints))!.SetValue(hull, 1000);
        typeof(ShipHull).GetProperty(nameof(ShipHull.MaxSailHitPoints))!.SetValue(hull, 500);

        var ship = new Ship(hull) { Owner = owner };
        ship.HitPoints = hitPoints;
        return ship;
    }

    // An owned ship with no hull or pieces, so reading its maximum hit points throws.
    private static Ship CreateBrokenShip(PartyBase owner)
    {
        var ship = (Ship)RuntimeHelpers.GetUninitializedObject(typeof(Ship));
        ship._owner = owner;
        ship._hitPoints = 500f;
        return ship;
    }
}
