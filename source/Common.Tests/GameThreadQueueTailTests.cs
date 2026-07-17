using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Moq;
using System.Runtime.ExceptionServices;

namespace Common.Tests;

/// <summary>
/// Verifies queue-tail deduplication and its ordering boundaries.
/// </summary>
[Collection(GameThreadQueueTailCollection.Name)]
public class GameThreadQueueTailTests : IDisposable
{
    /// <summary>
    /// Test message used to observe send order and payload values.
    /// </summary>
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

    public GameThreadQueueTailTests()
    {
        GameThread.Instance.MarkGameThread();
        Assert.Equal(0, GameThread.Instance.QueueLength);
    }

    public void Dispose()
    {
        GameThread.Instance.MarkGameThread();
        if (GameThread.Instance.QueueLength > 0)
        {
            GameThread.Instance.Update(TimeSpan.Zero);
        }
    }

    [Fact]
    public void AdjacentFlushes_CollapseToOneQueueEntry()
    {
        var (coalescer, network, sent) = NewFixture();
        var flushKey = new object();
        coalescer.Enqueue(
            new CoalesceKey("latest", "one"),
            new LatestWinsPayload(new TestMessage("update", 1)));

        QueueOffThread(() =>
        {
            QueueFlush(flushKey, coalescer, network);
            QueueFlush(flushKey, coalescer, network);
        });

        Assert.Equal(1, GameThread.Instance.QueueLength);
        DrainGameThread();
        AssertMessage(sent, 0, "update", 1);
        Assert.Single(sent);
    }

    [Fact]
    public void InterveningAction_PreservesFlushAndDirectSendOrder()
    {
        var (coalescer, network, sent) = NewFixture();
        var flushKey = new object();
        var valueKey = new CoalesceKey("latest", "one");
        coalescer.Enqueue(valueKey, new LatestWinsPayload(new TestMessage("update", 1)));

        QueueOffThread(() =>
        {
            QueueFlush(flushKey, coalescer, network);
            GameThread.Run(() =>
                coalescer.Enqueue(valueKey, new LatestWinsPayload(new TestMessage("update", 2))));
            QueueFlush(flushKey, coalescer, network);
            GameThread.Run(() => network.SendAll(new TestMessage("direct", 3)));
        });

        Assert.Equal(4, GameThread.Instance.QueueLength);
        DrainGameThread();
        Assert.Collection(
            sent,
            message => AssertMessage(message, "update", 1),
            message => AssertMessage(message, "update", 2),
            message => AssertMessage(message, "direct", 3));
    }

    [Fact]
    public void InterveningRunSafeAction_PreservesBothKeyedActions()
    {
        var queueTailKey = new object();
        var order = new List<int>();

        QueueOffThread(() =>
        {
            GameThread.RunSafeOnceAtQueueTail(queueTailKey, () => order.Add(1));
            GameThread.RunSafe(() => order.Add(2));
            GameThread.RunSafeOnceAtQueueTail(queueTailKey, () => order.Add(3));
        });

        Assert.Equal(3, GameThread.Instance.QueueLength);
        DrainGameThread();
        Assert.Equal(new[] { 1, 2, 3 }, order);
    }

    [Fact]
    public void MultipleInterveningProducers_PreserveEveryFlushBoundary()
    {
        var (coalescer, network, sent) = NewFixture();
        var flushKey = new object();
        coalescer.Enqueue(
            new CoalesceKey("latest", "initial"),
            new LatestWinsPayload(new TestMessage("initial", 0)));

        QueueOffThread(() =>
        {
            QueueFlush(flushKey, coalescer, network);
            GameThread.Run(() => coalescer.Enqueue(
                new CoalesceKey("latest", "k1"),
                new LatestWinsPayload(new TestMessage("k1", 1))));
            QueueFlush(flushKey, coalescer, network);
            GameThread.Run(() => coalescer.Enqueue(
                new CoalesceKey("latest", "k2"),
                new LatestWinsPayload(new TestMessage("k2", 2))));
            QueueFlush(flushKey, coalescer, network);
        });

        Assert.Equal(5, GameThread.Instance.QueueLength);
        DrainGameThread();
        Assert.Collection(
            sent,
            message => AssertMessage(message, "initial", 0),
            message => AssertMessage(message, "k1", 1),
            message => AssertMessage(message, "k2", 2));
    }

