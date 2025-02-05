using Common.Logging;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.Heroes.Interfaces;

internal interface ISaveInterface : IGameAbstraction
{
    byte[] SaveCurrentGame();
}

internal class SaveInterface : ISaveInterface
{
    private readonly ILogger Logger = LogManager.GetLogger<SaveInterface>();

    public byte[] SaveCurrentGame()
    {
        // Validation
        if (Game.Current == null) return ReportSaveFailure(nameof(Game.Current));
        if (Campaign.Current == null) return ReportSaveFailure(nameof(Campaign.Current));
        if (Campaign.Current.SaveHandler == null) return ReportSaveFailure(nameof(Campaign.Current.SaveHandler));

        // Logic
        var saveHandler = Campaign.Current.SaveHandler;
        var dataArgs = saveHandler.GetSaveMetaData();
        var metaData = MBSaveLoad.GetSaveMetaData(dataArgs);

        var saveDriver = new CoopInMemSaveDriver();
        Game.Current.Save(metaData, "TransferSave", saveDriver, (SaveResult) => { });

        return saveDriver.Data;
    }

    /// <summary>
    /// Helper method for reporting save failures
    /// </summary>
    /// <param name="nullObjectName">Name of null object</param>
    /// <returns>Empty byte array</returns>
    private byte[] ReportSaveFailure(string nullObjectName)
    {
        Logger.Error($"Failed to package game save. {nullObjectName} was Null.");
        return Array.Empty<byte>();
    }
}
