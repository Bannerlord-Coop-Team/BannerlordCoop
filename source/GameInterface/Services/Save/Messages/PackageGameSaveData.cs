﻿using Common.Messaging;
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
        public ISet<Guid> ControlledHeros { get; }
        public IReadOnlyDictionary<string, Guid> PartyIds { get; }
        public IReadOnlyDictionary<string, Guid> HeroIds { get; }

        /// <summary>
        /// GameSaveData will only be created internally as it requires game access
        /// </summary>
        public GameSaveDataPackaged(
            Guid transactionID, 
            byte[] gameSaveData, 
            string campaignID,
            ISet<Guid> controlledHeros,
            IReadOnlyDictionary<string, Guid> partyIds,
            IReadOnlyDictionary<string, Guid> heroIds)
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
