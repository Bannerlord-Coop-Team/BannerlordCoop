using Common.Logging;
using Serilog;
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
    private readonly ILogger Logger = LogManager.GetLogger<MessageTransaction<T>>();

    private readonly CancellationTokenSource cts;
    private readonly TaskCompletionSource<bool> tcs;
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

        cts.Token.Register(() =>
        {
            tcs.TrySetCanceled();
        });
    }

    public void Wait()
    {
        try
        {
            tcs.Task.Wait();
        }
        catch(AggregateException ex)
        {
            // Raise exception if it's not a TaskCanceledException
            if (ex.InnerException is TaskCanceledException == false) throw ex;

            Logger.Error("Could not sync new hero on all clients");
        }
    }

    private void TargetMessageProcessed(MessagePayload<T> payload)
    {
        tcs.TrySetResult(true);
    }

    public void Dispose()
    {
        Wait();
        messageBroker.Unsubscribe(targetMethod);
    }
}
