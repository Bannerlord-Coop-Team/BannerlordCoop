using GameInterface.CoopSessionData.Messages;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Services.NavalDLC;
using GameInterface.Surrogates;
using ProtoBuf;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Coop.Tests.GameInterface.Services.NavalDLC;

/// <summary>
/// The naval player data clients receive in the session update the server sends after a change.
/// </summary>
public class NavalPlayerDataSerializationTests
{
    public NavalPlayerDataSerializationTests()
    {
        // The session also carries game types such as CampaignTime, sent through their surrogates
        _ = new SurrogateCollection();
    }

    [Fact]
    public void NetworkUpdateCoopSession_KeepsSentTroopsAndCachedFleets()
    {
        var session = CoopSession.Empty;
        session.NavalPlayerData.PlayerSentTroops["MyHero1"] = new()
        {
            new SentTroopsData(new() { new SentTroopData("imperial_recruit", 12, 3, 450) }, 99999L, "Sea Wolf", "Garrison Run"),
        };
        var cachedShip = new CachedShipData(
            "ship_hull_1",
            "Sea Wolf",
            40f,
            30f,
            "figurehead_1",
            "sail_pattern_1",
            7,
            new() { ["slot1"] = "piece_1" },
            new() { "piece_1" });
        session.NavalPlayerData.PlayerCachedFleets["MyHero1"] = new PlayerCachedFleetData(new() { cachedShip }, float.NaN, 20f, false, 31f, 41f, true);

        var received = RoundTrip(new NetworkUpdateCoopSession(session)).UpdatedSession.NavalPlayerData;

        var sentTroops = Assert.Single(received.PlayerSentTroops["MyHero1"]);
        Assert.Equal((99999L, "Sea Wolf", "Garrison Run"), (sentTroops.ReturnTimeTicks, sentTroops.ShipName, sentTroops.PartyName));
        var troop = Assert.Single(sentTroops.Troops);
        Assert.Equal(("imperial_recruit", 12, 3, 450), (troop.CharacterId, troop.Number, troop.WoundedNumber, troop.Xp));

        var fleet = received.PlayerCachedFleets["MyHero1"];
        Assert.True(float.IsNaN(fleet.AnchorX));
        Assert.Equal((20f, false, 31f, 41f, true), (fleet.AnchorY, fleet.AnchorIsOnLand, fleet.DisembarkX, fleet.DisembarkY, fleet.DisembarkIsOnLand));
        var ship = Assert.Single(fleet.Ships);
        Assert.Equal(("ship_hull_1", "Sea Wolf", 40f, 30f), (ship.HullId, ship.Name, ship.HitPoints, ship.SailHitPoints));
        Assert.Equal(("figurehead_1", "sail_pattern_1", 7), (ship.FigureheadId, ship.CustomSailPatternId, ship.RandomValue));
        Assert.Equal("piece_1", ship.SlotPieceIds["slot1"]);
        Assert.Equal(new[] { "piece_1" }, ship.UnlockedPieceIds);
    }

    private static T RoundTrip<T>(T message)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, message);
        stream.Position = 0;
        return Serializer.Deserialize<T>(stream);
    }
}
