using GameInterface.Services.Ships;
using GameInterface.Services.Ships.Data;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GameInterface.Tests.Services.Ships;

/// <summary>Verifies server diagnostics preserve pending reports and expose migration/capture report boundaries.</summary>
public class BattleShipFateInspectionTests
{
    // Inspection must not consume a fate or keep following mutable ledger state after returning.
    [Fact]
    public void Inspect_CopiesWithoutConsumingAndKeepsNewestEpoch()
    {
        var ledger = new BattleShipFateLedger();
        ledger.Record("battle", new BattleShipFate("ship", 18000f, 200f, false), 1, 9);
        ledger.Record("battle", new BattleShipFate("ship", 17700f, 180f, false, 12f), 2, 1);
        ledger.Record("battle", new BattleShipFate("ship", 17900f, 190f, false), 1, 10);
        var snapshot = ledger.Inspect();
        Assert.Single(ledger.Take("battle", _ => true));
        var battle = Assert.Single(JArray.FromObject(snapshot));
        var ship = Assert.Single((JArray)battle["ships"]!);
        Assert.Equal(17700f, (float)ship["hitPoints"]!);
        Assert.Equal(2, (int)ship["hostEpoch"]!);
        Assert.Equal(1L, (long)ship["revision"]!);
        Assert.Equal(12f, (float)ship["mitigatedDamage"]!);
        Assert.Empty(JArray.FromObject(ledger.Inspect()));
    }

    // Capture invalidates the old reporter's stamp, while the retained HP remains visible until its successor reports.
    [Fact]
    public void Inspect_DistinguishesPreCaptureHpAndAnUnreportedCapture()
    {
        var ledger = new BattleShipFateLedger();
        ledger.Record("battle", new BattleShipFate("ship", 18000f, 200f, false), 1, 9);
        ledger.RecordCapture("battle", "ship", "captor", 1);
        ledger.RecordCapture("battle", "unreported", "captor", 2);
        var battle = Assert.Single(JArray.FromObject(ledger.Inspect()));
        var ships = (JArray)battle["ships"]!;
        Assert.Equal(2, ships.Count);
        Assert.True((bool)ships[0]["awaitingCaptureReport"]!);
        Assert.Equal(JTokenType.Null, ships[0]["hostEpoch"]!.Type);
        Assert.Equal(18000f, (float)ships[0]["hitPoints"]!);
        Assert.False((bool)ships[1]["pendingFate"]!);
        Assert.Equal(JTokenType.Null, ships[1]["hitPoints"]!.Type);
        Assert.Equal(JTokenType.Null, ships[1]["awaitingCaptureReport"]!.Type);
        ledger.Record("battle", new BattleShipFate("ship", 17700f, 200f, false), 1, 1);
        battle = Assert.Single(JArray.FromObject(ledger.Inspect()));
        var ship = battle["ships"]![0];
        Assert.False((bool)ship["awaitingCaptureReport"]!);
        Assert.Equal(17700f, (float)ship["hitPoints"]!);
        Assert.Equal("captor", (string)ship["captor"]!);
        Assert.Equal(1, (int)ship["captureSequence"]!);
        Assert.Single(ledger.Take("battle", fate => fate.CampaignShipId == "ship"));
        battle = Assert.Single(JArray.FromObject(ledger.Inspect()));
        ship = battle["ships"]![0];
        Assert.Equal(JTokenType.Null, ship["hitPoints"]!.Type);
        Assert.Equal(JTokenType.Null, ship["awaitingCaptureReport"]!.Type);
    }
}
