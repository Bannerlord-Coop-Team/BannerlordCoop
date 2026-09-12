#if DEBUG
using Missions.Agents.Packets;
using Missions.Diagnostics;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace E2E.Tests.Services.Missions;

public class MissionActionDiagnosticsTests : IDisposable
{
    public MissionActionDiagnosticsTests()
    {
        MissionActionDiagnostics.StartPerformance();
    }

    public void Dispose()
    {
        MissionActionDiagnostics.SnapshotPerformance(stop: true);
    }

    [Fact]
    public void PacketObservations_RetainFormerHostEpochZeroAlongsideBothHostEpochs()
    {
        AgentActionPacket baseline = Packet("A", 1);
        AgentActionPacket successor = Packet("B", 2);
        AgentActionPacket returningPlayer = Packet("A", 0);
        foreach (AgentActionPacket packet in new[] { baseline, successor, returningPlayer })
        {
            MissionActionDiagnostics.RecordActionPacketSent(packet, 128);
            MissionActionDiagnostics.RecordActionPacketReceived(packet, 128);
        }
        MissionActionDiagnostics.RecordActionPacketReceived(returningPlayer, 128);

        JObject traffic = Traffic();
        var observations = Assert.IsType<JArray>(traffic["authorityObservations"]);
        Assert.Equal(new[] { "A", "B", "A" },
            observations.Select(row => row.Value<string>("controllerId")));
        Assert.Equal(new[] { 1, 2, 0 },
            observations.Select(row => row.Value<int>("battleHostEpoch")));
        Assert.Equal(new[] { 1L, 1L, 1L },
            observations.Select(row => row.Value<long>("sentPackets")));
        Assert.Equal(new[] { 1L, 1L, 2L },
            observations.Select(row => row.Value<long>("receivedPackets")));
        Assert.Equal(3, traffic.Value<long>("sentPackets"));
        Assert.Equal(6, traffic.Value<long>("sentUpdates"));
        Assert.Equal(384, traffic.Value<long>("sentSerializedBytes"));
        Assert.Equal(4, traffic.Value<long>("receivedPackets"));
        Assert.Equal(8, traffic.Value<long>("receivedUpdates"));
        Assert.Equal(512, traffic.Value<long>("receivedSerializedBytes"));
        Assert.Equal(4, observations[2].Value<long>("receivedUpdates"));
        Assert.Equal(256, observations[2].Value<long>("receivedSerializedBytes"));
        Assert.Equal(0, traffic.Value<long>("evictedAuthorityObservations"));
    }

    [Fact]
    public void StartPerformance_ClearsPreviousObservationWindowAndEvictions()
    {
        for (int epoch = 0; epoch < 70; epoch++)
            MissionActionDiagnostics.RecordActionPacketReceived(Packet("A", epoch), 128);
        MissionActionDiagnostics.RecordActionPacketSent(Packet("B", 2), 64);

        MissionActionDiagnostics.StartPerformance();

        JObject traffic = Traffic();
        Assert.Empty(Assert.IsType<JArray>(traffic["authorityObservations"]));
        Assert.Equal(0, traffic.Value<long>("sentPackets"));
        Assert.Equal(0, traffic.Value<long>("receivedPackets"));
        Assert.Equal(0, traffic.Value<long>("sentUpdates"));
        Assert.Equal(0, traffic.Value<long>("receivedUpdates"));
        Assert.Equal(0, traffic.Value<long>("sentSerializedBytes"));
        Assert.Equal(0, traffic.Value<long>("receivedSerializedBytes"));
        Assert.Equal(0, traffic.Value<long>("evictedAuthorityObservations"));

        MissionActionDiagnostics.RecordActionPacketReceived(Packet("A", 0), 32);
        JObject restartedTraffic = Traffic();
        JToken observation = Assert.Single(
            Assert.IsType<JArray>(restartedTraffic["authorityObservations"]));
        Assert.Equal(0, observation.Value<int>("battleHostEpoch"));
        Assert.Equal(1, observation.Value<long>("receivedPackets"));
    }

