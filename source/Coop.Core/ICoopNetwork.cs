

using Common;
using LiteNetLib;

namespace Coop.Core
{
    public interface ICoopNetwork : IUpdateable, INetEventListener
    {
        void Start();
        void Stop();
    }
}
