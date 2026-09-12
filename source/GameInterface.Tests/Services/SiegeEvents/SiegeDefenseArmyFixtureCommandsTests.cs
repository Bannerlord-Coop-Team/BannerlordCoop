#if DEBUG
using Common;
using Common.Commands;
using Common.Util;
using GameInterface.Services.SiegeEvents.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

/// <summary>Checks fixture identities, assertion failures, and command registration without a live campaign.</summary>
[Collection(global::GameInterface.Tests.ModInformationRoleCollection.Name)]
public class SiegeDefenseArmyFixtureCommandsTests
{
    [Theory]
    [InlineData("baseline")]
    [InlineData("joined")]
    [InlineData("unstuck")]
    [InlineData("restored")]
    public void ExactCapturedState_PassesEachSeparateAssertion(string state)
    {
        Assert.True(SiegeDefenseArmyFixtureCommands.EvaluateState(Expected(), Observed(state), state, out var error), error);
    }

    [Theory]
    [InlineData("baseline")]
    [InlineData("joined")]
    [InlineData("unstuck")]
    [InlineData("restored")]
    public void Assertion_RejectsUnpausedCampaign(string state)
    {
        var actual = Observed(state);
        actual["paused"] = false;
        AssertFailure(Expected(), actual, state, "paused");
    }

    [Theory]
    [InlineData("SiegeOutside")]
    [InlineData("Siege")]
    public void Joined_AcceptsReliefTransitionAndInsideDefenderBattle(string battleType)
    {
        var actual = Observed("joined");
        actual["battleType"] = battleType;
        actual["siegeAssault"] = battleType == "Siege";
        Assert.True(SiegeDefenseArmyFixtureCommands.EvaluateState(Expected(), actual, "joined", out var error), error);
    }

    [Fact]
    public void Joined_RejectsUnrelatedBattleType()
    {
        var actual = Observed("joined");
        actual["battleType"] = "FieldBattle";
        AssertFailure(Expected(), actual, "joined", "battle type");
    }

    [Fact]
    public void FailedAssertion_ProducesFailureResultAndDiagnosticJson()
    {
        var actual = Observed("joined");
        Party(actual, "follower-b")["canonicalSide"] = "Attacker";
        var result = SiegeDefenseArmyFixtureCommands.FormatStateResult("joined", Expected(), actual, "joined");
        Assert.False(result.Succeeded);
        Assert.Equal("fixture_assertion_failed", result.ErrorCode);
        var json = JObject.Parse(result.Output.Split(new[] { "LIVE_TEST_JSON=" }, StringSplitOptions.None)[1]);
        Assert.False(json.Value<bool>("success"));
        Assert.Contains("follower-b", json.Value<string>("error"));
    }

    [Fact]
    public void Joined_AlliedFollowerOnAttackerSide_FailsDespiteCorrectLeaderAndCount()
    {
        var actual = Observed("joined");
        Party(actual, "follower-b")["canonicalSide"] = "Attacker";
        AssertFailure(Expected(), actual, "joined", "follower-b");
    }

    [Fact]
    public void Joined_FollowerOnDifferentDefenderEvent_Fails()
    {
        var actual = Observed("joined");
        Party(actual, "follower-b")["mapEventId"] = "different-event";
        AssertFailure(Expected(), actual, "joined", "follower-b");
    }

    [Fact]
    public void Joined_MissingFollower_FailsDespiteReportedArmyCount()
    {
        var actual = Observed("joined");
        Party(actual, "follower-b")["exists"] = false;
        AssertFailure(Expected(), actual, "joined", "participant");
    }

    [Fact]
    public void Unstuck_ReplacementThirdArmyMember_Fails()
    {
        var actual = Observed("unstuck");
        actual["armyPartyIds"] = JArray.FromObject(new[] { "player", "follower-a", "replacement" });
        AssertFailure(Expected(), actual, "unstuck", "member set");
    }

    [Fact]
    public void Unstuck_RecreatedArmyWithSameMembers_Fails()
    {
        var actual = Observed("unstuck");
        actual["armyId"] = "replacement-army";
        AssertFailure(Expected(), actual, "unstuck", "army identity");
    }

    [Fact]
    public void Unstuck_FollowerDetachedFromLeader_Fails()
    {
        var actual = Observed("unstuck");
        Party(actual, "follower-b")["attachedToId"] = null;
        AssertFailure(Expected(), actual, "unstuck", "attachment identity");
    }

    [Theory]
    [InlineData("mapEventActive")]
    [InlineData("settlementActive")]
    [InlineData("campActive")]
    public void Unstuck_FollowerStillInEncounterState_Fails(string activeField)
    {
        var actual = Observed("unstuck");
        Party(actual, "follower-b")[activeField] = true;
        AssertFailure(Expected(), actual, "unstuck", "follower-b");
    }

