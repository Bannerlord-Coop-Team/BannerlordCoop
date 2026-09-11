#if DEBUG
using Common.Messaging;
using Common.Network;
using E2E.Tests.Environment.Mock;
using GameInterface.Services.MapEvents;
using Missions;
using Missions.Messages;
using Missions.Battles;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public sealed class NavalLabRoutingTests : NavalMissionTestEnvironment
{
    public NavalLabRoutingTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Create_WaitsForBothReadyReceipts_AndPreservesWireIdentitiesOnRetry()
    {
        Guid operation = CreateLab();
        AssertNoHost(Server, Manifest.InstanceId);
        Assert.Empty(Actions(First));
        Assert.Empty(Actions(Second));
        Ready(Second);
        AssertHost(Server, Manifest.InstanceId, "naval-B");
        Tick(Second);
        Assert.False(Adapter(Second).Authority);
        Assert.Empty(Actions(First));
        Assert.Empty(Actions(Second));

        CampaignRouter.PauseLink(First.NetPeer, Server.NetPeer);
        Ready(First);
        Assert.Empty(Actions(First));
        Assert.Empty(Actions(Second));
        Assert.Throws<InvalidOperationException>(() => Execute("helm"));
        CampaignRouter.ResumeLink(First.NetPeer, Server.NetPeer);
        PumpAll();
        foreach (var client in Clients)
        {
            AssertHost(client, Manifest.InstanceId, "naval-B", "naval-A");
            Assert.Single(Actions(client), action => action.Kind == "release");
            var received = Assert.Single(client.InternalMessages.GetMessages<NetworkNavalLabStart>());
            Assert.Equal(Manifest.Ships, received.Ships);
            Assert.Equal(Manifest.Combatants, received.Combatants);
            Assert.Equal(Manifest.Controllers, received.Controllers);
            Assert.NotSame(Manifest, Adapter(client).OpenedManifest);
            Assert.Equal(Manifest.IncarnationId, Adapter(client).OpenedManifest!.IncarnationId);
        }
        Tick(Second);
        Tick(First);
        Assert.True(Adapter(Second).Authority);
        Assert.False(Adapter(First).Authority);
        AssertIdentityMirrors();

        Server.Call(() => Server.Resolve<INavalLabCoordinator>().Create(operation, "naval-A", "naval-B"));
        var start = Assert.Single(First.InternalMessages.GetMessages<NetworkNavalLabStart>());
        Server.Call(() => Server.Resolve<INetwork>().Send(First.NetPeer, start));
        PumpAll();
        Assert.Equal(1, Adapter(First).OpenCount);
        Assert.Equal(1, Adapter(Second).OpenCount);
        Assert.Single(Actions(First), action => action.Kind == "release");
        AssertIdentityMirrors();
    }

    [Theory]
    [InlineData("walk", 0, 1f)]
    [InlineData("turn", 1, -0.5f)]
    [InlineData("jump", 1, 0f)]
    [InlineData("crew", 0, 0f)]
    public void OwnerControls_TravelOnlyToOriginalOwner_AndDuplicateDoesNotApplyAgain(string kind, int ship, float value)
    {
        StartReleased();
        var owner = ship == 0 ? First : Second;
        var other = ship == 0 ? Second : First;
        Guid operation = Execute(kind, ship, value);
        Assert.Equal((kind, ship, value), Assert.Single(Adapter(owner).AgentControlCalls));
        Assert.Empty(Adapter(other).AgentControlCalls);
        Assert.Equal("applied", Receipt(owner, operation));
        Assert.DoesNotContain(Actions(other), action => action.OperationId == operation);
        Execute(kind, ship, value, operation: operation);
        SendAction(owner, Assert.Single(Actions(owner), action => action.OperationId == operation));
        Assert.Single(Adapter(owner).AgentControlCalls);
        Assert.Equal("applied", Receipt(owner, operation));

        var misrouted = new NetworkNavalLabAction(Manifest.IncarnationId, Guid.NewGuid(), 1, kind, ship, value, false);
        SendAction(other, misrouted);
        Assert.Equal("rejected:not_original_owner", Receipt(other, misrouted.OperationId));
        Assert.Empty(Adapter(other).AgentControlCalls);
    }

    [Fact]
    public void Helm_UsesElectedShipHost_NotShipOwner_AndRejectsMisrouting()
    {
        StartReleased();
        Guid operation = Execute("helm", 1, -0.4f, true);
        Assert.Equal((1, -0.4f, true), Assert.Single(Adapter(First).HelmCalls));
        Assert.Empty(Adapter(Second).HelmCalls);
        Assert.Equal("applied", Receipt(First, operation));
        Assert.DoesNotContain(Actions(Second), action => action.OperationId == operation);
        var wrong = new NetworkNavalLabAction(Manifest.IncarnationId, Guid.NewGuid(), 1, "helm", 1, 0.4f, true);
        SendAction(Second, wrong);
        Assert.Equal("rejected:not_ship_host", Receipt(Second, wrong.OperationId));
        Assert.Empty(Adapter(Second).HelmCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnerControl_RejectsChangedAuthorityOrWrongNativeAgent(bool wrongAgent)
    {
        StartReleased();
        First.Call(() =>
        {
            var registry = First.Resolve<INetworkAgentRegistry>();
            Assert.True(registry.TryGetAgentInfo(Manifest.Combatants[0], out var info));
            if (wrongAgent) Adapter(First).Agents[0] = Adapter(First).Agents[1];
            else Assert.True(registry.TryTransferAuthority("naval-B", Manifest.Combatants[0]));
        });
        Guid operation = Execute("walk", 0, 1);
        Assert.Equal("rejected:agent_authority_changed_or_unavailable", Receipt(First, operation));
        Assert.Empty(Adapter(First).AgentControlCalls);
        Assert.Empty(Adapter(Second).AgentControlCalls);
    }

    [Fact]
    public void HostFrames_UseMeshCopies_AndDroppedWrongInstanceStaleFramesNeverApply()
    {
        StartReleased();
        var host = Adapter(First);
        var follower = Adapter(Second);
        host.Frames[0] = new MatrixFrame(Mat3.Identity, new Vec3(11, 22, 33));
        Tick(First);
        Assert.Equal(1, follower.ApplyCount);
        Assert.Equal(11, follower.Frames[0].origin.x);
        Assert.Equal(0, host.ApplyCount);
        var wire = Assert.Single(First.Resolve<MockBattleNetwork>().NetworkSentMessages.GetMessages<NetworkNavalLabFrames>());
        Assert.NotSame(wire, Assert.Single(Second.InternalMessages.GetMessages<NetworkNavalLabFrames>()));
        host.Frames[0] = MatrixFrame.Identity;
        Assert.Equal(11, follower.Frames[0].origin.x);
        SendFrames(First, wire);
        SendFrames(First, new NetworkNavalLabFrames(Guid.NewGuid(), 1, 2, wire.Frames, 2));
        SendFrames(First, new NetworkNavalLabFrames(Manifest.IncarnationId, 2, 2, wire.Frames, 2));
        SendFrames(Second, new NetworkNavalLabFrames(Manifest.IncarnationId, 1, 2, wire.Frames, 2));
        Assert.Equal(1, follower.ApplyCount);
        Assert.Equal(0, host.ApplyCount);
        Assert.Equal(3, (long)Samples(Second)["rejectedFrames"]!);

        First.Resolve<MockBattleNetwork>().RouteMessages = false;
        Tick(First);
        Assert.Equal(1, follower.ApplyCount);
        First.Resolve<MockBattleNetwork>().RouteMessages = true;
        Second.Call(() => Second.Resolve<MockBattleNetwork>().ConnectToInstance("unrelated-mission"));
        Tick(First);
        Assert.Equal(1, follower.ApplyCount);
        Second.Call(() => Second.Resolve<MockBattleNetwork>().ConnectToInstance(Manifest.InstanceId));
        Tick(First);
        Assert.Equal(2, follower.ApplyCount);
        Assert.Equal(2, (long)Samples(Second)["receivedGaps"]!);
    }

    private void AssertIdentityMirrors()
    {
        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.Equal(10, Adapter(client).Mission.Agents.Count);
                var registry = client.Resolve<INetworkAgentRegistry>();
                for (int i = 0; i < Manifest.Combatants.Length; i++)
                {
                    Assert.True(registry.TryGetAgentInfo(Manifest.Combatants[i], out var info));
                    Assert.Equal(Manifest.Controllers[i / NavalLabManifest.CrewPerShip], info.OriginalOwner);
                    Assert.Equal(info.OriginalOwner, info.CurrentAuthority);
                    Assert.Equal(1, info.AuthorityRevision);
                    Assert.Equal(i + 1, info.MovementId);
                    Assert.Same(Adapter(client).Agents[i], info.Agent);
                    Assert.NotSame(Adapter(First).Agents[i], Adapter(Second).Agents[i]);
                }
            });
        }
    }
}
#endif
