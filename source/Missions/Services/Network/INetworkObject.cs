using System;

namespace Missions.Services.Network
{
    public interface INetworkObject
    {
        Guid NetworkId { get; set; }

        void Destroy(Type type);
    }
}
