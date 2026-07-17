using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Heroes.Messages;
using Serilog;

namespace Coop.Core.Server.Services.Time.Handlers;

/// <summary>
/// Handles time requests and commanding time controls. Reacts to the registry's loading signal to
/// lock player time controls while a joining client catches up without stopping authoritative time.
/// </summary>
public class TimeHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<TimeHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly ITimeControlInterface timeControlInterface;
    private volatile int loadingPlayerCount;

    public TimeHandler(IMessageBroker messageBroker, INetwork network, ITimeControlInterface timeControlInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.timeControlInterface = timeControlInterface;
        this.messageBroker.Subscribe<LoadingPlayersChanged>(Handle_LoadingPlayersChanged);
        this.messageBroker.Subscribe<TimeSpeedChangedAttempted>(Handle_TimeSpeedChanged);
        this.messageBroker.Subscribe<NetworkRequestTimeSpeedChange>(Handle_NetworkRequestTimeSpeedChange);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<LoadingPlayersChanged>(Handle_LoadingPlayersChanged);
        messageBroker.Unsubscribe<TimeSpeedChangedAttempted>(Handle_TimeSpeedChanged);
        messageBroker.Unsubscribe<NetworkRequestTimeSpeedChange>(Handle_NetworkRequestTimeSpeedChange);
    }

    /// <summary>
    /// Locks player time-control requests while anyone is loading, but leaves authoritative campaign
    /// time running so existing players can continue. The joining peer receives the intervening world
    /// stream from <see cref="ConnectionMessageQueue"/> before its loading screen is released.
    /// </summary>
    internal void Handle_LoadingPlayersChanged(MessagePayload<LoadingPlayersChanged> obj)
    {
        loadingPlayerCount = obj.What.LoadingPlayerCount;

        if (loadingPlayerCount > 0)
        {
            network.SendAll(new NetworkTimeControlLockChanged(true, loadingPlayerCount));
            return;
        }

        network.SendAll(new NetworkTimeControlLockChanged(false));
    }

    internal void Handle_NetworkRequestTimeSpeedChange(MessagePayload<NetworkRequestTimeSpeedChange> obj)
    {
        TrySetRequestedTime(obj.What.NewControlMode);
    }

    internal void Handle_TimeSpeedChanged(MessagePayload<TimeSpeedChangedAttempted> obj)
    {
        TrySetRequestedTime(obj.What.NewControlMode);
    }

    private void TrySetRequestedTime(TimeControlEnum newMode)
    {
        if (loadingPlayerCount > 0)
        {
            Logger.Information("{LoadingPlayerCount} player(s) are loading, unable to change time", loadingPlayerCount);
            return;
        }

        timeControlInterface.ServerSetTimeControl(newMode);
    }
}
