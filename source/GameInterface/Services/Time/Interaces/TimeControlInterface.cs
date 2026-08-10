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

public interface ITimeControlInterface : IGameAbstraction
{
    void AddUnpausePolicy(Func<bool> policy);
    void RemoveUnpausePolicy(Func<bool> policy);
    void AddFastForwardPolicy(Func<bool> policy);
    void RemoveFastForwardPolicy(Func<bool> policy);
    bool CanSetTimeControl(TimeControlEnum timeMode);
    TimeControlEnum GetTimeControl();
    bool ServerTryCreatePause(out TimeControlEnum previousMode, out long pauseToken);
    AutomaticPauseRestoreResult ServerTryRestoreTimeControl(
        long pauseToken,
        out TimeControlEnum restoredMode);
    void ClientSetTimeControl(TimeControlEnum newMode);
    void ServerSetTimeControl(TimeControlEnum timeMode);
}

public enum AutomaticPauseRestoreResult
{
    Stale,
    StillPaused,
    Blocked,
    Restored,
}

internal class TimeControlInterface : ITimeControlInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<TimeControlInterface>();

    private readonly ITimeControlModeConverter modeConverter;
    private readonly List<WeakDelegate> unpausePolicies = new List<WeakDelegate>();
    private readonly List<WeakDelegate> fastForwardPolicies = new List<WeakDelegate>();
    private readonly INetwork network;
    private readonly object timeControlLock = new object();
    private readonly HashSet<long> automaticPauseTokens = new HashSet<long>();
    private TimeControlEnum? automaticPauseResumeMode;
    private long nextAutomaticPauseToken;

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
        if (ModInformation.IsClient)
        {
            Logger.Warning("Client attempted to set time mode. This is only allowed on the server. {CallStack}", Environment.StackTrace);
            return;
        }

        lock (timeControlLock)
        {
            var effectiveMode = LimitTimeControl(timeMode);
            if (timeMode == TimeControlEnum.Pause || effectiveMode != TimeControlEnum.Pause)
            {
                InvalidateAutomaticPauses();
            }

            ApplyServerTimeControl(timeMode, effectiveMode);
        }
    }

    public bool ServerTryCreatePause(out TimeControlEnum previousMode, out long pauseToken)
    {
        lock (timeControlLock)
        {
            bool isFirstAutomaticPause = automaticPauseTokens.Count == 0;
            previousMode = isFirstAutomaticPause
                ? GetTimeControl()
                : automaticPauseResumeMode.Value;
            pauseToken = default;

            if (isFirstAutomaticPause && previousMode == TimeControlEnum.Pause)
            {
                return false;
            }

            pauseToken = ++nextAutomaticPauseToken;
            automaticPauseTokens.Add(pauseToken);

            if (!isFirstAutomaticPause)
            {
                return true;
            }

            automaticPauseResumeMode = previousMode;
            try
            {
                ApplyServerTimeControl(TimeControlEnum.Pause, TimeControlEnum.Pause);
                return true;
            }
            catch
            {
                automaticPauseTokens.Remove(pauseToken);
                automaticPauseResumeMode = null;
                throw;
            }
        }
    }

    public AutomaticPauseRestoreResult ServerTryRestoreTimeControl(
        long pauseToken,
        out TimeControlEnum restoredMode)
    {
        lock (timeControlLock)
        {
            restoredMode = TimeControlEnum.Pause;
            if (!automaticPauseTokens.Contains(pauseToken))
            {
                return AutomaticPauseRestoreResult.Stale;
            }

            if (automaticPauseTokens.Count > 1)
            {
                automaticPauseTokens.Remove(pauseToken);
                return AutomaticPauseRestoreResult.StillPaused;
            }

            var requestedMode = automaticPauseResumeMode.Value;
            var effectiveMode = LimitTimeControl(requestedMode);
            if (effectiveMode == TimeControlEnum.Pause)
            {
                return AutomaticPauseRestoreResult.Blocked;
            }

            automaticPauseTokens.Remove(pauseToken);
            automaticPauseResumeMode = null;
            restoredMode = effectiveMode;
            ApplyServerTimeControl(requestedMode, effectiveMode);
            return AutomaticPauseRestoreResult.Restored;
        }
    }

    private void InvalidateAutomaticPauses()
    {
        automaticPauseTokens.Clear();
        automaticPauseResumeMode = null;
    }

    private void ApplyServerTimeControl(TimeControlEnum requestedMode, TimeControlEnum effectiveMode)
    {
        var currentMode = Campaign.Current == null
            ? (TimeControlEnum?)null
            : GetTimeControl();

        Logger.Information(
            "Applying server time control: current={CurrentMode} requested={RequestedMode} effective={EffectiveMode}",
            currentMode?.ToString() ?? "<unavailable>",
            requestedMode,
            effectiveMode);

        network.SendAll(new NetworkChangeTimeControlMode(effectiveMode));

        ClientSetTimeControl(effectiveMode);
    }
}
