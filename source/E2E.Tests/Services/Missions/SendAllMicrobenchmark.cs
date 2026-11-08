using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text;
using Common.Messaging;
using Common.Network;
using Common.Network.Data;
using Common.Network.Session;
using Common.PacketHandlers;
using Common.Serialization;
using GameInterface.Services.Entity;
using GameInterface.Surrogates;
using LiteNetLib;
using Missions.Agents.Messages;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Services.Missions;

// TEMPORARY (#4495 measurement rule): before/after microbenchmark of the mission broadcast send path.
// Runs only when COOP_SENDALL_BENCH_OUT is set; removed before the PR leaves draft.
public class SendAllMicrobenchmark
{
    private const int OpsPerSample = 8;
    private const int WarmupOps = 20_000;
    private const int Samples = 3_000;

    public SendAllMicrobenchmark()
    {
        _ = new SurrogateCollection();
    }

    [Fact]
    public void Run()
    {
        string outPath = System.Environment.GetEnvironmentVariable("COOP_SENDALL_BENCH_OUT");
        if (string.IsNullOrEmpty(outPath)) return;
        string side = System.Environment.GetEnvironmentVariable("COOP_SENDALL_BENCH_SIDE") ?? "unknown";

        var plain = new ProtoBufSerializer(new SerializableTypeMapper());
        var kinds = new (string Name, Func<int, IMessage> Create)[]
        {
            ("AgentVoicePlayed", CreateVoice),
            ("BattleAgentDied", CreateDeath),
            ("MeleeHitPresentation", CreateMeleeHit),
            ("ApplyBattleDamage", CreateDamage),
        };

        var csv = new StringBuilder();
        csv.AppendLine("side,op,peers,message,payload_bytes,serialize_per_call,ns_median,ns_p95,ns_mean,alloc_median,alloc_p95,relay_sends");
        foreach (int peers in new[] { 2, 4, 8 })
        {
            foreach (var kind in kinds)
            {
                csv.AppendLine(Measure(side, "SendAll", peers, kind.Name, kind.Create, plain));
            }
            csv.AppendLine(Measure(side, "SendAllBut", peers, "MeleeHitPresentation", CreateMeleeHit, plain));
        }

        File.WriteAllText(outPath, csv.ToString());
    }

    private static string Measure(string side, string op, int peers, string name, Func<int, IMessage> create, ICommonSerializer plain)
    {
        string[] controllers = Enumerable.Range(1, peers).Select(i => $"peer-{i}").ToArray();
        IMessage[] messages = Enumerable.Range(1, 64).Select(create).ToArray();
        int payloadBytes = MessagePacket.Create(messages[0], plain).Data.Length;

        var counting = new CountingSerializer(plain);
        var relay = new StubRelayNetwork();
        var config = new Mock<INetworkConfig>();
        config.SetupGet(value => value.IsTunneled).Returns(true);
        using var client = new LiteNetP2PClient(
            config.Object,
            relay,
            new StubMissionContext(controllers),
            counting,
            Mock.Of<IMessageBroker>(),
            Mock.Of<IPacketManager>(),
            Mock.Of<IMessagePacketHandler>(),
            Mock.Of<IControllerIdProvider>(),
            Mock.Of<ISteamMissionBridge>(),
            new MovementPacketCompressor(counting),
            new ReliableMessageBatcher<string>(plain),
            () => new Common.Logging.ReceivePathDiagnostics());
        client.ConnectToInstance("microbench");

        int next = 0;
        void SendOne()
        {
            IMessage message = messages[next++ & 63];
            if (op == "SendAll") client.SendAll(message);
            else client.SendAllBut(controllers[0], message);
        }

        for (int i = 0; i < WarmupOps; i++)
        {
            SendOne();
            if (i % OpsPerSample == OpsPerSample - 1) client.FlushPendingMessages();
        }
        client.FlushPendingMessages();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        counting.SerializeCalls = 0;
        relay.Sends = 0;
        var ns = new double[Samples];
        var alloc = new double[Samples];
        double tickNs = 1e9 / Stopwatch.Frequency;
        for (int s = 0; s < Samples; s++)
        {
            long a0 = GC.GetAllocatedBytesForCurrentThread();
            long t0 = Stopwatch.GetTimestamp();
            for (int i = 0; i < OpsPerSample; i++) SendOne();
            long t1 = Stopwatch.GetTimestamp();
            long a1 = GC.GetAllocatedBytesForCurrentThread();
            // One flush per sample stands in for the per-tick flush and stays outside the timed region.
            client.FlushPendingMessages();
            ns[s] = (t1 - t0) * tickNs / OpsPerSample;
            alloc[s] = (double)(a1 - a0) / OpsPerSample;
        }

        double serializePerCall = (double)counting.SerializeCalls / (Samples * OpsPerSample);
        return string.Join(",",
            side, op, peers, name, payloadBytes,
            serializePerCall.ToString("0.###"),
            Percentile(ns, 0.5).ToString("0"),
            Percentile(ns, 0.95).ToString("0"),
            ns.Average().ToString("0"),
            Percentile(alloc, 0.5).ToString("0"),
            Percentile(alloc, 0.95).ToString("0"),
            relay.Sends);
    }

