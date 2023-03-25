using Common.Messaging;
using System;

namespace GameInterface.Services.GameState.Messages
{
    public readonly struct LoadGameSave : ICommand
    {
        public Guid TransactionID { get; }

        public byte[] SaveData { get; }

        public LoadGameSave(Guid transactionID, byte[] saveData)
        {
            TransactionID = transactionID;
            SaveData = saveData;
        }
    }

    public readonly struct GameSaveLoaded : IResponse
    {
        public Guid TransactionID { get; }

        public GameSaveLoaded(Guid transactionID)
        {
            TransactionID = transactionID;
        }
    }
}
