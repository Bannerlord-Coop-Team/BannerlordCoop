using Common.Messaging;
using GameInterface.Utils;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Moq;
using ProtoBuf;
using System.IO;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class BattleWaterClockTests
{
    private readonly Mock<IBattleNetwork> network = new();
    private readonly Mock<IMessageBroker> messageBroker = new();
    private readonly Mock<IBattleSession> session = new();
    private readonly FakeWaterClock waterClock = new();
    private readonly BattleWaterClock clock;

    public BattleWaterClockTests()
    {
        session.SetupGet(s => s.HostEpoch).Returns(2);
        session.SetupGet(s => s.InstanceId).Returns("battle");
        clock = new BattleWaterClock(network.Object, messageBroker.Object, session.Object, new HostEpochPolicy(), waterClock,
            new BattleWaterTimeEstimator());
    }

    [Fact]
    public void WithoutThePatch_TickAndSamplesDoNothing()
    {
        waterClock.Installed = false;
        session.SetupGet(s => s.IsLocalHost).Returns(true);

        clock.Tick(5f);
        clock.AcceptHostTime(new NetworkBattleWaterTime(5000f, 2));
        clock.SendTimeTo("peer");
        clock.Dispose();

        Assert.Equal(0, waterClock.Begins);
        Assert.Equal(0, waterClock.Writes);
        Assert.Equal(0, waterClock.Ends);
        Assert.Equal(0, clock.SamplesAccepted);
        network.VerifyNoOtherCalls();
    }

    [Fact]
    public void FirstTick_WithoutHostTime_StartsFromTheVanillaClock()
    {
        waterClock.Vanilla = 1234.5f;

        clock.Tick(0.016f);

        Assert.True(waterClock.IsOverriding);
        Assert.Equal(1234.5f, waterClock.BeganAt);
        Assert.Equal(1234.5f, waterClock.WrittenTime);
    }

    [Fact]
    public void Client_WritesTheAdoptedHostTime()
    {
        waterClock.Vanilla = 1000f;
        clock.Tick(0.016f);

        clock.AcceptHostTime(new NetworkBattleWaterTime(5000f, 2));
        waterClock.Vanilla = 1000.25f;
        clock.Tick(0.016f);

        Assert.Equal(1, clock.SamplesAccepted);
        Assert.Equal(5000.25f, waterClock.WrittenTime);
    }

    [Fact]
    public void Client_DropsASupersededHostsTime()
    {
        waterClock.Vanilla = 1000f;

        clock.AcceptHostTime(new NetworkBattleWaterTime(5000f, 1));

        Assert.Equal(0, clock.SamplesAccepted);
        Assert.False(clock.Estimator.HasHostTime);
    }

    [Fact]
    public void Client_DropsANonFiniteHostTime()
    {
        clock.AcceptHostTime(new NetworkBattleWaterTime(float.NaN, 2));
        clock.AcceptHostTime(new NetworkBattleWaterTime(float.PositiveInfinity, 2));

        Assert.Equal(0, clock.SamplesAccepted);
    }

    [Fact]
    public void AFailedRedirect_IsNotRetriedEveryFrame()
    {
        waterClock.RefuseOverride = true;

        clock.Tick(0.016f);
        clock.Tick(0.016f);

        Assert.Equal(1, waterClock.Begins);
        Assert.Equal(0, waterClock.Writes);
        network.VerifyNoOtherCalls();
    }

    [Fact]
    public void Host_IgnoresTimeSamplesAndBroadcastsItsOwnEverySecond()
    {
        session.SetupGet(s => s.IsLocalHost).Returns(true);
        waterClock.Vanilla = 1000f;

        clock.AcceptHostTime(new NetworkBattleWaterTime(5000f, 2));
        clock.Tick(0.5f);
        waterClock.Vanilla = 1004f;
        clock.Tick(0.4f);
        waterClock.Vanilla = 1008f;
        clock.Tick(0.4f);

        Assert.Equal(0, clock.SamplesAccepted);
        network.Verify(n => n.SendAll(It.Is<NetworkBattleWaterTime>(m => m.WaterTime == 1000f && m.HostEpoch == 2)), Times.Once);
        network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Once);
    }

    [Fact]
    public void Host_SendsItsTimeToAnEnteringPeer()
    {
        session.SetupGet(s => s.IsLocalHost).Returns(true);
        waterClock.Vanilla = 1000f;
        clock.Tick(0.016f);

        clock.SendTimeTo("peer");

        network.Verify(n => n.Send("peer", It.Is<NetworkBattleWaterTime>(m => m.WaterTime == 1000f && m.HostEpoch == 2)), Times.Once);
    }

    [Fact]
    public void Dispose_GivesTheEngineItsClockBackOnce()
    {
        clock.Tick(0.016f);

        clock.Dispose();
        clock.Dispose();

        Assert.Equal(1, waterClock.Ends);
        Assert.False(waterClock.IsOverriding);
    }

    [Fact]
    public void Dispose_BeforeAnyTick_LeavesTheEngineClockAlone()
    {
        clock.Dispose();

        Assert.Equal(0, waterClock.Ends);
    }

    [Fact]
    public void Message_SurvivesTheWire()
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, new NetworkBattleWaterTime(4321.25f, 3));
        stream.Position = 0;

        var copy = Serializer.Deserialize<NetworkBattleWaterTime>(stream);

        Assert.Equal(4321.25f, copy.WaterTime);
        Assert.Equal(3, copy.HostEpoch);
    }

#if DEBUG
    [Fact]
    public void DebugLine_ReportsPatchVanillaAndOffset()
    {
        waterClock.Vanilla = 1000f;
        clock.AcceptHostTime(new NetworkBattleWaterTime(1002.5f, 2));
        clock.Tick(0.016f);

        string line = global::Missions.Naval.NavalWaterClockCoopCommand.Describe(waterClock, clock);

        Assert.StartsWith("NAVAL_WATER_CLOCK installed=true overriding=true", line);
        Assert.Contains("written=1002.500 vanilla=1000.000 offset=2.500", line);
        Assert.Contains("battle=naval host=false hostTime=true samples=1", line);
    }
#endif

    /// <summary>An installed water clock in memory, counting what the battle clock asks of it.</summary>
    private sealed class FakeWaterClock : IWaterClock
    {
        public bool Installed = true;
        public bool RefuseOverride;
        public float Vanilla;
        public float BeganAt;
        public int Begins;
        public int Writes;
        public int Ends;

        public bool IsInstalled => Installed;

        public bool IsOverriding { get; private set; }

        public string Status => Installed ? "installed" : "disabled: test";

        public float WrittenTime { get; private set; }

        public bool TryInstall() => Installed;

        public bool TryReadVanillaTime(int timeType, out float waterTime)
        {
            waterTime = Vanilla;
            return Installed && timeType == WaterClock.MissionTimeType;
        }

        public bool TryBeginOverride(float waterTime)
        {
            if (!Installed) return false;
            Begins++;
            if (RefuseOverride) return false;
            BeganAt = waterTime;
            WrittenTime = waterTime;
            IsOverriding = true;
            return true;
        }

        public void SetWaterTime(float waterTime)
        {
            if (!Installed) return;
            Writes++;
            WrittenTime = waterTime;
        }

        public void EndOverride()
        {
            if (!IsOverriding) return;
            Ends++;
            IsOverriding = false;
        }
    }
}
