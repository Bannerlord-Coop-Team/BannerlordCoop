using JetBrains.Annotations;
using RailgunNet.Connection.Client;
using Sync.Store;

namespace Coop.Mod.Persistence.RPC
{
    /// <summary>
    ///     Interface to access a clients persistence state. 
    /// </summary>
    public interface IClientAccess
    {
        [CanBeNull]
        RemoteStore GetStore();

        [CanBeNull]
        RailClientRoom GetRoom();
    }
}
