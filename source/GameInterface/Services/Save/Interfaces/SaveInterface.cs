using Common;
using Common.Logging;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Heroes.Interfaces;

public interface ISaveInterface : IGameAbstraction
{
    SaveResults SaveCurrentGame();
}

internal class SaveInterface : ISaveInterface
{
    private readonly ILogger Logger = LogManager.GetLogger<SaveInterface>();

    public SaveResults SaveCurrentGame()
    {
        // Validation
        if (Game.Current == null) return ReportSaveFailure(nameof(Game.Current));
        if (Campaign.Current == null) return ReportSaveFailure(nameof(Campaign.Current));
        if (Campaign.Current.SaveHandler == null) return ReportSaveFailure(nameof(Campaign.Current.SaveHandler));

        // Logic
        var saveHandler = Campaign.Current.SaveHandler;
        var dataArgs = saveHandler.GetSaveMetaData();
        var metaData = MBSaveLoad.GetSaveMetaData(dataArgs);

        // Required to properly transfer campaign behaviors
        CampaignEventDispatcher.Instance.OnBeforeSave();

        var saveDriver = new CoopInMemSaveDriver();
        Game.Current.Save(metaData, "TransferSave", saveDriver, (SaveResult) => { });

        return new SaveResults(true, saveDriver.Data, Campaign.Current?.UniqueGameId);
    }

    /// <summary>
    /// Helper method for reporting save failures
    /// </summary>
    /// <param name="nullObjectName">Name of null object</param>
    /// <returns>Empty byte array</returns>
    private SaveResults ReportSaveFailure(string nullObjectName)
    {
        Logger.Error($"Failed to package game save. {nullObjectName} was Null.");
        return new SaveResults(false, Array.Empty<byte>(), null);
    }
}

public class SaveResults
{
    public bool Success { get; }
    public byte[] Data { get; }
    public string CampaignId { get; }
    public SaveResults(bool success, byte[] data, string saveId)
    {
        Success = success;
        Data = data;
        CampaignId = saveId;
    }
}
