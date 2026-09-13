using Common;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Common.Serialization;
using Common.Tests.Utils;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Services.ItemRosters.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.TroopRosters.Messages;
using GameInterface.Services.Workshops.Messages;
using LiteNetLib;
using Moq;
using ProtoBuf;
using System.Diagnostics;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.TroopRosters;

/// <summary>Compares original and compacted replay against native item and troop roster behavior.</summary>
public class JoinRosterReplayTests : SyncTestBase
{
    private readonly string items;
    private readonly string item;
    private readonly string otherItem;
    private readonly string troops;
    private readonly string character;

    public JoinRosterReplayTests(ITestOutputHelper output) : base(output)
    {
        items = "join-replay-items";
        Server.CreateRegisteredObject<ItemRoster>(items);
        foreach (var client in Clients)
            client.CreateRegisteredObject<ItemRoster>(items);
        item = TestEnvironment.CreateRegisteredObject<ItemObject>();
        otherItem = TestEnvironment.CreateRegisteredObject<ItemObject>();
        troops = TestEnvironment.CreateRegisteredObject<TroopRoster>();
        character = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        foreach (var client in Clients)
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<ItemObject>(item, out var food));
                food.IsFood = true;
                food.Value = 23;
                food.ItemType = ItemObject.ItemTypeEnum.Goods;
            });
    }

    [ProtoContract]
    public readonly struct ObserveRosters : ICommand { }

    [Fact]
    public void RetainedReplay_MatchesOriginalAtEveryConsumerAndAfterClearRepopulate()
    {
        var serializer = Server.Resolve<ICommonSerializer>();
        var messages = new List<IMessage>();
        for (int i = 0; i < 64; i++) messages.Add(Item(1));
        messages.Add(Item(2, otherItem));
        messages.Add(new ObserveRosters());
        for (int i = 0; i < 8; i++) messages.Add(Item(-10));
        messages.Add(Item(3));
        messages.Add(new ObserveRosters());
        messages.Add(new NetworkItemRosterClear(items));
        messages.Add(Item(4, otherItem));
        messages.Add(Item(5));
        messages.Add(new ObserveRosters());
        messages.Add(Troop(TroopRosterElementOperation.AddCounts(5, 5, 100, true)));
        messages.Add(Troop(TroopRosterElementOperation.AddCounts(-4, 0, 50, true)));
        messages.Add(Troop(TroopRosterElementOperation.AddCounts(4, 0, 7, true)));
        messages.Add(new ObserveRosters());
        messages.Add(Troop(TroopRosterElementOperation.AddCounts(-5, -1, 0, true)));
        messages.Add(Troop(TroopRosterElementOperation.SetXp(999)));
        messages.Add(Troop(TroopRosterElementOperation.AddCounts(2, 0, 7, true)));
        messages.Add(Troop(TroopRosterElementOperation.SetXp(12)));
        messages.Add(Troop(TroopRosterElementOperation.AddCounts(-2, 0, 3, false)));
        messages.Add(new ObserveRosters());
        var packets = messages.Select(message => MessagePacket.Create(message, serializer)).ToArray();
        var sent = new List<MessagePacket>();
        var network = new Mock<INetwork>();
        network.Setup(value => value.SendImmediate(It.IsAny<NetPeer>(), It.IsAny<IPacket>()))
            .Callback<NetPeer, IPacket>((_, packet) => sent.Add((MessagePacket)packet));
        var broker = new TestMessageBroker();
        using var queue = new ConnectionMessageQueue(new Lazy<INetwork>(() => network.Object), broker, serializer);
        var peer = Clients.First().NetPeer;
        queue.RegisterPeer(peer);
        queue.BeginQueueing(peer);
        foreach (var packet in packets) queue.TryHandleBroadcast(peer, packet);
        while (queue.FlushBatch(peer).HasMore) { }
        var replay = sent.ToArray();
        Assert.True(replay.Length < packets.Length / 3);

        var clients = Clients.Take(2).ToArray();
        var observations = new[] { new List<string>(), new List<string>() };
        for (int i = 0; i < clients.Length; i++)
        {
            int index = i;
            var client = clients[i];
            client.Resolve<IMessageBroker>().Subscribe<ObserveRosters>(_ =>
                GameThread.RunSafe(() => observations[index].Add(State(client))));
            foreach (var packet in i == 0 ? packets : replay)
                client.SimulatePacket(Server.NetPeer, packet);
        }

        Assert.Equal(5, observations[0].Count);
        Assert.Equal(observations[0], observations[1]);
        clients[1].Call(() =>
        {
            Assert.True(clients[1].ObjectManager.TryGetObject<ItemRoster>(items, out var inventory));
            Assert.Equal(5, inventory.TotalFood);
            Assert.Equal(1, inventory.FoodVariety);
            Assert.Equal(115, inventory.TotalValue);
            Assert.Equal(115, inventory.TradeGoodsTotalValue);
            Assert.True(clients[1].ObjectManager.TryGetObject<TroopRoster>(troops, out var roster));
            var element = Assert.Single(roster.GetTroopRoster());
            Assert.Equal(0, element.Number);
            Assert.Equal(0, element.WoundedNumber);
            Assert.Equal(15, element.Xp);
        });
    }

    private IMessage Item(int amount, string? id = null) =>
        new NetworkItemRosterUpdate(items, id ?? item, null!, amount);

    private IMessage Troop(TroopRosterElementOperation operation) =>
        new NetworkTroopRosterElementBatch(troops, character, new[] { operation });

    private string State(EnvironmentInstance client)
    {
        Assert.True(client.ObjectManager.TryGetObject<ItemRoster>(items, out var itemRoster));
        Assert.True(client.ObjectManager.TryGetObject<TroopRoster>(troops, out var troopRoster));
        var itemRows = itemRoster.Select(row =>
        {
            Assert.True(client.ObjectManager.TryGetId(row.EquipmentElement.Item, out var id));
            return $"{id}:{row.Amount}";
        });
        var troopRows = troopRoster.GetTroopRoster().Select(row =>
        {
            Assert.True(client.ObjectManager.TryGetId(row.Character, out var id));
            return $"{id}:{row.Number}:{row.WoundedNumber}:{row.Xp}";
        });
        return string.Join(",", itemRows) + "|" + string.Join(",", troopRows) +
            $"|{itemRoster.TotalFood}:{itemRoster.FoodVariety}:{itemRoster.TotalValue}:" +
            $"{itemRoster.TradeGoodsTotalValue}:{itemRoster.NumberOfPackAnimals}:" +
            $"{itemRoster.NumberOfMounts}:{itemRoster.NumberOfLivestockAnimals}:" +
            $"{troopRoster.TotalManCount}:{troopRoster.TotalWounded}";
    }
}