    [Fact]
    public void AdjacentFlushDeduplication_PreservesLatestWinsAndSummedPayloadBoundaries()
    {
        var (coalescer, network, sent) = NewFixture();
        var flushKey = new object();
        var latestKey = new CoalesceKey("latest", "one");
        var summedKey = new CoalesceKey("summed", "one");
        coalescer.Enqueue(latestKey, new LatestWinsPayload(new TestMessage("latest", 1)));
        coalescer.Enqueue(summedKey, Sum("summed", 2));

        QueueOffThread(() =>
        {
            QueueFlush(flushKey, coalescer, network);
            QueueFlush(flushKey, coalescer, network);
            GameThread.Run(() =>
            {
                coalescer.Enqueue(latestKey, new LatestWinsPayload(new TestMessage("latest", 2)));
                coalescer.Enqueue(latestKey, new LatestWinsPayload(new TestMessage("latest", 3)));
                coalescer.Enqueue(summedKey, Sum("summed", 4));
                coalescer.Enqueue(summedKey, Sum("summed", 5));
            });
            QueueFlush(flushKey, coalescer, network);
            QueueFlush(flushKey, coalescer, network);
        });

        Assert.Equal(3, GameThread.Instance.QueueLength);
        DrainGameThread();
        Assert.Collection(
            sent,
            message => AssertMessage(message, "latest", 1),
            message => AssertMessage(message, "summed", 2),
            message => AssertMessage(message, "latest", 3),
            message => AssertMessage(message, "summed", 9));
    }

    [Fact]
    public void ThrowingKeyedAction_DoesNotLeaveAStaleTailKey()
    {
        var queueTailKey = new object();
        int runs = 0;

        QueueOffThread(() =>
        {
            GameThread.RunSafeOnceAtQueueTail(queueTailKey, () =>
            {
                runs++;
                throw new InvalidOperationException("expected test exception");
            });
            GameThread.RunSafeOnceAtQueueTail(queueTailKey, () => runs++);
        });
        DrainGameThread();

        QueueOffThread(() => GameThread.RunSafeOnceAtQueueTail(queueTailKey, () => runs++));
        DrainGameThread();

        Assert.Equal(2, runs);
    }

    [Fact]
    public void RunAndRunSafe_KeepTheirExistingNonDeduplicatingBehavior()
    {
        int runs = 0;
        Action action = () => runs++;

        QueueOffThread(() =>
        {
            GameThread.Run(action);
            GameThread.Run(action);
            GameThread.RunSafe(action);
            GameThread.RunSafe(action);
        });

        Assert.Equal(4, GameThread.Instance.QueueLength);
        DrainGameThread();
        Assert.Equal(4, runs);
    }

    [Fact]
    public void ConcurrentAdjacentRequests_AtomicallyQueueOneAction()
    {
        const int workerCount = 16;
        var queueTailKey = new object();
        var start = new ManualResetEventSlim(false);
        var workers = new Thread[workerCount];
        var failures = new Exception?[workerCount];
        int runs = 0;

        for (int i = 0; i < workers.Length; i++)
        {
            int index = i;
            workers[i] = new Thread(() =>
            {
                try
                {
                    start.Wait();
                    GameThread.RunSafeOnceAtQueueTail(
                        queueTailKey,
                        () => Interlocked.Increment(ref runs));
                }
                catch (Exception exception)
                {
                    failures[index] = exception;
                }
            });
            workers[i].Start();
        }

        start.Set();
        foreach (Thread worker in workers)
        {
            worker.Join();
        }

        foreach (Exception? failure in failures)
        {
            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }

        Assert.Equal(1, GameThread.Instance.QueueLength);
        DrainGameThread();
        Assert.Equal(1, runs);
    }

    private static (SendCoalescer Coalescer, INetwork Network, List<TestMessage> Sent) NewFixture()
    {
        var sent = new List<TestMessage>();
        var network = new Mock<INetwork>();
        network
            .Setup(instance => instance.SendAll(It.IsAny<IMessage>()))
            .Callback<IMessage>(message => sent.Add(Assert.IsType<TestMessage>(message)));
        return (new SendCoalescer(), network.Object, sent);
    }

    private static void QueueFlush(
        object queueTailKey,
        SendCoalescer coalescer,
        INetwork network)
    {
        GameThread.RunSafeOnceAtQueueTail(queueTailKey, () => coalescer.Flush(network));
    }

    private static SummedPayload<int> Sum(string tag, int value) =>
        new SummedPayload<int>(
            value,
            (running, next) => running + next,
            total => new TestMessage(tag, total));

    private static void QueueOffThread(Action queueActions)
    {
        Exception? failure = null;
        var worker = new Thread(() =>
        {
            try
            {
                queueActions();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        worker.Start();
        worker.Join();

        if (failure != null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static void DrainGameThread()
    {
        GameThread.Instance.Update(TimeSpan.Zero);
        Assert.Equal(0, GameThread.Instance.QueueLength);
    }

    private static void AssertMessage(
        IReadOnlyList<TestMessage> messages,
        int index,
        string tag,
        int value) => AssertMessage(messages[index], tag, value);

    private static void AssertMessage(TestMessage message, string tag, int value)
    {
        Assert.Equal(tag, message.Tag);
        Assert.Equal(value, message.Value);
    }
}

/// <summary>
/// Prevents tests from sharing the singleton game-thread queue concurrently.
/// </summary>
[CollectionDefinition(GameThreadQueueTailCollection.Name, DisableParallelization = true)]
public sealed class GameThreadQueueTailCollection
{
    public const string Name = nameof(GameThreadQueueTailCollection);
}
