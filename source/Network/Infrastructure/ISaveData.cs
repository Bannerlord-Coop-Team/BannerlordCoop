using System;

namespace Network.Infrastructure
{
    public interface ISaveData
    {
        bool RequiresCharacterCreation { get; }
        bool Receive(ArraySegment<byte> rawData);
        byte[] SerializeInitialWorldState();
    }
}
