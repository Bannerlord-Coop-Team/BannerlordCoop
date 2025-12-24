using Common.Messaging;

namespace GameInterface.Services.Save.Messages;

public record PackageGameSaveData : ICommand
{
}

public record GameSaveDataPackaged : IResponse
{
    public byte[] GameSaveData { get; }
    public string CampaignID { get; }

    /// <summary>
    /// GameSaveData will only be created internally as it requires game access
    /// </summary>
    public GameSaveDataPackaged(
        byte[] gameSaveData,
        string campaignID)
    {
        GameSaveData = gameSaveData;
        CampaignID = campaignID;
    }
}
