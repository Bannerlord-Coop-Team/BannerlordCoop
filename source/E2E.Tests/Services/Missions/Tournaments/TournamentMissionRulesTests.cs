using GameInterface.Services.ObjectManager;
using GameInterface.Services.Tournaments;
using GameInterface.Services.Tournaments.Data;
using Missions;
using Missions.Tournaments;
using Missions.Tournaments.Spectators;
using Moq;
using System.Reflection;
using SandBox.Missions.MissionLogics;
using SandBox.Missions.MissionLogics.Arena;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions;

namespace E2E.Tests.Services.Missions.Tournaments;

public class TournamentMissionRulesTests
{
    private static TournamentContestantData Contestant(
        string slotId,
        string characterId,
        int seed,
        string controllerId = null,
        bool isHuman = false,
        bool isReplaced = false)
        => new TournamentContestantData(
            slotId, characterId, seed, controllerId, slotId, isHuman, isReplaced, false, null);

    private static TournamentSessionSnapshot Snapshot(
        TournamentContestantData[] contestants,
        TournamentSessionPhase phase = TournamentSessionPhase.LiveMatch,
        long bracketRevision = 4,
        string currentMatchId = "match")
    {
        var teams = new[]
        {
            new TournamentTeamData("red", new[] { "npc" }, 0, false, 1, null),
            new TournamentTeamData("blue", new[] { "human" }, 0, false, 2, null)
        };
        var match = new TournamentMatchData(currentMatchId, "round", 1, 1, 1, teams, Array.Empty<string>());
        return new TournamentSessionSnapshot(
            "session", "mission", "town", "scene", "prize",
            phase, 8, bracketRevision, currentMatchId, "host",
            Array.Empty<string>(), contestants, Array.Empty<string>(),
            Array.Empty<TournamentPlayerChoiceData>(),
            new[] { new TournamentRoundData("round", 0, 0, new[] { match }) },
            0, 0, 0, false, false, null);
    }

    private static TournamentAgentSpawnData Agent(
        Guid agentId,
        string slotId,
        string characterId,
        int seed,
        string teamId,
        uint color,
        string controllerId,
        Guid mountId = default,
        string mountCharacterId = "horse-character")
        => new TournamentAgentSpawnData(
            agentId, slotId, characterId, seed, teamId, color, null, controllerId,
            new[] { new TournamentEquipmentElementData(0, "weapon", null, null, 37) },
            1, 2, 3, 0, 1, 100,
            mountId,
            mountId == Guid.Empty ? null : mountCharacterId,
            77,
            mountId == Guid.Empty
                ? Array.Empty<TournamentEquipmentElementData>()
                : new[] { new TournamentEquipmentElementData(10, "horse-item", null) },
            mountId == Guid.Empty ? 0 : 90);

    [Fact]
    public void HostControlPolicy_KeepsNpcsAiAndOnlyOwnHumanPlayerControlled()
    {
        var npc = Contestant("npc", "npc-character", 1);
        var human = Contestant("human", "human-character", 2, "host", true);
        var remoteHuman = Contestant("remote", "remote-character", 3, "remote", true);

        Assert.Equal(TournamentAgentControlRole.NpcAuthority,
            TournamentAgentControlPolicy.Resolve(npc, "host", "host"));
        Assert.Equal(TournamentAgentControlRole.HumanPlayer,
            TournamentAgentControlPolicy.Resolve(human, "host", "host"));
        Assert.Equal(TournamentAgentControlRole.Puppet,
            TournamentAgentControlPolicy.Resolve(remoteHuman, "remote", "host"));
    }

    [Fact]
    public void ReplacementBeforeStart_RequiresBracketRefresh()
    {
        TournamentSessionSnapshot previous = Snapshot(
            new[]
            {
                Contestant("npc", "npc-character", 11),
                Contestant("human", "human-character", 22, "fighter", true)
            },
            TournamentSessionPhase.AwaitingChoices,
            4);
        TournamentSessionSnapshot snapshot = Snapshot(
            new[]
            {
                Contestant("npc", "npc-character", 11),
                Contestant("human", "culture-basic-troop", 22, null, true, true)
            },
            TournamentSessionPhase.AwaitingChoices,
            5);

        Assert.True(TournamentMatchTransitionRules.RequiresBracketRefresh(previous, snapshot));
    }

