using Common;
using Common.Messaging;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.Players;
using HarmonyLib;
using Missions.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>
/// The naval end flow through the server: the battle host's result reaches its peers over the mesh and the server applies
/// it once every member reported, also when the host leaves before that. A host leaving a battle it resolved keeps the
/// naval assignment, while a land battle still promotes (M2 K23, R41a).
/// </summary>
public class NavalBattleEndFlowScenarioTests : NavalBattleTestEnvironment
{
    public NavalBattleEndFlowScenarioTests(ITestOutputHelper output) : base(output, numClients: 2) { }

    [Fact]
    public void HostWin_ReachesThePeers_AndTheServerConcludesOnce()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out var b, out var concluded);
        battle.CommitAll();

        ReportWin(a);

        Assert.True(b.Controller.ResultCommitter.TryGetResolvedState(out var peerState));
        Assert.Equal(BattleState.AttackerVictory, peerState);
        Assert.Equal(1, concluded());
        AssertMapEventRemoved(battle.MapEventId);

        b.Call(() => b.Controller.ResultCommitter.ReportAcceptedResult());
        Assert.Equal(1, concluded());
    }

    // R41a D: the host's reported result keeps it the naval host after it leaves, so the peer's later report concludes the
    // battle with it instead of a promotion that waits on the peer as the new host.
    [Fact]
    public void HostLeavesMidEnd_KeepsTheAssignment_AndThePeersReportConcludesOnce()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out var b, out var concluded);
        var pirate = battle.FieldAiHull(a, BattleSideEnum.Defender, new Vec3(0f, 300f, 0f));
        a.Commit();
        battle.TickAll();

        ReportWin(a);
        Assert.Equal(0, concluded());
        LeaveMission(battle, a);

        AssertHost(Server, battle.MapEventId, "A", "B");
        AssertHost(b.Instance, battle.MapEventId, "A", "B");
        Assert.Equal(0, concluded());
        // B holds the result, so its naval mission takes over no AI hull from the departed host.
        Assert.Equal("A", b.Ship(pirate).CurrentAuthority);
        b.Ticks(3);
        Assert.True(b.Hull(pirate).IsCopy);
        Assert.Empty(b.Engine.Boardings);

        b.Call(() =>
        {
            b.Controller.Deployment.OnLocalDeploymentFinished();
            b.Controller.ResultCommitter.ReportAcceptedResult();
        }, VictoryConclusionDisabledMethods());
        Assert.Equal(1, concluded());
        AssertMapEventRemoved(battle.MapEventId);
    }

    // R41a D: the host resolves and leaves first while a member never reports (stuck in deployment). The host's finalize
    // waits for the server, and the member leaving empties the instance, which concludes with the host's result.
    [Fact]
    public void HostLeavesFirst_AndAMemberNeverReports_TheBattleConcludesOnceTheMemberLeaves()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out var b, out var concluded);
        a.Commit();
        battle.TickAll();

        ReportWin(a);
        LeaveMission(battle, a);
        Server.Call(() => Server.SimulateMessage(a.Instance.NetPeer, new NetworkMapEventFinalizeAttempted(battle.MapEventId)));

        Server.Call(() => Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(battle.MapEventId, out var mapEvent)
            && !mapEvent.IsFinalized, "the departed host's finalize ended the battle before the server concluded it"));
        Assert.Equal(0, concluded());

        LeaveMission(battle, b);
        Assert.Equal(1, concluded());
        AssertMapEventRemoved(battle.MapEventId);
    }

    // R41a fix-loop C1: the kept naval host is still in the map event at its encounter. Its own Attack is refused, and another
    // member's Attack neither pulls it back into the mission nor holds the conclusion for it as a joiner.
    [Fact]
    public void KeptHost_CannotEnterAgain_AndAnotherMembersAttackLeavesItOut()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out var b, out var concluded);
        a.Commit();
        battle.TickAll();
        ReportWin(a);
        LeaveMission(battle, a);
        Server.Call(() => Server.Resolve<BattleMissionStartHandler>().GetOrCreateMissionInitializerSnapshot(battle.MapEventId,
            () => new MissionInitializerRecord("test-naval")));
        int startsToA = a.Instance.InternalMessages.GetMessages<NetworkStartAttackMission>().Count();

        bool navalDlcActive = ModInformation.IsNavalDlcActive;
        try
        {
            ModInformation.IsNavalDlcActive = true;
            RequestAttack(battle, a, "kept-host-attack");
            RequestAttack(battle, b, "member-attack");
        }
        finally
        {
            ModInformation.IsNavalDlcActive = navalDlcActive;
        }

        var replies = Server.NetworkSentMessages.GetMessages<NetworkBattleStartReply>().ToArray();
        Assert.False(replies.Single(reply => reply.RequestId == "kept-host-attack").Accepted);
        Assert.True(replies.Single(reply => reply.RequestId == "member-attack").Accepted);
        Assert.Equal(startsToA, a.Instance.InternalMessages.GetMessages<NetworkStartAttackMission>().Count());
        AssertHost(Server, battle.MapEventId, "A", "B");
        Assert.Equal(0, concluded());

        b.Call(() =>
        {
            b.Controller.Deployment.OnLocalDeploymentFinished();
            b.Controller.ResultCommitter.ReportAcceptedResult();
        }, VictoryConclusionDisabledMethods());
        Assert.Equal(1, concluded());
        AssertMapEventRemoved(battle.MapEventId);
    }

    // The player presses Attack at the battle's encounter menu.
    private void RequestAttack(NavalBattle battle, NavalPeer player, string requestId)
    {
        // The headless manager uses Default without loading the engine's BannerlordConfig.
        var spawnSettings = new[] { AccessTools.PropertyGetter(typeof(MBGameManager), nameof(MBGameManager.UnitSpawnPrioritization)) };
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(player.Id, out var registered));
            Server.SimulateMessage(player.Instance.NetPeer, new NetworkBattleStartRequest(requestId, (int)BattleStartMode.Mission,
                battle.MapEventId, registered.MobilePartyId));
        }, spawnSettings);
    }

    // The member's mission ends and the server observes it leave.
    private void LeaveMission(NavalBattle battle, NavalPeer leaver)
    {
        leaver.Call(leaver.Network.Stop);
        Server.Call(() => Server.SimulateMessage(leaver.Instance.NetPeer, new NetworkMissionLeft(leaver.Id, battle.MapEventId)),
            VictoryConclusionDisabledMethods());
    }

    [Theory]
    [InlineData(true, "A", "B")]
    [InlineData(false, "B", null)]
    public void HostLeavesAfterTheConclusion_KeepsTheNavalAssignment_ButALandBattlePromotes(bool atSea, string expectedHost,
        string? expectedSuccessor)
    {
        using var battle = StartServerBattle(atSea, out var a, out _, out var concluded);
        battle.CommitAll();
        ReportWin(a);
        Assert.Equal(1, concluded());

        DepartBattle("A", battle.MapEventId);

        var successors = expectedSuccessor == null ? Array.Empty<string>() : new[] { expectedSuccessor };
        AssertHost(Server, battle.MapEventId, expectedHost, successors);
        foreach (var peer in battle.Peers)
            AssertHost(peer.Instance, battle.MapEventId, expectedHost, successors);
    }

    // coop.debug.naval.win on the host: its end logic resolves the battle and the committer reports it.
    private void ReportWin(NavalPeer host) => ResolveOnHost(host, BattleState.AttackerVictory);
}
