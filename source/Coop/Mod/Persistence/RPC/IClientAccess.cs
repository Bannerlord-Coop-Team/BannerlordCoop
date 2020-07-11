using JetBrains.Annotations;
using RailgunNet.Connection.Client;
using Sync.Store;

namespace Coop.Mod.Persistence.RPC
{
    public interface IClientAccess
    {
        [CanBeNull]
        RemoteStore GetStore();

        [CanBeNull]
        RailClientRoom GetRoom();
    }
}
