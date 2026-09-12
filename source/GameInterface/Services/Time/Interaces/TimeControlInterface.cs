using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Time;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Interaces;

public interface IAutomaticPauseLease
{
    /// <summary>
    /// Releases this owner when policies permit it. A false result keeps the lease active for retry.
    /// </summary>
    bool TryRelease();
}

public interface ITimeControlInterface : IGameAbstraction
{
    void AddUnpausePolicy(Func<bool> policy);
    void RemoveUnpausePolicy(Func<bool> policy);
    void AddFastForwardPolicy(Func<bool> policy);
    void RemoveFastForwardPolicy(Func<bool> policy);
    bool CanSetTimeControl(TimeControlEnum timeMode);
    TimeControlEnum GetTimeControl();
    IAutomaticPauseLease ServerAcquireAutomaticPause();
    void ClientSetTimeControl(TimeControlEnum newMode);
    void ServerSetTimeControl(TimeControlEnum timeMode);
#if DEBUG
    void ServerSetTimeControlForLiveTest(TimeControlEnum timeMode);
#endif
}

internal class TimeControlInterface : ITimeControlInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<TimeControlInterface>();

    private readonly ITimeControlModeConverter modeConverter;
    private readonly List<WeakDelegate> unpausePolicies = new List<WeakDelegate>();
    private readonly List<WeakDelegate> fastForwardPolicies = new List<WeakDelegate>();
    private readonly INetwork network;
    private readonly object timeControlLock = new object();
    private readonly HashSet<AutomaticPauseLease> automaticPauseLeases = new HashSet<AutomaticPauseLease>();
    private TimeControlEnum? automaticPauseResumeMode;

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
        lock (timeControlLock)
        {
            unpausePolicies.Add(policy);
        }
    }

    /// <summary>
    /// Removed a policy to consider whether unpausing is allowed
    /// </summary>
    /// <param name="policy">Policy to remove</param>
    public void RemoveUnpausePolicy(Func<bool> policy)
    {
        lock (timeControlLock)
        {
            unpausePolicies.Remove(policy);
        }
    }

    /// <summary>
    /// Adds a policy to consider whether fast-forwarding is allowed
    /// </summary>
    /// <param name="policy">Function to check if fast-forwarding is allowed. True is allowed and false is NOT allowed</param>
    public void AddFastForwardPolicy(Func<bool> policy)
    {
        lock (timeControlLock)
        {
            fastForwardPolicies.Add(policy);
        }
    }

    /// <summary>
    /// Removes a policy to consider whether fast-forwarding is allowed
    /// </summary>
    /// <param name="policy">Policy to remove</param>
    public void RemoveFastForwardPolicy(Func<bool> policy)
    {
        lock (timeControlLock)
        {
            fastForwardPolicies.Remove(policy);
        }
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
    internal TimeControlEnum LimitTimeControl(
        TimeControlEnum requestedMode,
        bool bypassFastForwardPolicies = false)
    {
        lock (timeControlLock)
        {
            if (requestedMode != TimeControlEnum.Pause &&
                TryGetDisallowingPolicy(unpausePolicies, out var unpausePolicy))
            {
                Logger.Information(
                    "Time control request {RequestedMode} limited to {EffectiveMode} by {Policy}",
                    requestedMode,
                    TimeControlEnum.Pause,
                    unpausePolicy);
                return TimeControlEnum.Pause;
            }

            if (requestedMode == TimeControlEnum.Play_2x &&
                !bypassFastForwardPolicies &&
                TryGetDisallowingPolicy(fastForwardPolicies, out var fastForwardPolicy))
            {
                Logger.Information(
                    "Time control request {RequestedMode} limited to {EffectiveMode} by {Policy}",
                    requestedMode,
                    TimeControlEnum.Play_1x,
                    fastForwardPolicy);
                return TimeControlEnum.Play_1x;
            }

            return requestedMode;
        }
    }

    /// <summary>
    /// Evaluates a set of time control policies. Each policy returns true when its
    /// action is allowed; if any live policy returns false, the action is disallowed.
    /// </summary>
    /// <param name="policies">The policies to evaluate</param>
    /// <param name="policyName">The policy that disallowed the action</param>
    /// <returns>True if any policy disallows the action, otherwise false</returns>
    private static bool TryGetDisallowingPolicy(List<WeakDelegate> policies, out string policyName)
    {
        foreach (var policy in policies)
        {
            if (policy.IsAlive == false)
            {
                continue;
            }

            if (policy.Invoke<bool>(Array.Empty<object>()) == false)
            {
                policyName = $"{policy.Method.DeclaringType?.Name}.{policy.Method.Name}";
                return true;
            }
        }

        policyName = null;
        return false;
    }

    /// <summary>
    /// This should only run server side
    /// </summary>
    /// <param name="timeMode"></param>
    public void ServerSetTimeControl(TimeControlEnum timeMode)
    {
        ApplyServerTimeControl(timeMode, false);
    }

#if DEBUG
    public void ServerSetTimeControlForLiveTest(TimeControlEnum timeMode)
    {
        ApplyServerTimeControl(timeMode, true);
    }
#endif

    private void ApplyServerTimeControl(
        TimeControlEnum timeMode,
        bool bypassFastForwardPolicies)
    {
        if (ModInformation.IsClient)
        {
            Logger.Warning("Client attempted to set time mode. This is only allowed on the server. {CallStack}", Environment.StackTrace);
            return;
        }

        lock (timeControlLock)
        {
            var effectiveMode = LimitTimeControl(timeMode, bypassFastForwardPolicies);
            if (timeMode == TimeControlEnum.Pause || effectiveMode != TimeControlEnum.Pause)
            {
                InvalidateAutomaticPauses();
            }

            ApplyResolvedServerTimeControl(timeMode, effectiveMode, bypassFastForwardPolicies);
        }
    }

    public IAutomaticPauseLease ServerAcquireAutomaticPause()
    {
        lock (timeControlLock)
        {
            bool isFirstAutomaticPause = automaticPauseLeases.Count == 0;
            var previousMode = isFirstAutomaticPause ? GetTimeControl() : TimeControlEnum.Pause;
            var pauseLease = new AutomaticPauseLease(this);
            automaticPauseLeases.Add(pauseLease);

            if (!isFirstAutomaticPause || previousMode == TimeControlEnum.Pause)
            {
                return pauseLease;
            }

            automaticPauseResumeMode = previousMode;
            try
            {
                ApplyResolvedServerTimeControl(TimeControlEnum.Pause, TimeControlEnum.Pause, false);
                return pauseLease;
            }
            catch
            {
                CompleteAutomaticPauseLease(pauseLease);
                automaticPauseResumeMode = null;
                throw;
            }
        }
    }

    private bool TryReleaseAutomaticPause(AutomaticPauseLease pauseLease)
    {
        lock (timeControlLock)
        {
            if (!automaticPauseLeases.Contains(pauseLease))
            {
                return true;
            }

            if (automaticPauseLeases.Count > 1)
            {
                CompleteAutomaticPauseLease(pauseLease);
                return true;
            }

            if (!automaticPauseResumeMode.HasValue)
            {
                CompleteAutomaticPauseLease(pauseLease);
                return true;
            }

            var requestedMode = automaticPauseResumeMode.Value;
            var effectiveMode = LimitTimeControl(requestedMode);
            if (effectiveMode == TimeControlEnum.Pause)
            {
                return false;
            }

            CompleteAutomaticPauseLease(pauseLease);
            automaticPauseResumeMode = null;
            ApplyResolvedServerTimeControl(requestedMode, effectiveMode, false);
            return true;
        }
    }

    private void CompleteAutomaticPauseLease(AutomaticPauseLease pauseLease)
    {
        automaticPauseLeases.Remove(pauseLease);
    }

    private void InvalidateAutomaticPauses()
    {
        automaticPauseLeases.Clear();
        automaticPauseResumeMode = null;
    }

    private void ApplyResolvedServerTimeControl(
        TimeControlEnum requestedMode,
        TimeControlEnum effectiveMode,
        bool bypassFastForwardPolicies)
    {
        var currentMode = Campaign.Current == null
            ? "<unavailable>"
            : GetTimeControl().ToString();

        Logger.Information(
            "Applying server time control: current={CurrentMode} requested={RequestedMode} effective={EffectiveMode} liveTestFastForwardBypass={LiveTestFastForwardBypass}",
            currentMode,
            requestedMode,
            effectiveMode,
            bypassFastForwardPolicies);

        network.SendAll(new NetworkChangeTimeControlMode(effectiveMode));

        ClientSetTimeControl(effectiveMode);
    }

    private sealed class AutomaticPauseLease : IAutomaticPauseLease
    {
        private readonly TimeControlInterface timeControlInterface;

        public AutomaticPauseLease(TimeControlInterface timeControlInterface)
        {
            this.timeControlInterface = timeControlInterface;
        }

        public bool TryRelease() => timeControlInterface.TryReleaseAutomaticPause(this);
    }
}
