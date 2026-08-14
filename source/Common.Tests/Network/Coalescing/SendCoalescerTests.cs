using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using LiteNetLib;
using Moq;
using System.Runtime.Serialization;

namespace Common.Tests.Network.Coalescing;

public class SendCoalescerTests
{
    private sealed class TestMessage : IMessage
    {
        public string Tag { get; }
        public int Value { get; }

        public TestMessage(string tag, int value)
        {
            Tag = tag;
            Value = value;
        }
    }

    private static (SendCoalescer coalescer, INetwork network, List<IMessage> sent) NewFixture()
    {
        var sent = new List<IMessage>();
        var network = new Mock<INetwork>();
        network.Setup(n => n.SendAll(It.IsAny<IMessage>())).Callback<IMessage>(sent.Add);
        return (new SendCoalescer(), network.Object, sent);
    }

    private static int ValueOf(IMessage message) => Assert.IsType<TestMessage>(message).Value;

    private static string TagOf(IMessage message) => Assert.IsType<TestMessage>(message).Tag;

    private static NetPeer NewPeer() =>
        (NetPeer)FormatterServices.GetUninitializedObject(typeof(NetPeer));

    [Fact]
    public void LatestWins_CollapsesToTheLastValue()
    {
        var (coalescer, network, sent) = NewFixture();
        var key = new CoalesceKey("hero", "h1", "Power");

        coalescer.Enqueue(key, new LatestWinsPayload(new TestMessage("Power", 1)));
        coalescer.Enqueue(key, new LatestWinsPayload(new TestMessage("Power", 2)));
        coalescer.Enqueue(key, new LatestWinsPayload(new TestMessage("Power", 3)));

        coalescer.Flush(network);

        Assert.Equal(3, ValueOf(Assert.Single(sent)));
    }

    [Fact]
    public void Summed_AccumulatesDeltas()
    {
        var (coalescer, network, sent) = NewFixture();
        var key = new CoalesceKey("xp", "roster1", "char1");

        coalescer.Enqueue(key, Xp(5));
        coalescer.Enqueue(key, Xp(3));
        coalescer.Enqueue(key, Xp(2));

        coalescer.Flush(network);

        Assert.Equal(10, ValueOf(Assert.Single(sent)));
    }

    [Fact]
    public void Snapshot_BuildsFromTheLatestStateAtFlush()
    {
        var (coalescer, network, sent) = NewFixture();
        var key = new CoalesceKey("roster", "r1");
        int live = 1;

        coalescer.Enqueue(key, new SnapshotPayload(() => new TestMessage("roster", live)));
        live = 2;
        coalescer.Enqueue(key, new SnapshotPayload(() => new TestMessage("roster", live)));
        live = 99; // mutate after the last enqueue; the producer is only invoked at flush

        coalescer.Flush(network);

        Assert.Equal(99, ValueOf(Assert.Single(sent)));
    }

    [Fact]
    public void DistinctKeys_AreNotMerged()
    {
        var (coalescer, network, sent) = NewFixture();

        coalescer.Enqueue(new CoalesceKey("hero", "h1", "Power"), new LatestWinsPayload(new TestMessage("a", 1)));
        coalescer.Enqueue(new CoalesceKey("hero", "h1", "Morale"), new LatestWinsPayload(new TestMessage("b", 2)));
        coalescer.Enqueue(new CoalesceKey("hero", "h2", "Power"), new LatestWinsPayload(new TestMessage("c", 3)));

        coalescer.Flush(network);

        Assert.Equal(3, sent.Count);
    }

    [Fact]
    public void Flush_SendsDistinctKeysInFirstEnqueueOrder()
    {
        var (coalescer, network, sent) = NewFixture();

        coalescer.Enqueue(new CoalesceKey("hero", "h1", "Power"), new LatestWinsPayload(new TestMessage("first", 1)));
        coalescer.Enqueue(new CoalesceKey("hero", "h1", "Morale"), new LatestWinsPayload(new TestMessage("second", 2)));
        coalescer.Enqueue(new CoalesceKey("hero", "h1", "Power"), new LatestWinsPayload(new TestMessage("firstUpdated", 3)));
        coalescer.Enqueue(new CoalesceKey("hero", "h2", "Power"), new LatestWinsPayload(new TestMessage("third", 4)));

        coalescer.Flush(network);

        Assert.Collection(sent,
            first => Assert.Equal("firstUpdated", TagOf(first)),
            second => Assert.Equal("second", TagOf(second)),
            third => Assert.Equal("third", TagOf(third)));
    }

    [Fact]
    public void Flush_ClearsPending()
    {
        var (coalescer, network, sent) = NewFixture();
        coalescer.Enqueue(new CoalesceKey("hero", "h1", "Power"), new LatestWinsPayload(new TestMessage("a", 1)));

        coalescer.Flush(network);
        coalescer.Flush(network);

        Assert.Single(sent);
    }