/// <summary>Checks bounded replay compaction and records synthetic packet-volume savings.</summary>
public class JoinReplayVolumeTests
{
    private readonly ITestOutputHelper output;
    private readonly ICommonSerializer serializer = new ProtoBufSerializer(new SerializableTypeMapper());

    public JoinReplayVolumeTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void AdjacentBursts_MeasureRetainedBytesCountsAndQueueWork()
    {
        var packets = new List<MessagePacket>();
        for (int burst = 0; burst < 1000; burst++)
        {
            string id = "roster-" + burst;
            for (int i = 0; i < 16; i++) Add(new NetworkItemRosterUpdate(id, "item", null!, 1));
            for (int i = 0; i < 16; i++) Add(new NetworkTroopRosterElementBatch(id, "troop",
                new[] { TroopRosterElementOperation.AddCounts(1, 0, 1, true) }));
            Add(new AddOutputProgressForTown("workshop", 0.1f));
            Add(new AddOutputProgressForTown("workshop", -0.03f));
        }

        Run(packets.Take(34).ToArray(), false);
        Run(packets.Take(34).ToArray(), true);
        var baseline = Run(packets, false);
        var reduced = Run(packets, true);
        Assert.Equal(34000, baseline.Packets.Length);
        Assert.Equal(4000, reduced.Packets.Length);
        Assert.True(reduced.Bytes < baseline.Bytes);
        Assert.Equal(16000, reduced.Packets.Where(packet => packet.MessageType == typeof(NetworkItemRosterUpdate))
            .Sum(packet => serializer.Deserialize<NetworkItemRosterUpdate>(packet.Data).Amount));
        var originalOperations = packets.Where(packet => packet.MessageType == typeof(NetworkTroopRosterElementBatch))
            .SelectMany(packet => serializer.Deserialize<NetworkTroopRosterElementBatch>(packet.Data).Operations);
        var replayOperations = reduced.Packets.Where(packet => packet.MessageType == typeof(NetworkTroopRosterElementBatch))
            .SelectMany(packet => serializer.Deserialize<NetworkTroopRosterElementBatch>(packet.Data).Operations);
        Assert.Equal(originalOperations, replayOperations);
        var originalProgress = packets.Where(packet => packet.MessageType == typeof(AddOutputProgressForTown)).ToArray();
        var replayProgress = reduced.Packets.Where(packet => packet.MessageType == typeof(AddOutputProgressForTown)).ToArray();
        Assert.Equal(originalProgress.Select(packet => packet.Data), replayProgress.Select(packet => packet.Data));
        Assert.Equal(Progress(originalProgress), Progress(replayProgress));
        output.WriteLine($"Synthetic adjacent 16-message bursts: {baseline.Packets.Length} to {reduced.Packets.Length} packets; " +
            $"{baseline.Bytes} to {reduced.Bytes} bytes. Admission {baseline.AdmitMs:F2} to {reduced.AdmitMs:F2} ms; " +
            $"drain {baseline.DrainMs:F2} to {reduced.DrainMs:F2} ms. No live join timing measured.");

        void Add(IMessage message) => packets.Add(MessagePacket.Create(message, serializer));
    }

