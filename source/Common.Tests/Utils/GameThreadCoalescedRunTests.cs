using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Common.Tests.Utils;

[Collection(nameof(GameThreadCoalescedRunCollection))]
public sealed class GameThreadCoalescedRunTests : IDisposable
{
    public GameThreadCoalescedRunTests()
    {
        GameThread.Instance.MarkGameThread();
        GameThread.Instance.Update(TimeSpan.Zero);
    }

    public void Dispose()
    {
        GameThread.Instance.Update(TimeSpan.Zero);
        GameThread.Instance.UnmarkGameThread();
    }

    [Fact]
    public void RunSafeCoalesced_RetainsLatestValuePerKeyInOneQueueEntry()
    {
        var applied = new List<KeyedValue>();
        Func<KeyedValue, int> keySelector = value => value.Key;
        Action<IReadOnlyList<KeyedValue>> apply = values => applied.AddRange(values);

        RunOffGameThread(() =>
        {
            GameThread.RunSafeCoalesced(
                new[] { new KeyedValue(1, 10) }, keySelector, apply);
            GameThread.RunSafeCoalesced(
                new[] { new KeyedValue(1, 20), new KeyedValue(2, 30) },
                keySelector,
                apply);
        });

        Assert.Equal(1, GameThread.Instance.QueueLength);
        GameThread.Instance.Update(TimeSpan.Zero);

        Assert.Equal(new[] { 20, 30 }, applied.Select(value => value.Value));
    }

    [Fact]
    public void RunSafeCoalesced_DoesNotCrossOrdinaryQueueBarrier()
    {
        var applied = new List<string>();
        Func<KeyedValue, int> keySelector = value => value.Key;
        Action<IReadOnlyList<KeyedValue>> apply = values =>
            applied.AddRange(values.Select(value => value.Value.ToString()));

        RunOffGameThread(() =>
        {
            GameThread.RunSafeCoalesced(
                new[] { new KeyedValue(1, 10) }, keySelector, apply);
            GameThread.Run(() => applied.Add("barrier"));
            GameThread.RunSafeCoalesced(
                new[] { new KeyedValue(1, 20) }, keySelector, apply);
        });

        Assert.Equal(3, GameThread.Instance.QueueLength);
        GameThread.Instance.Update(TimeSpan.Zero);

        Assert.Equal(new[] { "10", "barrier", "20" }, applied);
    }

    private static void RunOffGameThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                failure = e;
            }
        });
        thread.Start();
        thread.Join();

        if (failure != null) throw failure;
    }

    private readonly struct KeyedValue
    {
        public int Key { get; }
        public int Value { get; }

        public KeyedValue(int key, int value)
        {
            Key = key;
            Value = value;
        }
    }
}

[CollectionDefinition(
    nameof(GameThreadCoalescedRunCollection),
    DisableParallelization = true)]
public sealed class GameThreadCoalescedRunCollection
{
}
