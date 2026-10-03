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
}
#endif
