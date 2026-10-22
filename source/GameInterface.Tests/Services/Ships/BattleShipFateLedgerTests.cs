using GameInterface.Services.Ships;
using GameInterface.Services.Ships.Data;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Services.Ships;

/// <summary>M6 D4: which reported ship fate the server holds until it applies it.</summary>
public class BattleShipFateLedgerTests
{
    private const string Battle = "MapEvent_1";

    [Fact]
    public void Record_KeepsTheNewestFatePerShip_ByHostEpochThenRevision()
    {
        var ledger = new BattleShipFateLedger();

        Assert.True(ledger.Record(Battle, Fate("Ship_1", 800f), hostEpoch: 1, revision: 5));
        Assert.False(ledger.Record(Battle, Fate("Ship_1", 900f), hostEpoch: 1, revision: 4));
        Assert.True(ledger.Record(Battle, Fate("Ship_1", 700f), hostEpoch: 1, revision: 6));
        // A promoted host numbers its reports from one again at a newer epoch.
        Assert.True(ledger.Record(Battle, Fate("Ship_1", 600f), hostEpoch: 2, revision: 1));
        Assert.False(ledger.Record(Battle, Fate("Ship_1", 500f), hostEpoch: 1, revision: 9));

        Assert.Equal(600f, Assert.Single(ledger.Take(Battle, _ => true)).HitPoints);
    }

    [Fact]
    public void Record_KeepsASunkShipSunk_AndASinkAlwaysLands()
    {
        var ledger = new BattleShipFateLedger();
        ledger.Record(Battle, Fate("Ship_1", 800f), hostEpoch: 2, revision: 5);

        Assert.True(ledger.Record(Battle, Fate("Ship_1", 0f, sunk: true), hostEpoch: 1, revision: 1));
        Assert.False(ledger.Record(Battle, Fate("Ship_1", 300f), hostEpoch: 3, revision: 9));

        Assert.True(Assert.Single(ledger.Take(Battle, _ => true)).Sunk);
    }

    // An applied fate leaves the ledger, so applying again finds nothing and a later report starts over.
    [Fact]
    public void Take_RemovesOnlyTheSelectedFates_AndForgetsAnEmptiedBattle()
    {
        var ledger = new BattleShipFateLedger();
        ledger.Record(Battle, Fate("Ship_1", 800f), 1, 1);
        ledger.Record(Battle, Fate("Ship_2", 700f), 1, 1);
        ledger.Record("MapEvent_2", Fate("Ship_3", 600f), 1, 1);

        var taken = ledger.Take(Battle, fate => fate.CampaignShipId == "Ship_1");

        Assert.Equal("Ship_1", Assert.Single(taken).CampaignShipId);
        Assert.Empty(ledger.Take(Battle, fate => fate.CampaignShipId == "Ship_1"));
        Assert.True(ledger.HasFates(Battle));
        Assert.Equal(new[] { "Ship_2" }, ledger.Take(Battle, _ => true).Select(fate => fate.CampaignShipId));
        Assert.False(ledger.HasFates(Battle));
        Assert.True(ledger.HasFates("MapEvent_2"));
        Assert.True(ledger.Record(Battle, Fate("Ship_1", 100f), 1, 1));
    }

    [Fact]
    public void Take_FindsNothingForAnUnknownBattle()
    {
        Assert.Empty(new BattleShipFateLedger().Take("MapEvent_missing", _ => true));
    }

    private static BattleShipFate Fate(string campaignShipId, float hitPoints, bool sunk = false) =>
        new BattleShipFate(campaignShipId, hitPoints, 200f, sunk);
}
