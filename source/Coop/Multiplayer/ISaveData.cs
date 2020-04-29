using System;

namespace Coop.Multiplayer
{
    public interface ISaveData
    {
        bool Receive(ArraySegment<byte> rawData);
        byte[] SerializeInitialWorldState();
    }
}
