using JetBrains.Annotations;
using RailgunNet.Connection.Client;
using Sync.Store;

namespace Coop.Mod.Persistence.RemoteAction
{
    /// <summary>
    ///     Interface to access a clients persistence state.
    /// </summary>
    public interface IClientAccess
    {
        [CanBeNull]
        RemoteStoreClient GetStore();

        [CanBeNull]
        RailClientRoom GetRoom();
    }
}