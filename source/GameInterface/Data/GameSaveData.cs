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
