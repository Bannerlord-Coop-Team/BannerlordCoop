using System;

namespace GameInterface.Services.Save
{
    public interface IGameSaveData
    {
        byte[] Data { get; }
    }

    [Serializable]
    public class GameSaveData : IGameSaveData
    {
        public byte[] Data { get; }

        public GameSaveData(byte[] data)
        {
            Data = data;
        }
    }
}