    [Fact]
    public void FlushInstance_SendsOnlyThatInstanceAndLeavesOthersPending()
    {
        var (coalescer, network, sent) = NewFixture();
        coalescer.Enqueue(new CoalesceKey("hero", "h1", "Power"), new LatestWinsPayload(new TestMessage("h1Power", 1)));
        coalescer.Enqueue(new CoalesceKey("hero", "h1", "Morale"), new LatestWinsPayload(new TestMessage("h1Morale", 2)));
        coalescer.Enqueue(new CoalesceKey("hero", "h2", "Power"), new LatestWinsPayload(new TestMessage("h2Power", 3)));

        coalescer.FlushInstance("h1", network);

        Assert.Equal(2, sent.Count);
        Assert.DoesNotContain(sent, m => TagOf(m) == "h2Power");
        Assert.True(coalescer.HasPending);

        sent.Clear();
        coalescer.Flush(network);
        Assert.Equal("h2Power", TagOf(Assert.Single(sent)));
    }

    [Fact]
    public void FlushInstance_PreservesRelativeEnqueueOrderForThatInstance()
    {
        var (coalescer, network, sent) = NewFixture();
        coalescer.Enqueue(new CoalesceKey("hero", "h1", "Power"), new LatestWinsPayload(new TestMessage("first", 1)));
        coalescer.Enqueue(new CoalesceKey("hero", "h2", "Power"), new LatestWinsPayload(new TestMessage("other", 2)));
        coalescer.Enqueue(new CoalesceKey("hero", "h1", "Morale"), new LatestWinsPayload(new TestMessage("second", 3)));
        coalescer.Enqueue(new CoalesceKey("hero", "h1", "Power"), new LatestWinsPayload(new TestMessage("firstUpdated", 4)));

        coalescer.FlushInstance("h1", network);

        Assert.Collection(sent,
            first => Assert.Equal("firstUpdated", TagOf(first)),
            second => Assert.Equal("second", TagOf(second)));
    }

