using System;

namespace Coop.Mod.Mission
{
    public interface INetworkObject
    {

        Guid NetworkId { get; set; }

        void Destroy(Type type);
    }
}