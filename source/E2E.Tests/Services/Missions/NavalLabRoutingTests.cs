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

    private void StartNative()
    {
        CreateLab(NavalLabMode.TwoClientNative);
        foreach (var client in Clients) Adapter(client).CaptureShipSamples = false;
        Ready(First); Ready(Second); Tick(First); Tick(Second);
        Execute("complete-deployment"); Tick(First); Tick(Second);
    }

    [Fact]
    public void ServerHoldsSamplesUntilBothClientsAcknowledgeStations()
    {
        CreateLab(NavalLabMode.TwoClientNative);
        foreach (var client in Clients) Adapter(client).CaptureShipSamples = false;
        Ready(First); Ready(Second); Tick(First); Tick(Second);
        SendShipSample(First, Adapter(First).ShipSample(1, 10, MatrixFrame.Identity));
        Assert.Empty(Second.InternalMessages.GetMessages<NetworkNavalLabShipSample>());
        Execute("complete-deployment"); Tick(First);
        SendShipSample(First, Adapter(First).ShipSample(1, 10, MatrixFrame.Identity));
        Assert.Empty(Second.InternalMessages.GetMessages<NetworkNavalLabShipSample>());
        Tick(Second);
        SendShipSample(First, Adapter(First).ShipSample(1, 10, MatrixFrame.Identity));
        Assert.Single(Second.InternalMessages.GetMessages<NetworkNavalLabShipSample>());
        Assert.Equal(1, (long)ShipStream(Second, 0)["acceptedSequence"]!);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void OwnerSamplesUseSerializedServerForwarding_AndNeverReturnToOwnedHull(int slot)
    {
        StartNative();
        var owner = slot == 0 ? First : Second;
        var other = slot == 0 ? Second : First;
        Adapter(owner).Frames[slot] = new MatrixFrame(Mat3.Identity, new Vec3(11, 22, 33));
        Adapter(owner).CaptureShipSamples = true;
        Tick(owner);
        var sent = Assert.Single(Adapter(owner).CapturedShips);
        Assert.Equal(slot, sent.Slot);
        Assert.Equal(1, sent.Sequence);
        var received = Assert.Single(other.InternalMessages.GetMessages<NetworkNavalLabShipSample>());
        Assert.NotSame(sent, received);
        Assert.NotSame(sent.Frame, received.Frame);
        Assert.Equal(sent.Frame, received.Frame);
        Assert.Equal(Manifest.Controllers[slot], received.OriginalOwner);
        Assert.Equal(Manifest.Ships[slot], received.ShipId);
        Assert.Single(Server.InternalMessages.GetMessages<NetworkNavalLabShipSample>());
        Assert.Empty(owner.InternalMessages.GetMessages<NetworkNavalLabShipSample>());
        Assert.Empty(owner.Resolve<MockBattleNetwork>().NetworkSentMessages.GetMessages<NetworkNavalLabShipSample>());
        Assert.Empty(Adapter(other).ForeignFrameWrites);
        Tick(other);
        Assert.Equal(slot, Assert.Single(Adapter(other).ForeignFrameWrites).slot);
        Assert.Equal(11, Adapter(other).Frames[slot].origin.x);
        Assert.Empty(Adapter(owner).ForeignFrameWrites);
        Assert.Equal("owner", (string?)ShipStream(owner, slot)["localRole"]);
        Assert.Equal("foreign", (string?)ShipStream(other, slot)["localRole"]);
        Assert.Equal(0, (long)ShipStream(owner, slot)["appliedSequence"]!);
        Assert.Equal(1, (long)ShipStream(other, slot)["appliedSequence"]!);
        Tick(owner);
        Assert.Equal(new long[] { 1, 2 }, other.InternalMessages.GetMessages<NetworkNavalLabShipSample>().Select(sample => sample.Sequence));
        Assert.Equal(2, (long)ShipStream(other, slot)["acceptedSequence"]!);
        Assert.Equal(1, (long)ShipStream(other, slot)["appliedSequence"]!);
    }

    [Theory]
    [InlineData("sender")]
    [InlineData("instance")]
    [InlineData("incarnation")]
    [InlineData("ship")]
    [InlineData("owner")]
    [InlineData("revision")]
    [InlineData("duplicate")]
    [InlineData("older")]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("frame")]
    [InlineData("inventory")]
    public void ServerRejectsInvalidForeignSamplesWithoutAdvancingStream(string kind)
    {
        StartNative();
        SendShipSample(First, Adapter(First).ShipSample(10, 100, MatrixFrame.Identity));
        var invalid = Adapter(First).ShipSample(11, 110, MatrixFrame.Identity);
        // Mutate one serialized field at a time, including readonly wire fields without alternate constructors.
        void Field(string name, object value) => typeof(NetworkNavalLabShipSample).GetField(name)!.SetValue(invalid, value);
        if (kind == "instance") Field("InstanceId", "different-instance");
        if (kind == "incarnation") Field("IncarnationId", Guid.NewGuid());
        if (kind == "ship") Field("ShipId", Manifest.Ships[1]);
        if (kind == "owner") Field("OriginalOwner", "naval-B");
        if (kind == "revision") Field("AuthorityRevision", 2);
        if (kind == "duplicate") Field("Sequence", 10L);
        if (kind == "older") Field("Sequence", 9L);
        if (kind == "expired") Field("DeadlineUtcTicks", DateTime.UtcNow.AddSeconds(-1).Ticks);
        if (kind == "future") Field("DeadlineUtcTicks", DateTime.UtcNow.AddMinutes(1).Ticks);
        if (kind == "frame") invalid.Frame[9] = float.NaN;
        if (kind == "inventory")
        {
            var presentation = invalid.Presentation;
            Field("Presentation", new NetworkNavalLabPresentation(invalid.ShipId,
                new[] { new NetworkNavalLabSailPresentation("uncommitted-sail", 0, 0, 0, 0, true, false, false, 0, 0) },
                presentation.Oars, presentation.Sides));
            Assert.True(invalid.IsValid);
        }
        SendShipSample(kind == "sender" ? Second : First, invalid);
        Assert.Single(Second.InternalMessages.GetMessages<NetworkNavalLabShipSample>());
        Assert.Single(Adapter(Second).AcceptedShips);
        Assert.Equal(10, (long)ShipStream(Second, 0)["acceptedSequence"]!);
        Assert.Empty(Adapter(First).AcceptedShips);
        SendShipSample(First, Adapter(First).ShipSample(11, 111, MatrixFrame.Identity));
        Assert.Equal(new long[] { 10, 11 }, Second.InternalMessages.GetMessages<NetworkNavalLabShipSample>().Select(sample => sample.Sequence));
        Assert.Equal(11, (long)ShipStream(Second, 0)["acceptedSequence"]!);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void IncomingOwnedSampleIsRejectedBeforeNativeValidationOrWrite(int slot)
    {
        StartNative();
        var owner = slot == 0 ? First : Second;
        var sample = Adapter(owner).ShipSample(100, 1000, new MatrixFrame(Mat3.Identity, new Vec3(50, 0, 0)));
        Server.Call(() => Server.Resolve<INetwork>().Send(owner.NetPeer, sample));
        PumpAll(); Tick(owner);
        Assert.Empty(Adapter(owner).AcceptedShips);
        Assert.Empty(Adapter(owner).ForeignFrameWrites);
        Assert.Equal(1, (long)ShipStream(owner, slot)["OwnerWriteRejects"]!);
        Assert.Equal("owner_incoming_write", (string?)ShipStream(owner, slot)["LastReject"]);
        Assert.Equal(0, Adapter(owner).Frames[slot].origin.x);
    }

    [Theory]
    [InlineData("departure")]
    [InlineData("epoch")]
    public void DepartureOrEpochChangeHoldsBothStreams_AndRejectsQueuedAndLaterSamples(string kind)
    {
        StartNative();
        SendShipSample(First, Adapter(First).ShipSample(10, 100, new MatrixFrame(Mat3.Identity, new Vec3(10, 0, 0))));
        SendShipSample(Second, Adapter(Second).ShipSample(10, 100, new MatrixFrame(Mat3.Identity, new Vec3(20, 0, 0))));
        Tick(First, 0.01f); Tick(Second, 0.01f);
        var manifest = Manifest;
        First.Call(() => First.Resolve<INetwork>().SendAll(Adapter(First).ShipSample(11, 110, MatrixFrame.Identity)));
        Second.Call(() => Second.Resolve<INetwork>().SendAll(Adapter(Second).ShipSample(11, 110, MatrixFrame.Identity)));
        if (kind == "departure")
        {
            First.Call(() => First.Resolve<INetwork>().SendAll(new NetworkMissionLeft("naval-A", manifest.InstanceId)));
            Server.PumpGameThread();
        }
        else
            foreach (var instance in Clients.Append(Server))
                instance.Call(() => instance.Resolve<IBattleHostRegistry>().Set(manifest.InstanceId,
                    new BattleHostAssignment("naval-A", new[] { "naval-B" }, 2)));
        PumpAll(); Tick(First); Tick(Second);
        foreach (var client in Clients)
        {
            Assert.True(Adapter(client).TerminalHold);
            Assert.False(Adapter(client).InputAuthority!());
            int foreign = client == First ? 1 : 0;
            Assert.Single(Adapter(client).ForeignFrameWrites);
            Assert.Equal(10, (long)ShipStream(client, foreign)["acceptedSequence"]!);
            Assert.Equal(10, (long)ShipStream(client, foreign)["appliedSequence"]!);
            Assert.Equal(Newtonsoft.Json.Linq.JTokenType.Null, ShipStream(client, foreign)["targetFrame"]!.Type);
        }
        var firstReceived = First.InternalMessages.GetMessages<NetworkNavalLabShipSample>().Count();
        var secondReceived = Second.InternalMessages.GetMessages<NetworkNavalLabShipSample>().Count();
        SendShipSample(First, Adapter(First).ShipSample(12, 120, MatrixFrame.Identity));
        SendShipSample(Second, Adapter(Second).ShipSample(12, 120, MatrixFrame.Identity));
        Tick(First); Tick(Second);
        Assert.Equal(firstReceived, First.InternalMessages.GetMessages<NetworkNavalLabShipSample>().Count());
        Assert.Equal(secondReceived, Second.InternalMessages.GetMessages<NetworkNavalLabShipSample>().Count());
        Assert.All(Clients, client => Assert.Single(Adapter(client).ForeignFrameWrites));
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
