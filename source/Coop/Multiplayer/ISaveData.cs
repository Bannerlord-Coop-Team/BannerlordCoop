using Coop.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Multiplayer
{
    public interface ISaveData
    {
        bool Receive(ArraySegment<byte> rawData);
        byte[] SerializeInitialWorldState();
    }
}
