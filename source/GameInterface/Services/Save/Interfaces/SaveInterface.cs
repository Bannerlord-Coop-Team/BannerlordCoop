using Common.Logging;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;
using TaleWorlds.SaveSystem;

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
        EnsureUsableGameVersion(metaData);

        // Required to properly transfer campaign behaviors
        CampaignEventDispatcher.Instance.OnBeforeSave();

        var saveDriver = new CoopInMemSaveDriver();
        Game.Current.Save(metaData, "TransferSave", saveDriver, (SaveResult) => { });

        return new SaveResults(true, saveDriver.Data, Campaign.Current?.UniqueGameId);
    }

    /// <summary>
    /// Headless hosts (the dedicated-server engine) ship no
    /// Parameters/Version.xml, so MBSaveLoad.CurrentVersion — and with it the
    /// "ApplicationVersion" this metadata was just stamped with — is Empty
    /// (i-1.-1.-1.-1). A client loading the transferred save then treats it as
    /// pre-1.3.0 and runs per-object legacy migration into members that don't
    /// exist in a modern save (MobileParty.OnLateLoad NRE). Re-stamp unusable
    /// version entries with the Native module's version, which every host has.
    /// </summary>
    private void EnsureUsableGameVersion(MetaData metaData)
    {
        if (GetVersion(metaData, "ApplicationVersion").Major > 0) return;

        var nativeVersion = ModuleHelper.GetModuleInfo("Native")?.Version ?? ApplicationVersion.Empty;
        if (nativeVersion.Major <= 0)
        {
            Logger.Error("Save metadata has no usable game version and the Native module version is unusable too; " +
                "clients will fail to load the transferred save.");
            return;
        }

        metaData["ApplicationVersion"] = nativeVersion.ToString();
        Logger.Information("Repaired transfer-save game version to {Version} (host had none)", nativeVersion);

        // The creation-version entry is only poisoned when the campaign itself was
        // created on a version-less host; repair it by the same rule.
        if (GetVersion(metaData, "NewGameVersion").Major <= 0)
        {
            metaData["NewGameVersion"] = nativeVersion.ToString();
        }
    }

    private static ApplicationVersion GetVersion(MetaData metaData, string key)
    {
        try
        {
            if (metaData.TryGetValue(key, out string value) && !string.IsNullOrEmpty(value))
            {
                return ApplicationVersion.FromString(value);
            }
        }
        catch (Exception)
        {
            // unparsable — treat as unusable
        }
        return ApplicationVersion.Empty;
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