    [Fact]
    public void StopPerformance_FreezesPacketObservations()
    {
        MissionActionDiagnostics.RecordActionPacketReceived(Packet("A", 1), 128);
        JObject stopped = JObject.Parse(
            MissionActionDiagnostics.SnapshotPerformance(stop: true));

        MissionActionDiagnostics.RecordActionPacketSent(Packet("B", 2), 64);
        MissionActionDiagnostics.RecordActionPacketReceived(Packet("A", 0), 64);

        Assert.False(stopped.Value<bool>("enabled"));
        Assert.True(JToken.DeepEquals(stopped["actionTraffic"], Traffic()));
    }

    [Fact]
    public void PacketObservations_EvictOldestGroupsAtCapacityAndKeepTrafficTotals()
    {
        for (int epoch = 0; epoch < 70; epoch++)
            MissionActionDiagnostics.RecordActionPacketReceived(Packet("A", epoch), 128);
        MissionActionDiagnostics.RecordActionPacketSent(Packet("A", 69), 64);

        JObject traffic = Traffic();
        var observations = Assert.IsType<JArray>(traffic["authorityObservations"]);
        Assert.Equal(64, traffic.Value<int>("authorityObservationCapacity"));
        Assert.Equal(64, observations.Count);
        Assert.Equal(Enumerable.Range(6, 64),
            observations.Select(row => row.Value<int>("battleHostEpoch")));
        Assert.Equal(6, traffic.Value<long>("evictedAuthorityObservations"));
        Assert.Equal(70, traffic.Value<long>("receivedPackets"));
        Assert.Equal(1, observations.Last!.Value<long>("sentPackets"));
        Assert.Equal(1, observations.Last!.Value<long>("receivedPackets"));
    }

    [Fact]
    public void ConcurrentPacketObservations_KeepControllerAndEpochTogether()
    {
        AgentActionPacket[] packets = { Packet("A", 1), Packet("B", 2), Packet("A", 0) };
        Parallel.For(0, 600, index =>
        {
            AgentActionPacket packet = packets[index % packets.Length];
            MissionActionDiagnostics.RecordActionPacketSent(packet, 128);
            MissionActionDiagnostics.RecordActionPacketReceived(packet, 128);
            if (index % 29 == 0)
            {
                JObject snapshot = Traffic();
                var rows = Assert.IsType<JArray>(snapshot["authorityObservations"]);
                Assert.Equal(snapshot.Value<long>("sentPackets"),
                    rows.Sum(row => row.Value<long>("sentPackets")));
                Assert.Equal(snapshot.Value<long>("receivedPackets"),
                    rows.Sum(row => row.Value<long>("receivedPackets")));
                Assert.All(rows, row => Assert.Contains(
                    (row.Value<string>("controllerId"), row.Value<int>("battleHostEpoch")),
                    new[] { ("A", 1), ("B", 2), ("A", 0) }));
            }
        });

        JObject traffic = Traffic();
        var observations = Assert.IsType<JArray>(traffic["authorityObservations"]);
        Assert.Equal(3, observations.Count);
        Assert.Equal(600, traffic.Value<long>("sentPackets"));
        Assert.Equal(600, traffic.Value<long>("receivedPackets"));
        Assert.All(observations, row =>
        {
            Assert.Equal(200, row.Value<long>("sentPackets"));
            Assert.Equal(200, row.Value<long>("receivedPackets"));
        });
    }

    private static JObject Traffic() => Assert.IsType<JObject>(
        JObject.Parse(MissionActionDiagnostics.SnapshotPerformance(stop: false))["actionTraffic"]);

    private static AgentActionPacket Packet(string controllerId, int epoch) => new(
        controllerId,
        new[]
        {
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
        },
        new AgentActionData[2],
        new[] { 1L, 1L },
        epoch);
}
#endif
