using Common.Messaging;
using GameInterface.Services.Save;
using System;
using System.Collections.Generic;

namespace GameInterface.Services.GameState.Messages
{
    public readonly struct PackageGameSaveData : ICommand
    {
        public Guid TransactionID { get; }

        public PackageGameSaveData(Guid transactionID)
        {
            TransactionID = transactionID;
        }
    }

    public readonly struct GameSaveDataPackaged : IResponse
    {
        public Guid TransactionID { get; }
        public byte[] GameSaveData { get; }
        public string CampaignID { get; }
        public HashSet<Guid> ControlledHeros { get; }
        public Dictionary<string, Guid> PartyIds { get; }
        public Dictionary<string, Guid> HeroIds { get; }

        /// <summary>
        /// GameSaveData will only be created internally as it requires game access
        /// </summary>
        public GameSaveDataPackaged(
            Guid transactionID, 
            byte[] gameSaveData, 
            string campaignID,
            HashSet<Guid> controlledHeros,
            Dictionary<string, Guid> partyIds,
            Dictionary<string, Guid> heroIds)
        {
            TransactionID = transactionID;
            GameSaveData = gameSaveData;
            CampaignID = campaignID;
            ControlledHeros = controlledHeros;
            PartyIds = partyIds;
            HeroIds = heroIds;
        }
}
}
