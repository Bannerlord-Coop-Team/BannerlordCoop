using Common.Messaging;
using System;

namespace GameInterface.Services.Heroes.Messages;

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

    /// <summary>
    /// GameSaveData will only be created internally as it requires game access
    /// </summary>
    public GameSaveDataPackaged(
        Guid transactionID,
        byte[] gameSaveData,
        string campaignID)
    {
        TransactionID = transactionID;
        GameSaveData = gameSaveData;
        CampaignID = campaignID;
    }
}
