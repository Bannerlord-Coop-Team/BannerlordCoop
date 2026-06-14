using Common.Logging;
using Coop.Core.Server.Connections;
using GameInterface.Services.Heroes.Interaces;
using Serilog;
using System;
using System.Linq;

namespace Coop.Core.Server.Services.Time;

/// <summary>
/// Owns the server-side time control policy that prevents the world from being un-paused while
/// any connection is still loading.
/// </summary>
public interface IServerTimeInterface : IDisposable
{
}

internal class ServerTimeInterface : IServerTimeInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerTimeInterface>();

    private readonly ITimeControlInterface timeControlInterface;
    private readonly IConnectionCollection clientRegistry;

    public ServerTimeInterface(ITimeControlInterface timeControlInterface, IConnectionCollection clientRegistry)
    {
        this.timeControlInterface = timeControlInterface;
        this.clientRegistry = clientRegistry;

        timeControlInterface.AddUnpausePolicy(PlayersLoadingPolicy);
    }

    public void Dispose()
    {
        timeControlInterface.RemoveUnpausePolicy(PlayersLoadingPolicy);
    }

    private bool PlayersLoadingPolicy()
    {
        var loadingPeers = clientRegistry.LoadingPeers;
        if (loadingPeers.Count() > 0)
        {
            
            Logger.Information($"{string.Join(",", loadingPeers.Select(p => p.Peer.Address))} are currently loading, unable to change time");
            return false;
        }

        return true;
    }
}
