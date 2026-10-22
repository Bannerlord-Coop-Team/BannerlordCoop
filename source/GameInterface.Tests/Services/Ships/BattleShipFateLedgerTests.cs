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
        Assert.Equal(800f, Newest((1, 5, 800f), (1, 4, 900f)));
        Assert.Equal(700f, Newest((1, 5, 800f), (1, 6, 700f)));
        // A promoted host numbers its reports from one again at a newer epoch.
        Assert.Equal(600f, Newest((1, 6, 700f), (2, 1, 600f), (1, 9, 500f)));
    }

    [Fact]
    public void Record_KeepsASunkShipSunk_AndASinkAlwaysLands()
    {
        var ledger = new BattleShipFateLedger();
        ledger.Record(Battle, Fate("Ship_1", 800f), hostEpoch: 2, revision: 5);

        ledger.Record(Battle, Fate("Ship_1", 0f, sunk: true), hostEpoch: 1, revision: 1);
        ledger.Record(Battle, Fate("Ship_1", 300f), hostEpoch: 3, revision: 9);

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
        ledger.Record(Battle, Fate("Ship_1", 100f), 1, 1);
        Assert.Equal(100f, Assert.Single(ledger.Take(Battle, _ => true)).HitPoints);
    }

    // M6 D7: every report carries the damage mitigated since its sender's last one, so all of it adds up, even a stale report's.
    [Fact]
    public void Record_SumsTheMitigatedDamageOfEveryReport()
    {
        var ledger = new BattleShipFateLedger();
        ledger.Record(Battle, new BattleShipFate("Ship_1", 800f, 200f, false, 30f), hostEpoch: 1, revision: 2);
        ledger.Record(Battle, new BattleShipFate("Ship_1", 900f, 200f, false, 5f), hostEpoch: 1, revision: 1);
        ledger.Record(Battle, new BattleShipFate("Ship_1", 0f, 200f, true, 15f), hostEpoch: 2, revision: 1);

        var fate = Assert.Single(ledger.Take(Battle, _ => true));
        Assert.True(fate.Sunk);
        Assert.Equal(50f, fate.MitigatedDamage);
    }

    [Fact]
    public void Take_FindsNothingForAnUnknownBattle()
    {
        Assert.Empty(new BattleShipFateLedger().Take("MapEvent_missing", _ => true));
    }

    // The HP of the fate a fresh ledger holds after these reports of one ship, in order.
    private static float Newest(params (int HostEpoch, long Revision, float HitPoints)[] reports)
    {
        var ledger = new BattleShipFateLedger();
        foreach (var report in reports)
            ledger.Record(Battle, Fate("Ship_1", report.HitPoints), report.HostEpoch, report.Revision);
        return Assert.Single(ledger.Take(Battle, _ => true)).HitPoints;
    }

    private static BattleShipFate Fate(string campaignShipId, float hitPoints, bool sunk = false) =>
        new BattleShipFate(campaignShipId, hitPoints, 200f, sunk);
}
