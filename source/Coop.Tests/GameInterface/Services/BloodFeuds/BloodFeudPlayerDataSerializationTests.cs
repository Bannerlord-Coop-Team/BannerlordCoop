using Common.Serialization;
using GameInterface.CoopSessionData.Messages;
using GameInterface.CoopSessionData.Save.Data;
using GameInterface.Services.BloodFeuds;
using GameInterface.Services.BloodFeuds.Data;
using GameInterface.Surrogates;
using ProtoBuf;
using System.IO;
using System.Text.Json;
using Xunit;

namespace Coop.Tests.GameInterface.Services.BloodFeuds;

/// <summary>
/// Blood feuds reach clients over the wire and are kept between sessions in the JSON session file.
/// </summary>
public class BloodFeudPlayerDataSerializationTests
{
    public BloodFeudPlayerDataSerializationTests()
    {
        _ = new SurrogateCollection();
    }

    [Fact]
    public void NetworkUpdateCoopSession_KeepsFeudsAndPendingExecutions()
    {
        var received = RoundTrip(new NetworkUpdateCoopSession(CreateSession())).UpdatedSession.BloodFeudPlayerData;

        AssertMatchesCreatedSession(received);
    }

    [Fact]
    public void JsonSessionFile_KeepsFeudsAndPendingExecutions()
    {
        var options = new JsonFileIO().JsonOptions;
        var json = JsonSerializer.Serialize(CreateSession(), options);

        var loaded = JsonSerializer.Deserialize<CoopSession>(json, options).BloodFeudPlayerData;

        AssertMatchesCreatedSession(loaded);
    }

    private static CoopSession CreateSession()
    {
        var session = CoopSession.Empty;
        var data = session.BloodFeudPlayerData;
        data.PlayerBloodFeuds["MyHero1"] = new()
        {
            // A settled feud keeps its counts, like vanilla after relations improve
            ["clan_empire_south_2"] = new BloodFeudData { HasBloodFeud = false, ExecutionsDoneCount = 2, ExecutionsReceivedCount = 1 },
            ["clan_empire_south_3"] = new BloodFeudData { HasBloodFeud = true, ExecutionsDoneCount = 0, ExecutionsReceivedCount = 3 },
        };
        data.PlayerPendingSettlementExecutions["MyHero1"] = new()
        {
            ["companion_1"] = new PendingSettlementExecution("clan_empire_south_3", 78691296244235L),
        };
        data.PlayerHeroesPendingMapEventEndExecution["MyHero1"] = new() { "lord_1_17", "lord_1_29" };
        return session;
    }

    private static void AssertMatchesCreatedSession(BloodFeudPlayerData data)
    {
        var feuds = data.PlayerBloodFeuds["MyHero1"];
        var settled = feuds["clan_empire_south_2"];
        Assert.Equal((false, 2, 1), (settled.HasBloodFeud, settled.ExecutionsDoneCount, settled.ExecutionsReceivedCount));
        var active = feuds["clan_empire_south_3"];
        Assert.Equal((true, 0, 3), (active.HasBloodFeud, active.ExecutionsDoneCount, active.ExecutionsReceivedCount));

        var pending = data.PlayerPendingSettlementExecutions["MyHero1"]["companion_1"];
        Assert.Equal(("clan_empire_south_3", 78691296244235L), (pending.CapturerClanId, pending.ExecutionDateTicks));

        Assert.Equal(new[] { "lord_1_17", "lord_1_29" }, data.PlayerHeroesPendingMapEventEndExecution["MyHero1"]);
    }

    private static T RoundTrip<T>(T message)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, message);
        stream.Position = 0;
        return Serializer.Deserialize<T>(stream);
    }
}
