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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Interaces;

public interface ITimeControlInterface : IGameAbstraction
{
    void AddUnpausePolicy(Func<bool> policy);
    TimeControlEnum GetTimeControl();
    void RemoveUnpausePolicy(Func<bool> policy);
    void ClientSetTimeControl(TimeControlEnum newMode);
    void ServerSetTimeControl(TimeControlEnum timeMode);
}

internal class TimeControlInterface : ITimeControlInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<TimeControlInterface>();

    private readonly ITimeControlModeConverter modeConverter;
    private readonly List<WeakDelegate> unpausePolicies = new List<WeakDelegate>();
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
        TimePatches.OverrideTimeControlMode(Campaign.Current, modeConverter.Convert(newMode));
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
    /// If any unpause policy fails, unpausing is not allowed
    /// </summary>
    /// <returns>True if unpausing is not allowed, otherwise False</returns>
    private bool UnpauseDisallowed()
    {
        return unpausePolicies
            .Where(policy => policy.IsAlive)
            .Select(policy => policy.Instance as Func<bool>)
            .Where(policy => policy != null)
            .Any(policy => policy() == false);
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

        if (timeMode != TimeControlEnum.Pause && UnpauseDisallowed())
        {
            timeMode = TimeControlEnum.Pause;
        }

        Logger.Verbose("Server changing time to {mode}", timeMode);

        network.SendAll(new NetworkChangeTimeControlMode(timeMode));

        ClientSetTimeControl(timeMode);
    }
}
