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

    // Capture 2.5: the captor numbers the prize's fates from its own count, so its first fate replaces the host's held one
    // whatever their stamps, and later captor fates order among themselves.
    [Fact]
    public void RecordCapture_LetsTheCaptorsFirstFateSupersedeTheHeldOne()
    {
        var ledger = new BattleShipFateLedger();
        ledger.Record(Battle, Fate("Ship_1", 800f), hostEpoch: 2, revision: 9);

        ledger.RecordCapture(Battle, "Ship_1", "captor");
        ledger.Record(Battle, Fate("Ship_1", 600f), hostEpoch: 1, revision: 1);
        ledger.Record(Battle, Fate("Ship_1", 650f), hostEpoch: 1, revision: 0);

        Assert.Equal("captor", ledger.CaptorOf(Battle, "Ship_1"));
        Assert.Equal(600f, Assert.Single(ledger.Take(Battle, _ => true)).HitPoints);
    }

    // A sunk ship stays sunk across a capture record (a sinking hull can't be captured anyway).
    [Fact]
    public void RecordCapture_KeepsAHeldSinkFinal()
    {
        var ledger = new BattleShipFateLedger();
        ledger.Record(Battle, Fate("Ship_1", 0f, sunk: true), hostEpoch: 1, revision: 1);

        ledger.RecordCapture(Battle, "Ship_1", "captor");
        ledger.Record(Battle, Fate("Ship_1", 500f), hostEpoch: 1, revision: 1);

        Assert.True(Assert.Single(ledger.Take(Battle, _ => true)).Sunk);
    }

    // Capture 2.5 (M6 D5 widened): the captor's departure applies the ships it captured; an owner taking its ship back clears
    // the record, and the battle's finalize forgets them all.
    [Fact]
    public void CapturedBy_ListsTheCaptorsShips_UntilRetakenOrForgotten()
    {
        var ledger = new BattleShipFateLedger();
        ledger.RecordCapture(Battle, "Ship_1", "captor");
        ledger.RecordCapture(Battle, "Ship_2", "captor");
        ledger.RecordCapture(Battle, "Ship_3", "rival");
        ledger.RecordCapture("MapEvent_2", "Ship_4", "captor");

        ledger.RecordCapture(Battle, "Ship_2", null);

        Assert.Equal(new[] { "Ship_1" }, ledger.CapturedBy(Battle, "captor"));
        Assert.Null(ledger.CaptorOf(Battle, "Ship_2"));
        ledger.ForgetCaptures(Battle);
        Assert.Empty(ledger.CapturedBy(Battle, "captor"));
        Assert.Null(ledger.CaptorOf(Battle, "Ship_3"));
        Assert.Equal("captor", ledger.CaptorOf("MapEvent_2", "Ship_4"));
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
