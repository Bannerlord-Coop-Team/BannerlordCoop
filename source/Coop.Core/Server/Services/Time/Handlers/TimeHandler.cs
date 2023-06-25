using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.Heroes.Messages;
using Serilog;

namespace Coop.Core.Server.Services.Time.Handlers;

/// <summary>
/// Handles time requests and commanding time controls
/// </summary>
public class TimeHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<TimeHandler>();

    private readonly IMessageBroker _messageBroker;
    private readonly INetwork _network;

    public TimeHandler(IMessageBroker messageBroker, INetwork network)
    {
        _messageBroker = messageBroker;
        _network = network;
        _messageBroker.Subscribe<TimeSpeedChanged>(Handle_TimeSpeedChanged);
        _messageBroker.Subscribe<NetworkRequestTimeSpeedChange>(Handle_NetworkRequestTimeSpeedChange);
    }

    public void Dispose()
    {
        _messageBroker.Unsubscribe<TimeSpeedChanged>(Handle_TimeSpeedChanged);
        _messageBroker.Unsubscribe<NetworkRequestTimeSpeedChange>(Handle_NetworkRequestTimeSpeedChange);
    }

    internal void Handle_NetworkRequestTimeSpeedChange(MessagePayload<NetworkRequestTimeSpeedChange> obj)
    {
        var newMode = obj.What.NewControlMode;

        Logger.Verbose("Server changing time to {mode} from client", newMode);

        _messageBroker.Publish(this, new SetTimeControlMode(newMode));
    }

    internal void Handle_TimeSpeedChanged(MessagePayload<TimeSpeedChanged> obj)
    {
        var newMode = obj.What.NewControlMode;

        Logger.Verbose("Server sending time change to {mode} to client", newMode);

        _network.SendAll(new NetworkTimeSpeedChanged(newMode));
    }
}
