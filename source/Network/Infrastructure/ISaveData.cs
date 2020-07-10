using System;

namespace Network.Infrastructure
{
    public interface ISaveData
    {
        bool RequiresInitialWorldData { get; }
        bool RequiresCharacterCreation { get; }
        bool Receive(ArraySegment<byte> rawData);
        byte[] SerializeInitialWorldState();
    }
}