    [Fact]
    public void Flush_TargetedEntrySendsOnlyToItsPeer()
    {
        var peer = NewPeer();
        var network = new Mock<INetwork>();
        IMessage sent = null!;
        network.Setup(n => n.Send(peer, It.IsAny<IMessage>()))
            .Callback<NetPeer, IMessage>((_, message) => sent = message);
        var coalescer = new SendCoalescer();

        coalescer.EnqueueToPeer(
            new CoalesceKey("hero", "h1", "Power"),
            new LatestWinsPayload(new TestMessage("target", 7)),
            peer);
        coalescer.Flush(network.Object);

        Assert.Equal("target", TagOf(sent));
        network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Never);
        network.Verify(n => n.SendAllBut(It.IsAny<NetPeer>(), It.IsAny<IMessage>()), Times.Never);
    }

    [Fact]
    public void Flush_AllButEntryExcludesItsPeer()
    {
        var peer = NewPeer();
        var network = new Mock<INetwork>();
        IMessage sent = null!;
        network.Setup(n => n.SendAllBut(peer, It.IsAny<IMessage>()))
            .Callback<NetPeer, IMessage>((_, message) => sent = message);
        var coalescer = new SendCoalescer();

        coalescer.EnqueueToAllBut(
            new CoalesceKey("hero", "h1", "Power"),
            new LatestWinsPayload(new TestMessage("observer", 8)),
            peer);
        coalescer.Flush(network.Object);

        Assert.Equal("observer", TagOf(sent));
        network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Never);
        network.Verify(n => n.Send(It.IsAny<NetPeer>(), It.IsAny<IMessage>()), Times.Never);
    }

    [Fact]
    public void Flush_PreservesEachEntriesDeliveryRouteAndOrder()
    {
        var peer = NewPeer();
        var sends = new List<string>();
        var network = new Mock<INetwork>();
        network.Setup(n => n.SendAll(It.IsAny<IMessage>()))
            .Callback<IMessage>(message => sends.Add("all:" + TagOf(message)));
        network.Setup(n => n.Send(peer, It.IsAny<IMessage>()))
            .Callback<NetPeer, IMessage>((_, message) => sends.Add("peer:" + TagOf(message)));
        network.Setup(n => n.SendAllBut(peer, It.IsAny<IMessage>()))
            .Callback<NetPeer, IMessage>((_, message) => sends.Add("except:" + TagOf(message)));
        var coalescer = new SendCoalescer();

        coalescer.Enqueue(new CoalesceKey("all", "h1"),
            new LatestWinsPayload(new TestMessage("first", 1)));
        coalescer.EnqueueToPeer(new CoalesceKey("peer", "h1"),
            new LatestWinsPayload(new TestMessage("second", 2)), peer);
        coalescer.EnqueueToAllBut(new CoalesceKey("except", "h1"),
            new LatestWinsPayload(new TestMessage("third", 3)), peer);
        coalescer.Flush(network.Object);

        Assert.Equal(new[] { "all:first", "peer:second", "except:third" }, sends);
    }

    [Fact]
    public void Enqueue_SameKeyWithDifferentRouteThrows()
    {
        var coalescer = new SendCoalescer();
        var key = new CoalesceKey("hero", "h1", "Power");
        coalescer.Enqueue(key, new LatestWinsPayload(new TestMessage("all", 1)));

        Assert.Throws<InvalidOperationException>(() => coalescer.EnqueueToPeer(
            key,
            new LatestWinsPayload(new TestMessage("peer", 2)),
            NewPeer()));
    }

    [Fact]
    public void FlushInstance_UsesTheBufferedTargetedRoute()
    {
        var peer = NewPeer();
        var network = new Mock<INetwork>();
        var coalescer = new SendCoalescer();
        coalescer.EnqueueToPeer(
            new CoalesceKey("hero", "h1", "Power"),
            new LatestWinsPayload(new TestMessage("h1", 1)),
            peer);
        coalescer.Enqueue(
            new CoalesceKey("hero", "h2", "Power"),
            new LatestWinsPayload(new TestMessage("h2", 2)));

        coalescer.FlushInstance("h1", network.Object);

        network.Verify(n => n.Send(peer, It.Is<IMessage>(message => TagOf(message) == "h1")), Times.Once);
        network.Verify(n => n.SendAll(It.IsAny<IMessage>()), Times.Never);
        Assert.True(coalescer.HasPending);
    }

    [Fact]
    public void DropInstance_DiscardsWithoutSending()
    {
        var (coalescer, network, sent) = NewFixture();
        coalescer.Enqueue(new CoalesceKey("hero", "h1", "Power"), new LatestWinsPayload(new TestMessage("h1", 1)));
        coalescer.Enqueue(new CoalesceKey("hero", "h2", "Power"), new LatestWinsPayload(new TestMessage("h2", 2)));

        coalescer.DropInstance("h1");
        coalescer.Flush(network);

        Assert.Equal("h2", TagOf(Assert.Single(sent)));
    }

    [Fact]
    public void HasPending_TracksBufferState()
    {
        var (coalescer, network, _) = NewFixture();

        Assert.False(coalescer.HasPending);
        coalescer.Enqueue(new CoalesceKey("hero", "h1", "Power"), new LatestWinsPayload(new TestMessage("a", 1)));
        Assert.True(coalescer.HasPending);
        coalescer.Flush(network);
        Assert.False(coalescer.HasPending);
    }

    [Fact]
    public void Flush_OnEmptyBuffer_SendsNothing()
    {
        var (coalescer, network, sent) = NewFixture();

        coalescer.Flush(network);

        Assert.Empty(sent);
    }

    [Fact]
    public void Summed_MergeWithMismatchedPayloadType_Throws()
    {
        var (coalescer, _, _) = NewFixture();
        var key = new CoalesceKey("xp", "r1", "c1");
        coalescer.Enqueue(key, Xp(5));

        Assert.Throws<ArgumentException>(() =>
            coalescer.Enqueue(key, new LatestWinsPayload(new TestMessage("xp", 1))));
    }

    [Fact]
    public void LatestWins_MergeWithMismatchedPayloadType_Throws()
    {
        var (coalescer, _, _) = NewFixture();
        var key = new CoalesceKey("hero", "h1", "Power");
        coalescer.Enqueue(key, new LatestWinsPayload(new TestMessage("Power", 1)));

        Assert.Throws<ArgumentException>(() => coalescer.Enqueue(key, Xp(5)));
    }

    [Fact]
    public void Snapshot_MergeWithMismatchedPayloadType_Throws()
    {
        var (coalescer, _, _) = NewFixture();
        var key = new CoalesceKey("roster", "r1");
        coalescer.Enqueue(key, new SnapshotPayload(() => new TestMessage("roster", 1)));

        Assert.Throws<ArgumentException>(() =>
            coalescer.Enqueue(key, new LatestWinsPayload(new TestMessage("roster", 2))));
    }

    [Fact]
    public void CoalesceKey_HasValueEqualityAndHashing()
    {
        var a = new CoalesceKey("c", "i", "m");
        var b = new CoalesceKey("c", "i", "m");
        var c = new CoalesceKey("c", "i", "other");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Enqueue_IsThreadSafe()
    {
        var (coalescer, network, sent) = NewFixture();
        const int threads = 8;
        const int perThread = 1000;
        var key = new CoalesceKey("xp", "r1", "c1");

        Parallel.For(0, threads, _ =>
        {
            for (int i = 0; i < perThread; i++)
            {
                coalescer.Enqueue(key, Xp(1));
            }
        });

        coalescer.Flush(network);

        Assert.Equal(threads * perThread, ValueOf(Assert.Single(sent)));
    }

    private static SummedPayload<int> Xp(int delta) =>
        new SummedPayload<int>(delta, (a, b) => a + b, total => new TestMessage("xp", total));
}
