using System;

namespace Coop.Network
{
    public interface IGameStatePersistence
    {
        void Receive(ArraySegment<byte> buffer);
    }
}