    [Fact]
    public void LiveReplacement_PreservesRunningNativeMatchUntilNextBracket()
    {
        TournamentSessionSnapshot previous = Snapshot(new[]
        {
            Contestant("npc", "npc-character", 11),
            Contestant("human", "human-character", 22, "fighter", true)
        });
        TournamentSessionSnapshot updated = Snapshot(new[]
        {
            Contestant("npc", "npc-character", 11),
            Contestant("human", "culture-basic-troop", 22, null, true, true)
        });

        Assert.True(TournamentMatchTransitionRules.PreservesRunningMatch(previous, updated));
        Assert.False(TournamentMatchTransitionRules.RequiresBracketRefresh(previous, updated));
        Assert.False(TournamentMatchTransitionRules.RequiresArenaCleanup(previous, updated));
    }

    [Fact]
    public void SimulatedMatchAdvance_DoesNotClearArenaMissionState()
    {
        TournamentContestantData[] contestants =
        {
            Contestant("npc", "npc-character", 11),
            Contestant("human", "human-character", 22, "fighter", true)
        };
        TournamentSessionSnapshot previous = Snapshot(
            contestants,
            TournamentSessionPhase.AwaitingChoices,
            4,
            "round:0:match:0");
        TournamentSessionSnapshot updated = Snapshot(
            contestants,
            TournamentSessionPhase.AwaitingChoices,
            5,
            "round:0:match:1");

        Assert.True(TournamentMatchTransitionRules.RequiresBracketRefresh(previous, updated));
        Assert.False(TournamentMatchTransitionRules.RequiresArenaCleanup(previous, updated));
    }

    [Fact]
    public void CompletedLiveMatch_ClearsFinishedArenaAgents()
    {
        TournamentContestantData[] contestants =
        {
            Contestant("npc", "npc-character", 11),
            Contestant("human", "human-character", 22, "fighter", true)
        };
        TournamentSessionSnapshot previous = Snapshot(
            contestants,
            TournamentSessionPhase.LiveMatch,
            4,
            "round:0:match:0");
        TournamentSessionSnapshot updated = Snapshot(
            contestants,
            TournamentSessionPhase.AwaitingChoices,
            5,
            "round:0:match:1");

        Assert.True(TournamentMatchTransitionRules.RequiresArenaCleanup(previous, updated));
    }

    [Fact]
    public void HostRuntime_PreservesLocalHumanOwnerButCorrectsSpectatorPuppets()
    {
        TournamentSessionSnapshot snapshot = Snapshot(new[]
        {
            Contestant("npc", "npc-character", 11),
            Contestant("human", "human-character", 22, "fighter", true)
        });
        Guid humanAgentId = Guid.NewGuid();
        Guid humanMountId = Guid.NewGuid();
        Guid npcAgentId = Guid.NewGuid();
        var manifest = new TournamentSpawnManifestData(
            "session",
            "match",
            8,
            4,
            1,
            new[]
            {
                Agent(
                    humanAgentId,
                    "human",
                    "human-character",
                    22,
                    "blue",
                    2,
                    "fighter",
                    humanMountId),
                Agent(npcAgentId, "npc", "npc-character", 11, "red", 1, "host")
            });

        Assert.False(TournamentRuntimeAuthority.ShouldApplyHostAggregate(
            humanAgentId, manifest, snapshot, "fighter"));
        Assert.False(TournamentRuntimeAuthority.ShouldApplyHostAggregate(
            humanMountId, manifest, snapshot, "fighter"));
        Assert.True(TournamentRuntimeAuthority.ShouldApplyHostAggregate(
            humanAgentId, manifest, snapshot, "spectator"));
        Assert.True(TournamentRuntimeAuthority.ShouldApplyHostAggregate(
            npcAgentId, manifest, snapshot, "fighter"));
    }