    [Fact]
    public void Unstuck_LocalEncounterStillOpen_Fails()
    {
        var actual = Observed("unstuck");
        actual["encounterActive"] = true;
        AssertFailure(Expected(), actual, "unstuck", "encounter remains");
    }

    [Theory]
    [InlineData("ownedArmyRegistered")]
    [InlineData("ownedSiegeRegistered")]
    [InlineData("ownedMapEventRegistered")]
    public void Restored_RejectsRegisteredOrphanDespiteCleanPartyPointers(string registeredField)
    {
        var actual = Observed("restored");
        actual[registeredField] = true;
        AssertFailure(Expected(), actual, "restored", "remains registered");
    }

    [Fact]
    public void Restored_BesiegerMovementChanged_Fails()
    {
        var actual = Observed("restored");
        Party(actual, "besieger")["behavior"]["partyPosition"]["X"] = 7;
        AssertFailure(Expected(), actual, "restored", "besieger");
    }

    [Fact]
    public void Restored_UnregisteredArmyCannotLookLikeNoArmy()
    {
        var actual = Observed("restored");
        Party(actual, "follower-b")["armyActive"] = true;
        Party(actual, "follower-b")["armyId"] = null;
        AssertFailure(Expected(), actual, "restored", "follower-b");
    }

    [Fact]
    public void Assertion_RejectsDifferentCompiledCommit()
    {
        var actual = Observed("joined");
        actual["commit"] = "other-commit";
        AssertFailure(Expected(), actual, "joined", "source identity");
    }

    [Theory]
    [InlineData("playerPartyId")]
    [InlineData("settlementNetworkId")]
    public void Assertion_RejectsMismatchedContextIdentity(string identityField)
    {
        var actual = Observed("joined");
        actual[identityField] = "replacement";
        AssertFailure(Expected(), actual, "joined", "identity");
    }

    [Fact]
    public void ExpectedState_RequiresCapturedIdentitiesAndStagedNetworkIds()
    {
        var expected = Expected();
        Assert.True(ReadExpected(expected, true));
        expected["armyId"] = null;
        Assert.False(ReadExpected(expected, true));
        Assert.True(ReadExpected(expected, false));
        expected["followerPartyIds"] = new JArray("follower-a");
        Assert.False(ReadExpected(expected, false));
    }

    [Fact]
    public void ExpectedState_RejectsMalformedJsonAndUnknownController()
    {
        Assert.False(SiegeDefenseArmyFixtureCommands.TryReadExpectation("not-json", "controller", "town_ES1", true, out _, out _));
        var json = new JObject { ["expectation"] = Expected() }.ToString(Formatting.None);
        Assert.False(SiegeDefenseArmyFixtureCommands.TryReadExpectation(json, "other-controller", "town_ES1", true, out _, out _));
    }

    [Theory]
    [InlineData("fixtureToken", "22222222222222222222222222222222")]
    [InlineData("playerPartyId", "replacement-player")]
    [InlineData("besiegerPartyId", "replacement-besieger")]
    [InlineData("settlementNetworkId", "replacement-settlement")]
    public void ActiveCapture_RejectsWrongTokenOrReplacedIdentity(string field, string replacement)
    {
        var captured = Expected();
        var supplied = (JObject)captured.DeepClone();
        supplied[field] = replacement;
        Assert.False(SiegeDefenseArmyFixtureCommands.MatchesCapture(captured, supplied, out var error));
        Assert.Contains(field, error);
    }

    [Fact]
    public void Readiness_RejectsAbsentAndInactiveParty()
    {
        Assert.False(SiegeDefenseArmyFixtureCommands.IsCleanParty(null));
        Assert.False(SiegeDefenseArmyFixtureCommands.IsCleanParty(ObjectHelper.SkipConstructor<MobileParty>()));
    }

    [Fact]
    public void DirectCommands_RegisterSixUniqueCommandsAndNoClientMutationShortcuts()
    {
        var commands = typeof(SiegeDefenseArmyFixtureCommands).GetNestedTypes()
            .Where(t => typeof(ICoopCommand).IsAssignableFrom(t))
            .Select(t => (ICoopCommand)Activator.CreateInstance(t)).ToArray();
        var registry = new CoopCommandRegistry(commands, new LoggerConfiguration().CreateLogger());
        Assert.Equal(6, registry.Commands.Count);
        Assert.Equal(4, commands.Count(c => c.Side == CoopCommandSide.Server));
        Assert.Equal(2, commands.Count(c => c.Side == CoopCommandSide.Both));
        Assert.DoesNotContain(commands, c => c.Name == "open_defender_encounter" || c.Name == "invoke_defender_join");
        var command = new SiegeDefenseArmyFixtureCommands.DefenseArmyStateCoopCommand();
        var args = new CoopCommandArgsFactory().FromValues(new[] { "controller", "town_ES1", "joined" });
        var result = registry.ProcessCommand($"{command.Prefix}.{command.Name}", args);
        Assert.False(result.Succeeded);
        Assert.Equal("invalid_arguments", result.ErrorCode);
    }

