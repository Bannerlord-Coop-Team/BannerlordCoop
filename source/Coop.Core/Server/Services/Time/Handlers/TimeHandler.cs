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
using System.Linq;

namespace Coop.Core.Server.Services.Time.Handlers;

/// <summary>
/// Handles time requests and commanding time controls. Reacts to the registry's loading signal to
/// pause and lock client time controls; the unpause policy itself lives in
/// <see cref="ServerTimeInterface"/>.
/// </summary>
public class TimeHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<TimeHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly ITimeControlInterface timeControlInterface;
    private readonly IConnectionCollection connections;

    public TimeHandler(IMessageBroker messageBroker, INetwork network, ITimeControlInterface timeControlInterface, IConnectionCollection connections)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.timeControlInterface = timeControlInterface;
        this.connections = connections;
        this.messageBroker.Subscribe<LoadingPlayersChanged>(Handle_LoadingPlayersChanged);
        this.messageBroker.Subscribe<TimeSpeedChangedAttempted>(Handle_TimeSpeedChanged);
        this.messageBroker.Subscribe<NetworkRequestTimeSpeedChange>(Handle_NetworkRequestTimeSpeedChange);

        timeControlInterface.AddUnpausePolicy(PlayersLoadingPolicy);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<LoadingPlayersChanged>(Handle_LoadingPlayersChanged);
        messageBroker.Unsubscribe<TimeSpeedChangedAttempted>(Handle_TimeSpeedChanged);
        messageBroker.Unsubscribe<NetworkRequestTimeSpeedChange>(Handle_NetworkRequestTimeSpeedChange);

        timeControlInterface.RemoveUnpausePolicy(PlayersLoadingPolicy);
    }

    /// <summary>
    /// Reacts to the registry's single loading signal: pause and lock client time controls while
    /// any player is loading, and release the lock once none are. The count arrives already
    /// reconciled with the connection states, so no peer bookkeeping is needed here.
    /// </summary>
    internal void Handle_LoadingPlayersChanged(MessagePayload<LoadingPlayersChanged> obj)
    {
        var loadingPlayerCount = obj.What.LoadingPlayerCount;

        if (loadingPlayerCount > 0)
        {
            timeControlInterface.ServerSetTimeControl(TimeControlEnum.Pause);
            network.SendAll(new NetworkTimeControlLockChanged(true, loadingPlayerCount));
            return;
        }

        network.SendAll(new NetworkTimeControlLockChanged(false));
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
        var loadingPeers = connections.LoadingPeers;
        if (loadingPeers.Count() > 0)
        {

            Logger.Information($"{string.Join(",", loadingPeers.Select(p => p.Peer.Address))} are currently loading, unable to change time");
            return false;
        }

        return true;
    }
}
