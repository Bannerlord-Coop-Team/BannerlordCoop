using System;

namespace Coop.Multiplayer
{
    public interface ISaveData
    {
        bool RequiresInitialWorldData { get; }
        bool Receive(ArraySegment<byte> rawData);
        byte[] SerializeInitialWorldState();
    }
}
