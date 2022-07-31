using System;

namespace Coop.Mod.Missions
{
    public interface INetworkObject
    {

        Guid NetworkId { get; set; }

        void Destroy(Type type);
    }
}