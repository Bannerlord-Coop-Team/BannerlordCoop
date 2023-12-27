using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Coop.Core.Server.Services.Time.Handlers;

/// <summary>
/// Handles time requests and commanding time controls
/// </summary>
public class TimeHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<TimeHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IClientRegistry clientRegistry;

    public TimeHandler(IMessageBroker messageBroker, INetwork network, IClientRegistry clientRegistry)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.clientRegistry = clientRegistry;
        this.messageBroker.Subscribe<AttemptedTimeSpeedChanged>(Handle_TimeSpeedChanged);
        this.messageBroker.Subscribe<NetworkRequestTimeSpeedChange>(Handle_NetworkRequestTimeSpeedChange);
        this.messageBroker.Subscribe<TimeControlModeResponse>(Handle_TimeControlModeResponse);

        AddUnpausePolicy(PlayersLoadingPolicy);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AttemptedTimeSpeedChanged>(Handle_TimeSpeedChanged);
        messageBroker.Unsubscribe<NetworkRequestTimeSpeedChange>(Handle_NetworkRequestTimeSpeedChange);
        messageBroker.Unsubscribe<TimeControlModeResponse>(Handle_TimeControlModeResponse);

        RemoveUnpausePolicy(PlayersLoadingPolicy);
    }

    List<WeakDelegate> unpausePolicies = new List<WeakDelegate>();
    /// <summary>
    /// Adds a policy to consider whether unpausing is allowed
    /// </summary>
    /// <param name="policy">Function to check if unpausing is allowed. True is allowed and false is NOT allowed</param>
    public void AddUnpausePolicy(Func<bool> policy)
    {
        unpausePolicies.Add(policy);
    }

    /// <summary>
    /// Removed a policy to consider whether unpausing is allowed
    /// </summary>
    /// <param name="policy">Policy to remove</param>
    public void RemoveUnpausePolicy(Func<bool> policy)
    {
        unpausePolicies.Remove(policy);
    }

    internal void Handle_NetworkRequestTimeSpeedChange(MessagePayload<NetworkRequestTimeSpeedChange> obj)
    {
        var newMode = obj.What.NewControlMode;

        SetTimeMode(newMode);
    }

    internal void Handle_TimeSpeedChanged(MessagePayload<AttemptedTimeSpeedChanged> obj)
    {
        var newMode = obj.What.NewControlMode;

        SetTimeMode(newMode);
    }

    private bool PlayersLoadingPolicy()
    {
        if (clientRegistry.PlayersLoading)
        {
            var loadingPeers = clientRegistry.LoadingPeers;
            Logger.Information($"{string.Join(",", loadingPeers.Select(p => p.EndPoint.ToString()))} are currently loading, unable to change time");
            return false;
        }

        return true;
    }

    /// <summary>
    /// If any unpause policy fails, unpausing is not allowed
    /// </summary>
    /// <returns>True if unpausing is not allowed, otherwise False</returns>
    private bool UnpauseDisallowed()
    {
        return unpausePolicies.Any(policy => policy.IsAlive && policy.Invoke<bool>(Array.Empty<object>()) == false);
    }

    public void SetTimeMode(TimeControlEnum timeMode)
    {
        if (UnpauseDisallowed()) return;   

        Logger.Verbose("Server changing time to {mode}", timeMode);

        messageBroker.Publish(this, new SetTimeControlMode(timeMode));
        network.SendAll(new NetworkTimeSpeedChanged(timeMode));
    }


    TaskCompletionSource<TimeControlEnum> tcs;
    public bool TryGetTimeControlMode(out TimeControlEnum timeControlMode)
    {
        var cts = new CancellationTokenSource(1000);

        timeControlMode = TimeControlEnum.Pause;

        try
        {
            tcs.Task.Wait(cts.Token);
            timeControlMode = tcs.Task.Result;
            return true;
        }
        catch (OperationCanceledException)
        {
            Logger.Error("Unable to get time control mode");
        }

        return false;
    }

    private void Handle_TimeControlModeResponse(MessagePayload<TimeControlModeResponse> payload)
    {
        tcs.SetResult(payload.What.TimeMode);
    }
}
