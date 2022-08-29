using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.SaveSystem.Load;

namespace GameInterface.Data
{
    internal class GameSaveData : IGameSaveData
    {
        public byte[] Data { get; }

        public GameSaveData(byte[] data)
        {
            Data = data;
        }
    }
}
