

using Common;
using LiteNetLib;

namespace Coop
{
    public interface ICoopNetwork : IUpdateable, INetEventListener
    {
        bool Start();
        void Stop();
    }
}
