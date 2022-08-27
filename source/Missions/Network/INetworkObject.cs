using System;

namespace Missions.Network
{
    public interface INetworkObject
    {
        Guid NetworkId { get; set; }

        void Destroy(Type type);
    }
}