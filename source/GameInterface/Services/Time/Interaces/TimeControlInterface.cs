using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Time;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Interaces;

public interface ITimeControlInterface : IGameAbstraction
{
    void AddUnpausePolicy(Func<bool> policy);
    void RemoveUnpausePolicy(Func<bool> policy);
    void AddFastForwardPolicy(Func<bool> policy);
    void RemoveFastForwardPolicy(Func<bool> policy);
    bool CanSetTimeControl(TimeControlEnum timeMode);
    TimeControlEnum GetTimeControl();
    void ClientSetTimeControl(TimeControlEnum newMode);
    void ServerSetTimeControl(TimeControlEnum timeMode);
}

internal class TimeControlInterface : ITimeControlInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<TimeControlInterface>();

    private readonly ITimeControlModeConverter modeConverter;
    private readonly List<WeakDelegate> unpausePolicies = new List<WeakDelegate>();
    private readonly List<WeakDelegate> fastForwardPolicies = new List<WeakDelegate>();
    private readonly INetwork network;

    public TimeControlInterface(ITimeControlModeConverter modeConverter, INetwork network)
    {
        this.modeConverter = modeConverter;
        this.network = network;
    }

    public TimeControlEnum GetTimeControl()
    {
        return modeConverter.Convert(Campaign.Current.TimeControlMode);
    }

    public void ClientSetTimeControl(TimeControlEnum newMode)
    {
        TimePatches.OverrideTimeControlMode(modeConverter.Convert(newMode));
    }

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

    /// <summary>
    /// Adds a policy to consider whether fast-forwarding is allowed
    /// </summary>
    /// <param name="policy">Function to check if fast-forwarding is allowed. True is allowed and false is NOT allowed</param>
    public void AddFastForwardPolicy(Func<bool> policy)
    {
        fastForwardPolicies.Add(policy);
    }

    /// <summary>
    /// Removes a policy to consider whether fast-forwarding is allowed
    /// </summary>
    /// <param name="policy">Policy to remove</param>
    public void RemoveFastForwardPolicy(Func<bool> policy)
    {
        fastForwardPolicies.Remove(policy);
    }

    public bool CanSetTimeControl(TimeControlEnum timeMode)
    {
        return LimitTimeControl(timeMode) == timeMode;
    }

    /// <summary>
    /// Reduces a requested time control mode to what the active policies allow.
    /// Unpausing being blocked forces <see cref="TimeControlEnum.Pause"/>, while
    /// fast-forwarding being blocked caps the speed at <see cref="TimeControlEnum.Play_1x"/>.
    /// </summary>
    /// <param name="requestedMode">The time control mode being requested</param>
    /// <returns>The highest mode the policies permit for the request</returns>
    internal TimeControlEnum LimitTimeControl(TimeControlEnum requestedMode)
    {
        if (requestedMode != TimeControlEnum.Pause && AnyPolicyDisallows(unpausePolicies))
        {
            return TimeControlEnum.Pause;
        }

        if (requestedMode == TimeControlEnum.Play_2x && AnyPolicyDisallows(fastForwardPolicies))
        {
            return TimeControlEnum.Play_1x;
        }

        return requestedMode;
    }

    /// <summary>
    /// Evaluates a set of time control policies. Each policy returns true when its
    /// action is allowed; if any live policy returns false, the action is disallowed.
    /// </summary>
    /// <param name="policies">The policies to evaluate</param>
    /// <returns>True if any policy disallows the action, otherwise false</returns>
    private static bool AnyPolicyDisallows(List<WeakDelegate> policies)
    {
        foreach (var policy in policies)
        {
            if (policy.IsAlive == false)
            {
                continue;
            }

            if (policy.Invoke<bool>(Array.Empty<object>()) == false)
            {
                return true;
            }
        }

        return false;
    }


    /// <summary>
    /// This should only run server side
    /// </summary>
    /// <param name="timeMode"></param>
    public void ServerSetTimeControl(TimeControlEnum timeMode)
    {
        if (ModInformation.IsClient)
        {
            Logger.Warning("Client attempted to set time mode. This is only allowed on the server. {CallStack}", Environment.StackTrace);
            return;
        }

        timeMode = LimitTimeControl(timeMode);

        Logger.Verbose("Server changing time to {mode}", timeMode);

        network.SendAll(new NetworkChangeTimeControlMode(timeMode));

        ClientSetTimeControl(timeMode);
    }
}
