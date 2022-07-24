using JetBrains.Annotations;
using RailgunNet.Connection.Server;
using Sync.Store;

namespace Coop.Mod.Persistence.RemoteAction
{
    /// <summary>
    ///     Interface to access a servers persistence state.
    /// </summary>
    public interface IServerAccess
    {
        [CanBeNull]
        RemoteStoreServer GetStore();

        [CanBeNull]
        RailServerRoom GetRoom();

        [CanBeNull]
        EventBroadcastingQueue GetQueue();
    }
}