    [Fact]
    public void MountedManifest_RoundTripsExactMountIdentityAndRejectsNullAgentArrays()
    {
        TournamentSessionSnapshot snapshot = Snapshot(new[]
        {
            Contestant("npc", "npc-character", 11),
            Contestant("human", "human-character", 22, "fighter", true)
        });
        Guid mountId = Guid.NewGuid();
        var manifest = new TournamentSpawnManifestData(
            "session", "match", 8, 4, 1,
            new[]
            {
                Agent(Guid.NewGuid(), "human", "human-character", 22, "blue", 2, "fighter"),
                Agent(Guid.NewGuid(), "npc", "npc-character", 11, "red", 1, "host", mountId)
            });

        Assert.True(TournamentSpawnManifestValidator.IsValid(manifest, snapshot));
        TournamentAgentSpawnData mounted = manifest.Agents.Single(agent => agent.MountAgentId != Guid.Empty);
        Assert.Equal(mountId, mounted.MountAgentId);
        Assert.Equal("horse-character", mounted.MountCharacterId);
        Assert.Equal(77, mounted.MountDescriptorSeed);
        Assert.Equal("horse-item", Assert.Single(mounted.MountEquipment).ItemId);
        Assert.All(manifest.Agents, agent => Assert.Equal(uint.MaxValue, agent.TeamColor2));
        Assert.Equal(37, manifest.Agents[0].Equipment[0].DataValue);
        Assert.True(manifest.Agents[0].Equipment[0].HasDataValue);

        var malformed = Common.Util.ObjectHelper.SkipConstructor<TournamentSpawnManifestData>();
        Assert.False(TournamentSpawnManifestValidator.IsValid(malformed, snapshot));
    }

    [Fact]
    public void MountedManifest_AllowsNativeMountWithoutCharacterIdentity()
    {
        TournamentSessionSnapshot snapshot = Snapshot(new[]
        {
            Contestant("npc", "npc-character", 11),
            Contestant("human", "human-character", 22, "fighter", true)
        });
        var manifest = new TournamentSpawnManifestData(
            "session", "match", 8, 4, 1,
            new[]
            {
                Agent(Guid.NewGuid(), "human", "human-character", 22, "blue", 2, "fighter"),
                Agent(
                    Guid.NewGuid(),
                    "npc",
                    "npc-character",
                    11,
                    "red",
                    1,
                    "host",
                    Guid.NewGuid(),
                    null)
            });

        Assert.True(TournamentSpawnManifestValidator.IsValid(manifest, snapshot));
        Assert.Null(manifest.Agents.Single(agent => agent.MountAgentId != Guid.Empty).MountCharacterId);
    }

    [Fact]
    public void SpawnManifestEquipmentSerialization_DoesNotReadArmorSlotsFromMissionEquipment()
    {
        var builder = new TournamentSpawnManifestBuilder(
            Mock.Of<IObjectManager>(),
            Mock.Of<ICoopMissionComponent>());
        MethodInfo serialize = typeof(TournamentSpawnManifestBuilder).GetMethod(
            "TrySerializeEquipment",
            BindingFlags.Instance | BindingFlags.NonPublic);
        object[] arguments = { new MissionEquipment(), new Equipment(), null };

        bool succeeded = (bool)serialize.Invoke(builder, arguments);

        Assert.True(succeeded);
        Assert.Empty((TournamentEquipmentElementData[])arguments[2]);
    }

    [Fact]
    public void SpawnManifestDirection_NormalizesNonFiniteNativeDirection()
    {
        MethodInfo normalize = typeof(TournamentSpawnManifestBuilder).GetMethod(
            "NormalizeDirection",
            BindingFlags.Static | BindingFlags.NonPublic);

        Vec2 direction = (Vec2)normalize.Invoke(
            null,
            new object[] { new Vec2(float.NaN, float.PositiveInfinity) });

        Assert.Equal(0f, direction.X);
        Assert.Equal(1f, direction.Y);
    }

    [Fact]
    public void TournamentFightBehaviorComposition_PreservesVanillaOrderWithCoopReplacements()
    {
        Assert.Equal(new[]
        {
            typeof(CampaignMissionComponent),
            typeof(EquipmentControllerLeaveLogic),
            typeof(CoopTournamentFightMissionController),
            typeof(CoopTournamentBehavior),
            typeof(AgentVictoryLogic),
            typeof(MissionAgentPanicHandler),
            typeof(AgentHumanAILogic),
            typeof(ArenaAgentStateDeciderLogic),
            typeof(MissionHardBorderPlacer),
            typeof(MissionBoundaryPlacer),
            typeof(TournamentSpectatorBarrierPlacer),
            typeof(MissionOptionsComponent),
            typeof(HighlightsController),
            typeof(SandboxHighlightsController),
            typeof(CoopTournamentController)
        }, CoopTournamentLauncher.BehaviorOrder);
    }
}
