using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
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
        this.messageBroker.Subscribe<PlayerConnected>(Handle_PlayerConnected);
        this.messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        this.messageBroker.Subscribe<PlayerCampaignEntered>(Handle_PlayerCampaignEntered);
        this.messageBroker.Subscribe<TimeSpeedChangedAttempted>(Handle_TimeSpeedChanged);
        this.messageBroker.Subscribe<NetworkRequestTimeSpeedChange>(Handle_NetworkRequestTimeSpeedChange);

        timeControlInterface.AddUnpausePolicy(PlayersLoadingPolicy);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerConnected>(Handle_PlayerConnected);
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Unsubscribe<PlayerCampaignEntered>(Handle_PlayerCampaignEntered);
        messageBroker.Unsubscribe<TimeSpeedChangedAttempted>(Handle_TimeSpeedChanged);
        messageBroker.Unsubscribe<NetworkRequestTimeSpeedChange>(Handle_NetworkRequestTimeSpeedChange);

        timeControlInterface.RemoveUnpausePolicy(PlayersLoadingPolicy);
    }

    internal void Handle_PlayerConnected(MessagePayload<PlayerConnected> obj)
    {
        timeControlInterface.ServerSetTimeControl(TimeControlEnum.Pause);
        network.SendAll(new NetworkTimeControlLockChanged(true, LoadingPlayerCount(minimumWhenLoading: 1)));
    }

    internal void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> obj)
    {
        var disconnectedPeer = obj.What.PlayerId;
        var loadingPlayerCount = LoadingPlayerCount(excludedPeer: disconnectedPeer);
        if (loadingPlayerCount > 0)
        {
            network.SendAll(new NetworkTimeControlLockChanged(true, loadingPlayerCount));
            return;
        }

        network.SendAll(new NetworkTimeControlLockChanged(false));
    }

    internal void Handle_PlayerCampaignEntered(MessagePayload<PlayerCampaignEntered> obj)
    {
        var loadingPlayerCount = LoadingPlayerCount();
        if (loadingPlayerCount > 0)
        {
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
        if (clientRegistry.PlayersLoading)
        {
            var loadingPeers = clientRegistry.LoadingPeers;
            Logger.Information($"{string.Join(",", loadingPeers.Select(p => p.Address))} are currently loading, unable to change time");
            return false;
        }

        return true;
    }

    private int LoadingPlayerCount(NetPeer excludedPeer = null, int minimumWhenLoading = 0)
    {
        var loadingPeers = clientRegistry.LoadingPeers ?? new List<NetPeer>();
        var loadingPlayerCount = loadingPeers.Count(peer => peer != excludedPeer);

        if (loadingPlayerCount == 0 && (clientRegistry.PlayersLoading || minimumWhenLoading > 0))
        {
            return minimumWhenLoading;
        }

        return loadingPlayerCount;
    }
}
