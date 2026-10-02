#if DEBUG
using Missions.Battles;
using System;
using Xunit;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace E2E.Tests.Services.Missions;

public class BattleReproductionFixtureCommandsTests
{
    [Fact]
    public void CameraCoordinates_RejectNonFiniteCoincidentAndMissingSubjects()
    {
        string subject = Guid.NewGuid().ToString();
        Assert.False(BattleReproductionFixtureCommands.TryParseCamera(
            new[] { "NaN", "0", "10", "0", "0", "0", subject }, out _, out _, out _));
        Assert.False(BattleReproductionFixtureCommands.TryParseCamera(
            new[] { "0", "0", "0", "0", "0", "0", subject }, out _, out _, out _));
        Assert.False(BattleReproductionFixtureCommands.TryParseCamera(
            new[] { "0", "0", "10", "0", "0", "0", Guid.Empty.ToString() }, out _, out _, out _));
        Assert.True(BattleReproductionFixtureCommands.TryParseCamera(
            new[] { "2.5", "-5", "10", "0", "0", "1", subject }, out var position, out var target, out var id));
        Assert.Equal(2.5f, position.x);
        Assert.Equal(1f, target.z);
        Assert.Equal(Guid.Parse(subject), id);
    }
    [Fact]
    public void MissingAgent_PreservesIdentityWithoutInventingHealthOrDeath()
    {
        var history = new Dictionary<string, JObject>();
        BattleReproductionFixtureCommands.MergeObservations(new[]
        {
            new { agentId = "subject", active = true, health = 42f, state = "Active" }
        }, history);
        var missing = BattleReproductionFixtureCommands.MergeObservations(Array.Empty<object>(), history).Single();
        Assert.Equal("subject", (string)missing["agentId"]);
        Assert.True((bool)missing["missing"]);
        Assert.Equal("Missing", (string)missing["state"]);
        Assert.Equal(42f, (float)missing["lastObservedHealth"]);
        Assert.Equal(JTokenType.Null, missing["health"].Type);
    }
}
#endif
