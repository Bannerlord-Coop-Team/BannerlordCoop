using GameInterface.Services.Tournaments.Data;
using Missions.Tournaments;
using Missions.Tournaments.Messages;
using TaleWorlds.Library;

namespace E2E.Tests.Services.Missions.Tournaments;

public class TournamentManifestAuthorityTests
{
    private static TournamentSessionSnapshot Snapshot(
        string hostControllerId,
        TournamentContestantData[] contestants)
        => new TournamentSessionSnapshot(
            "session",
            "mission",
            "town",
            "scene",
            null,
            TournamentSessionPhase.LiveMatch,
            7,
            3,
            "match",
            hostControllerId,
            System.Array.Empty<string>(),
            contestants,
            System.Array.Empty<string>(),
            System.Array.Empty<TournamentPlayerChoiceData>(),
            System.Array.Empty<TournamentRoundData>(),
            0,
            0,
            0,
            false,
            false,
            null);

    private static TournamentAgentSpawnData Agent(string slotId, string controllerId)
        => new TournamentAgentSpawnData(
            Guid.NewGuid(),
            slotId,
            "character",
            1,
            "team",
            1,
            null,
            controllerId,
            System.Array.Empty<TournamentEquipmentElementData>(),
            1,
            2,
            3,
            0,
            1,
            100,
            Guid.Empty,
            null,
            0,
            System.Array.Empty<TournamentEquipmentElementData>(),
            0);

    private static TournamentContestantData Contestant(
        string slotId,
        string controllerId,
        bool isHuman,
        bool isReplaced = false)
        => new TournamentContestantData(
            slotId,
            "character",
            1,
            controllerId,
            slotId,
            isHuman,
            isReplaced,
            false,
            null);

    [Fact]
    public void Normalize_RebindsNpcAndReplacementWithoutStealingActiveHuman()
    {
        TournamentAgentSpawnData npc = Agent("npc", "host-a");
        TournamentAgentSpawnData human = Agent("human", "fighter");
        TournamentAgentSpawnData replacement = Agent("replacement", "departed");
        var manifest = new TournamentSpawnManifestData(
            "session", "match", 7, 3, 1, new[] { npc, human, replacement });
        TournamentSessionSnapshot snapshot = Snapshot("host-b", new[]
        {
            Contestant("npc", null, false),
            Contestant("human", "fighter", true),
            Contestant("replacement", null, true, true)
        });

        TournamentSpawnManifestData normalized = TournamentManifestAuthority.Normalize(manifest, snapshot);

        Assert.Equal("host-b", normalized.Agents[0].ControllerId);
        Assert.Equal("fighter", normalized.Agents[1].ControllerId);
        Assert.Equal("host-b", normalized.Agents[2].ControllerId);
        Assert.Equal(3, normalized.BracketRevision);
        Assert.Equal(npc.AgentId, normalized.Agents[0].AgentId);
    }

    [Fact]
    public void Normalize_AfterTwoHostMigrations_UsesLatestHost()
    {
        TournamentAgentSpawnData alive = Agent("npc-alive", "host-a");
        TournamentAgentSpawnData knockedOut = Agent("npc-out", "host-a");
        var contestants = new[]
        {
            Contestant("npc-alive", null, false),
            Contestant("npc-out", null, false)
        };
        var manifest = new TournamentSpawnManifestData(
            "session", "match", 7, 3, 1, new[] { alive, knockedOut });

        manifest = TournamentManifestAuthority.Normalize(manifest, Snapshot("host-b", contestants));
        manifest = TournamentManifestAuthority.Normalize(manifest, Snapshot("host-c", contestants));
        Assert.All(manifest.Agents, data => Assert.Equal("host-c", data.ControllerId));
    }

    [Fact]
    public void SuccessorRecovery_ReusesCachedCanonicalManifestWithoutRegeneration()
    {
        TournamentAgentSpawnData npc = Agent("npc", "host-a");
        TournamentContestantData[] contestants =
        {
            Contestant("npc", null, false)
        };
        var manifest = new TournamentSpawnManifestData(
            "session",
            "match",
            7,
            3,
            41,
            new[] { npc });
        TournamentSessionSnapshot promoted = Snapshot("host-b", contestants);

        Assert.True(TournamentManifestAuthority.CanResume(manifest, promoted));
        TournamentSpawnManifestData normalized =
            TournamentManifestAuthority.Normalize(manifest, promoted);
        Assert.Equal(41, normalized.Sequence);
        Assert.Equal(npc.AgentId, Assert.Single(normalized.Agents).AgentId);
        Assert.Equal("host-b", normalized.Agents[0].ControllerId);

        var staleBracket = new TournamentSpawnManifestData(
            "session",
            "match",
            7,
            2,
            41,
            new[] { npc });
        Assert.False(TournamentManifestAuthority.CanResume(staleBracket, promoted));
    }

    [Fact]
    public void LateCatchUp_RuntimeStateCarriesCurrentEquipmentAndStableDroppedWorldItems()
    {
        Guid agentId = Guid.NewGuid();
        Guid worldItemId = Guid.NewGuid();
        var weapon = new TournamentMissionWeaponData(
            2,
            "practice_bow",
            "modifier",
            "banner",
            17);
        var worldItem = new TournamentWorldItemRuntimeData(
            worldItemId,
            "practice_sword",
            null,
            null,
            9,
            new Vec3(1, 2, 3),
            Mat3.Identity,
            4,
            false);
        var runtime = new NetworkTournamentRuntimeState(
            "session",
            "match",
            8,
            "host",
            4,
            Array.Empty<TournamentTeamScoreData>(),
            new TournamentAgentRuntimeData[]
            {
                null,
                new TournamentAgentRuntimeData(agentId, 72f, new[] { weapon })
            },
            new TournamentWorldItemRuntimeData[]
            {
                null,
                worldItem,
                new TournamentWorldItemRuntimeData(
                    Guid.Empty,
                    "invalid",
                    null,
                    null,
                    0,
                    Vec3.Zero,
                    Mat3.Identity,
                    0,
                    false)
            });

        var agents = TournamentRuntimeStateRules.GetAgents(runtime);
        var worldItems = TournamentRuntimeStateRules.GetWorldItems(runtime);

        Assert.Equal(weapon, Assert.Single(agents[agentId].Equipment));
        Assert.Equal(worldItem, Assert.Single(worldItems).Value);
        Assert.Equal(worldItemId, Assert.Single(worldItems).Key);
    }

    [Fact]
    public void RuntimeContracts_NormalizeNullCollectionsForProtobufSkipConstructorSafety()
    {
        Guid agentId = Guid.NewGuid();
        var agent = new TournamentAgentRuntimeData(agentId, 10f, null);
        var runtime = new NetworkTournamentRuntimeState(
            "session",
            "match",
            1,
            "host",
            1,
            null,
            new[] { agent },
            null);

        Assert.NotNull(agent.Equipment);
        Assert.Empty(runtime.TeamScores);
        Assert.Empty(runtime.WorldItems);
    }
}
