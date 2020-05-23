using System;

namespace Network.Infrastructure
{
    public interface IGameStatePersistence
    {
        void Receive(ArraySegment<byte> buffer);
    }
}
