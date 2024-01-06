using Common.Messaging;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Messages;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Time.Commands;

internal class TimeCommands
{
    [CommandLineArgumentFunction("get_time_mode", "coop.debug")]
    public static string GetTimeMode(List<string> strings)
    {
        var tx = new GetterTransaction();

        return $"{tx.GetTimeControlMode()}";
    }
}

class GetterTransaction
{
    public GetterTransaction()
    {
        MessageBroker.Instance.Subscribe<TimeControlModeResponse>(Handle);
    }

    ~GetterTransaction()
    {
        MessageBroker.Instance.Unsubscribe<TimeControlModeResponse>(Handle);
    }

    TaskCompletionSource<TimeControlEnum> tcs;
    public string GetTimeControlMode()
    {
        tcs = new TaskCompletionSource<TimeControlEnum>();
        var cts = new CancellationTokenSource(1000);

        MessageBroker.Instance.Publish(this, new GetTimeControlMode());

        try
        {
            tcs.Task.Wait(cts.Token);
            return $"{tcs.Task.Result}";
        }
        catch(OperationCanceledException)
        {
            return "Failed to get time mode";
        }
    }

    private void Handle(MessagePayload<TimeControlModeResponse> payload)
    {
        tcs.SetResult(payload.What.TimeMode);
    }
}
