

using Common;
using LiteNetLib;

namespace Coop
{
    public interface ICoopNetwork : IUpdateable, INetEventListener
    {
        void Start();
        void Stop();
    }
}
