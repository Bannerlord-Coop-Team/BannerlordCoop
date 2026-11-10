using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Save.Interfaces;

/// <summary>
/// Runs the named saves a server queues. An implementation accepts or refuses each request, reports
/// whether a save is running, and publishes GameSaveCompleted when a save it accepted ends.
/// </summary>
public interface IHostSaveInterface : IGameAbstraction
{
    /// <summary>True while a save is queued or running.</summary>
    bool IsSaving { get; }

    /// <summary>Queues a named save. False when no campaign is loaded or the host didn't take the request.</summary>
    bool TryQueueSave(string saveName);
}

/// <summary>Queues saves on the campaign's SaveHandler, whose OnSaveEnded patch publishes GameSaveCompleted.</summary>
internal class HostSaveInterface : IHostSaveInterface
{
    public bool IsSaving => Campaign.Current?.SaveHandler?.IsSaving == true;

    public bool TryQueueSave(string saveName)
    {
        var saveHandler = Campaign.Current?.SaveHandler;
        if (saveHandler == null) return false;

        saveHandler.SaveAs(saveName);
        // A host that blocks the native save queue drops the request, so report whether it was queued.
        return saveHandler.IsSaving;
    }
}
