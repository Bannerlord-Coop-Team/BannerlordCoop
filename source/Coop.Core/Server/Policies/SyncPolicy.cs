using GameInterface.Policies;

namespace Coop.Core.Server.Policies;

internal class SyncPolicy : ISyncPolicy
{
    public bool AllowOriginalCalls => false;
}
