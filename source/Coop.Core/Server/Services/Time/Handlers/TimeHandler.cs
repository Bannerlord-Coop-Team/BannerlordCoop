using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Messages;
using Serilog;
using System.Linq;

namespace Coop.Core.Server.Services.Time.Handlers;

/// <summary>
/// Handles time requests and commanding time controls
/// </summary>
public class TimeHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<TimeHandler>();

    private readonly IMessageBroker _messageBroker;
    private readonly INetwork _network;
    private readonly IClientRegistry _clientRegistry;

    public TimeHandler(IMessageBroker messageBroker, INetwork network, IClientRegistry clientRegistry)
    {
        _messageBroker = messageBroker;
        _network = network;
        _clientRegistry = clientRegistry;
        _messageBroker.Subscribe<AttemptedTimeSpeedChanged>(Handle_TimeSpeedChanged);
        _messageBroker.Subscribe<NetworkRequestTimeSpeedChange>(Handle_NetworkRequestTimeSpeedChange);
    }

    public void Dispose()
    {
        _messageBroker.Unsubscribe<AttemptedTimeSpeedChanged>(Handle_TimeSpeedChanged);
        _messageBroker.Unsubscribe<NetworkRequestTimeSpeedChange>(Handle_NetworkRequestTimeSpeedChange);
    }

    internal void Handle_NetworkRequestTimeSpeedChange(MessagePayload<NetworkRequestTimeSpeedChange> obj)
    {
        if (AnyLoaders()) return;

        var newMode = obj.What.NewControlMode;

        SetTimeMode(newMode);
    }

    internal void Handle_TimeSpeedChanged(MessagePayload<AttemptedTimeSpeedChanged> obj)
    {
        if (AnyLoaders()) return;

        var newMode = obj.What.NewControlMode;

        SetTimeMode(newMode);
    }

    private bool AnyLoaders()
    {
        if (_clientRegistry.PlayersLoading)
        {
            var loadingPeers = _clientRegistry.LoadingPeers;
            Logger.Information($"{string.Join(",", loadingPeers.Select(p => p.EndPoint.ToString()))} are currently loading, unable to change time");
            return true;
        }

        return false;
    }

    public void SetTimeMode(TimeControlEnum timeMode)
    {
        Logger.Verbose("Server changing time to {mode}", timeMode);

        _messageBroker.Publish(this, new SetTimeControlMode(timeMode));
        _network.SendAll(new NetworkTimeSpeedChanged(timeMode));
    }
}
