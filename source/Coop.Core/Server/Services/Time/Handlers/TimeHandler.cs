using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
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
    private readonly ITimeControlInterface timeControlInterface;

    public TimeHandler(IMessageBroker messageBroker, INetwork network, IClientRegistry clientRegistry, ITimeControlInterface timeControlInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.clientRegistry = clientRegistry;
        this.timeControlInterface = timeControlInterface;
        this.messageBroker.Subscribe<TimeSpeedChangedAttempted>(Handle_TimeSpeedChanged);
        this.messageBroker.Subscribe<NetworkRequestTimeSpeedChange>(Handle_NetworkRequestTimeSpeedChange);

        timeControlInterface.AddUnpausePolicy(PlayersLoadingPolicy);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<TimeSpeedChangedAttempted>(Handle_TimeSpeedChanged);
        messageBroker.Unsubscribe<NetworkRequestTimeSpeedChange>(Handle_NetworkRequestTimeSpeedChange);

        timeControlInterface.RemoveUnpausePolicy(PlayersLoadingPolicy);
    }

    internal void Handle_NetworkRequestTimeSpeedChange(MessagePayload<NetworkRequestTimeSpeedChange> obj)
    {
        var newMode = obj.What.NewControlMode;

        timeControlInterface.ServerSetTimeControl(newMode);
    }

    internal void Handle_TimeSpeedChanged(MessagePayload<TimeSpeedChangedAttempted> obj)
    {
        var newMode = obj.What.NewControlMode;

        timeControlInterface.ServerSetTimeControl(newMode);
    }

    private bool PlayersLoadingPolicy()
    {
        if (clientRegistry.PlayersLoading)
        {
            var loadingPeers = clientRegistry.LoadingPeers;
            Logger.Information($"{string.Join(",", loadingPeers.Select(p => p.Address))} are currently loading, unable to change time");
            return false;
        }

        return true;
    }
}