    [Fact]
    public void TroopMerges_BoundOperationsPreservePayloadAndRejectDifferentIdentities()
    {
        var operations = new[] { TroopRosterElementOperation.AddCounts(1, 1, 7, false) };
        var packet = MessagePacket.Create(new NetworkTroopRosterElementBatch("roster", "troop", operations), serializer);
        operations[0] = TroopRosterElementOperation.SetXp(999);
        var packets = Enumerable.Repeat(packet, 65).ToArray();
        var result = Run(packets, true);
        Assert.Equal(new[] { 32, 32, 1 }, result.Packets.Select(value =>
            serializer.Deserialize<NetworkTroopRosterElementBatch>(value.Data).Operations.Length));
        Assert.All(result.Packets.SelectMany(value => serializer.Deserialize<NetworkTroopRosterElementBatch>(value.Data).Operations),
            operation => Assert.Equal(TroopRosterElementOperationKind.AddCounts, operation.Kind));
        var other = MessagePacket.Create(new NetworkTroopRosterElementBatch("other", "troop", operations), serializer);
        Assert.False(packet.TryMergeForJoinCatchUp(other, serializer, out _));
        other = MessagePacket.Create(new NetworkTroopRosterElementBatch("roster", "hero", operations), serializer);
        Assert.False(packet.TryMergeForJoinCatchUp(other, serializer, out _));
    }

    private int Progress(IEnumerable<MessagePacket> packets)
    {
        float progress = 0;
        foreach (var packet in packets)
            progress += serializer.Deserialize<AddOutputProgressForTown>(packet.Data).ProgressToAdd;
        return BitConverter.SingleToInt32Bits(progress);
    }

    private (MessagePacket[] Packets, long Bytes, double AdmitMs, double DrainMs) Run(
        IEnumerable<MessagePacket> packets, bool merge)
    {
        var sent = new List<MessagePacket>();
        var network = new Mock<INetwork>();
        network.Setup(value => value.SendImmediate(It.IsAny<NetPeer>(), It.IsAny<IPacket>()))
            .Callback<NetPeer, IPacket>((_, packet) => sent.Add((MessagePacket)packet));
        using var queue = new ConnectionMessageQueue(new Lazy<INetwork>(() => network.Object), new TestMessageBroker(), serializer);
        var peer = (NetPeer)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(NetPeer));
        queue.RegisterPeer(peer);
        queue.BeginQueueing(peer);
        if (!merge) queue.EndFinalBaselineCoverage(peer);
        var timer = Stopwatch.StartNew();
        foreach (var packet in packets) queue.TryHandleBroadcast(peer, packet);
        double admit = timer.Elapsed.TotalMilliseconds;
        Assert.False(queue.HasCatchUpOverflowed(peer));
        Assert.True(queue.TryGetCatchUpPendingBytes(peer, out long bytes));
        timer.Restart();
        while (queue.FlushBatch(peer).HasMore) { }
        double drain = timer.Elapsed.TotalMilliseconds;
        return (sent.ToArray(), bytes, admit, drain);
    }
}
