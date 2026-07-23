using Coop.Core.Server.States;
using GameInterface.Policies;

namespace Coop.Core.Server.Policies;

/// <summary>
/// Server side sync policy for allowing method calls without syncing
/// Currently the server always requires syncing
/// </summary>
/// <inheritdoc cref="ISyncPolicy"/>
internal class ServerSyncPolicy : ISyncPolicy
{
    private readonly IServerLogic serverLogic;

    public ServerSyncPolicy(IServerLogic serverLogic)
    {
        this.serverLogic = serverLogic;
    }

    public bool AllowOriginal()
    {
        return !(serverLogic.State is ServerRunningState);
    }
}
