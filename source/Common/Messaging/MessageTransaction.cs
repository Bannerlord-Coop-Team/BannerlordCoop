using System;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Messaging;

/// <summary>
/// Waits for an event to be called.
/// </summary>
/// <typeparam name="T">Message to wait for</typeparam>
public class MessageTransaction<T> : IDisposable where T : IMessage
{
    private readonly CancellationTokenSource cts;
    private readonly TaskCompletionSource<bool> tcs;
    private readonly Task waitTask;
    private readonly IMessageBroker messageBroker;
    private readonly Action<MessagePayload<T>> targetMethod;

    public MessageTransaction(
        IMessageBroker messageBroker,
        TimeSpan timeout, 
        Action<MessagePayload<T>, TaskCompletionSource<bool>> customTargetMessageFunction = null)
    {
        this.messageBroker = messageBroker;
        cts = new CancellationTokenSource(timeout);
        tcs = new TaskCompletionSource<bool>();

        if (customTargetMessageFunction == null)
        {
            targetMethod = TargetMessageProcessed;
        }
        else
        {
            targetMethod = payload => customTargetMessageFunction(payload, tcs);
        }

        messageBroker.Subscribe(targetMethod);

        waitTask = Task.Run(async () => await tcs.Task, cts.Token);
    }

    public void Wait()
    {
        waitTask.Wait();
    }

    private void TargetMessageProcessed(MessagePayload<T> payload)
    {
        tcs.SetResult(true);
    }

    public void Dispose()
    {
        Wait();
        messageBroker.Unsubscribe(targetMethod);
        cts.Cancel();
        cts.Dispose();
    }
}
