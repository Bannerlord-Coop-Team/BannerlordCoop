

using Common;
using Coop.Core.Communication.PacketHandlers;
using LiteNetLib;

namespace Coop.Core
{
    public interface ICoopNetwork : IUpdateable, INetEventListener
    {
        IPacketManager PacketManager { get; }
        void Start();
        void Stop();
    }
}
