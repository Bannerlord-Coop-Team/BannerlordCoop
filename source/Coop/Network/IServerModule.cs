using System;

namespace Coop.Network
{
    public interface IServerModule
    {
        void Tick(TimeSpan frameTime);
    }
}
