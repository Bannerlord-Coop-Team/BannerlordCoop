using Coop.Multiplayer;
using Coop.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Game
{
    class WorldData : IWorldData
    {
        enum EData
        {
            WorldState
        }
        public bool Receive(byte[] rawData)
        {
            ByteReader reader = new ByteReader(rawData);
            EData eData = (EData)reader.Binary.ReadInt32();
            switch(eData)
            {
                case EData.WorldState:
                    return true;
                default:
                    return false;
            }
        }

        public byte[] SerializeWorldState()
        {
            ByteWriter writer = new ByteWriter();
            writer.Binary.Write((int)EData.WorldState);
            return writer.ToArray();
        }
    }
}