    private static double Percentile(double[] values, double p)
    {
        double[] sorted = values.OrderBy(v => v).ToArray();
        return sorted[(int)Math.Min(sorted.Length - 1, Math.Floor(p * sorted.Length))];
    }

    private static IMessage CreateVoice(int i) => new NetworkAgentVoicePlayed(Id(i), "grunt", null);

    private static IMessage CreateDeath(int i) =>
        new NetworkBattleAgentDied(Id(i), wounded: false, Id(i + 1000), inflictedDamage: 37 + i, BoneBodyPartType.Head, deathAction: 12);

    private static IMessage CreateMeleeHit(int i) => new NetworkMeleeHitPresentation(
        Id(i), isMount: false, MeleeHitPresentationKind.Blood, collisionBoneIndex: 13,
        new Vec3(412.25f + i, 318.5f, 7.75f), WeaponClass.OneHandedSword, physicsMaterialIndex: 4,
        strength: 38.5f, soundIndex: 211, armorType: 2f);

    private static IMessage CreateDamage(int i)
    {
        object blow = default(Blow);
        Fill(blow, i);
        object collision = default(AttackCollisionData);
        Fill(collision, i);
        var data = new BattleDamageData(new BattleBlowData((Blow)blow), new BattleCollisionData((AttackCollisionData)collision));
        return new NetworkApplyBattleDamage(Id(i), Id(i + 2000), data, isMissile: false, isMount: false, 0, null, "controller-host");
    }

    // Gives every numeric and vector field a non-default value so the serialized size matches a real blow.
    private static void Fill(object boxed, int seed)
    {
        foreach (FieldInfo field in boxed.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            Type type = field.FieldType;
            if (type == typeof(float)) field.SetValue(boxed, 1.375f + seed);
            else if (type == typeof(int)) field.SetValue(boxed, 17 + seed);
            else if (type == typeof(sbyte)) field.SetValue(boxed, (sbyte)5);
            else if (type == typeof(bool)) field.SetValue(boxed, true);
            else if (type == typeof(Vec3)) field.SetValue(boxed, new Vec3(412.25f + seed, -318.5f, 7.75f));
            else if (type.IsValueType && !type.IsEnum && !type.IsPrimitive && type.Namespace?.StartsWith("TaleWorlds") == true)
            {
                object nested = field.GetValue(boxed);
                Fill(nested, seed);
                field.SetValue(boxed, nested);
            }
        }
    }

    private static Guid Id(int i) => new(i, 7, 9, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

    private sealed class CountingSerializer : ICommonSerializer
    {
        private readonly ICommonSerializer inner;

        public CountingSerializer(ICommonSerializer inner)
        {
            this.inner = inner;
        }

        public long SerializeCalls { get; set; }

        public T Deserialize<T>(byte[] data) => inner.Deserialize<T>(data);

        public object Deserialize(byte[] data) => inner.Deserialize(data);

        public byte[] Serialize(object obj)
        {
            SerializeCalls++;
            return inner.Serialize(obj);
        }
    }

    private sealed class StubMissionContext : IMissionContext
    {
        private readonly string[] controllers;

        public StubMissionContext(string[] controllers)
        {
            this.controllers = controllers;
        }

        public IReadOnlyCollection<string> ControllersInMission => controllers;

        public void MapPeer(string controllerId, NetPeer peer) { }

        public void RemovePeer(NetPeer peer) { }

        public bool TryGetPeer(string controllerId, out NetPeer netPeer)
        {
            netPeer = null;
            return false;
        }

        public void EndInstance() { }

        public void Dispose() { }
    }

    private sealed class StubRelayNetwork : IRelayNetwork
    {
        public long Sends;

        public IPEndPoint ServerEndpoint => null;

        public INetworkConfig Config => null;

        public void Send(NetPeer netPeer, IPacket packet) { }

        public void SendImmediate(NetPeer netPeer, IPacket packet) { }

        public void SendAll(IPacket packet) => Sends++;

        public void SendAllBut(NetPeer excludedPeer, IPacket packet) { }

        public void Send(NetPeer netPeer, IMessage message) { }

        public void SendImmediate(NetPeer netPeer, IMessage message) { }

        public void SendAll(IMessage message) { }

        public void SendAllBut(NetPeer excludedPeer, IMessage message) { }

        public void FlushPendingMessages() { }

        public void Start() { }

        public void Dispose() { }
    }
}
