using System;

namespace Network.Infrastructure
{
    public interface ISaveData
    {
        bool RequiresInitialWorldData { get; }
        bool Receive(ArraySegment<byte> rawData);
        byte[] SerializeInitialWorldState();
    }
}