    [Fact]
    public void MutationCommands_RejectClientWithExplicitFailure()
    {
        bool previous = ModInformation.IsServer;
        try
        {
            ModInformation.IsServer = false;
            var result = new SiegeDefenseArmyFixtureCommands.StageFixtureCoopCommand()
                .ProcessCommand(new CoopCommandArgsFactory().FromValues(new[] { "{}" }));
            Assert.False(result.Succeeded);
            Assert.Equal("command_failed", result.ErrorCode);
            Assert.Contains("server", result.Output);
        }
        finally
        {
            ModInformation.IsServer = previous;
        }
    }

    [Fact]
    public void StateCommand_MissingCapture_IsExplicitFailureBeforeCampaignAccess()
    {
        var result = new SiegeDefenseArmyFixtureCommands.DefenseArmyStateCoopCommand()
            .ProcessCommand(new CoopCommandArgsFactory().FromValues(new[] { "controller", "town_ES1", "joined", "{}" }));
        Assert.False(result.Succeeded);
        Assert.Equal("command_failed", result.ErrorCode);
        Assert.Contains("identities", result.Output);
    }

    private static void AssertFailure(JObject expected, JObject actual, string state, string diagnostic)
    {
        Assert.False(SiegeDefenseArmyFixtureCommands.EvaluateState(expected, actual, state, out var error));
        Assert.Contains(diagnostic, error);
    }

    private static bool ReadExpected(JObject expected, bool staged) =>
        SiegeDefenseArmyFixtureCommands.TryReadExpectation(new JObject { ["expectation"] = expected }.ToString(Formatting.None),
            "controller", "town_ES1", staged, out _, out _);

    private static JObject Party(JObject observed, string id) =>
        (JObject)observed["parties"].Single(p => p.Value<string>("partyId") == id);

    private static JObject Behavior(string id) => JObject.FromObject(new
    {
        MobilePartyId = id,
        partyPosition = new { X = 1, Y = 2, IsOnLand = true },
        partyMoveMode = "Hold",
    });

    private static JObject Expected() => JObject.FromObject(new
    {
        schemaVersion = 1,
        buildVersion = "1.0.0+fixture-commit",
        commit = "fixture-commit",
        fixtureToken = "11111111111111111111111111111111",
        controllerId = "controller",
        settlementId = "town_ES1",
        settlementNetworkId = "town_ES1",
        playerPartyId = "player",
        besiegerPartyId = "besieger",
        followerPartyIds = new[] { "follower-a", "follower-b" },
        armyId = "army-captured",
        siegeEventId = "siege-captured",
        mapEventId = "event-captured",
        capturedParties = new[] { "player", "besieger", "follower-a", "follower-b" }
            .Select(id => new { partyId = id, behavior = Behavior(id) }),
    });

    private static JObject Observed(string state)
    {
        bool restored = state == "restored";
        bool joined = state == "joined";
        var armyIds = new[] { "player", "follower-a", "follower-b" };
        return JObject.FromObject(new
        {
            buildVersion = "1.0.0+fixture-commit",
            commit = "fixture-commit",
            paused = true,
            playerPartyId = "player",
            settlementNetworkId = "town_ES1",
            siegeActive = !restored,
            siegeEventId = restored ? null : "siege-captured",
            settlementMapEventActive = !restored,
            settlementMapEventId = restored ? null : "event-captured",
            siegeAssault = !restored && !joined,
            battleType = joined ? "SiegeOutside" : "Siege",
            armyId = restored ? null : "army-captured",
            armyLeaderPartyId = restored ? null : "player",
            armyPartyIds = restored ? Array.Empty<string>() : armyIds,
            encounterActive = joined,
            ownedArmyRegistered = !restored,
            ownedSiegeRegistered = !restored,
            ownedMapEventRegistered = !restored,
            parties = new[] { "player", "besieger", "follower-a", "follower-b" }.Select(id => new
            {
                partyId = id,
                exists = true,
                active = true,
                armyActive = !restored && id != "besieger",
                armyId = restored || id == "besieger" ? null : "army-captured",
                attachedToActive = !restored && id.StartsWith("follower-", StringComparison.Ordinal),
                attachedToId = !restored && id.StartsWith("follower-", StringComparison.Ordinal) ? "player" : null,
                attachedPartyIds = !restored && id == "player" ? new[] { "follower-a", "follower-b" } : Array.Empty<string>(),
                mapEventActive = !restored && (joined || id == "besieger"),
                mapEventId = !restored && (joined || id == "besieger") ? "event-captured" : null,
                canonicalSide = joined && id != "besieger" ? "Defender" : null,
                settlementActive = false,
                campActive = !restored && id == "besieger",
                behavior = Behavior(id),
            }),
        });
    }
}
#endif
