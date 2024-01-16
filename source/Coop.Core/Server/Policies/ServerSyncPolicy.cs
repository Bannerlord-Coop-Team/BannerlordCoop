using GameInterface.Policies;

namespace Coop.Core.Server.Policies;

/// <summary>
/// Server side sync policy for allowing method calls without syncing
/// Currently the server always requires syncing
/// </summary>
/// <inheritdoc cref="ISyncPolicy"/>
internal class ServerSyncPolicy : ISyncPolicy
{
    public bool AllowOriginalCalls => false;

    public bool EnforceSyncing => !AllowOriginalCalls;
}
