using Common;
using Common.Logging;
using Serilog;
using System;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using Timer = System.Threading.Timer;

namespace GameInterface.Services.GameState;

/// <summary>
/// Shuts the managed server process down. Static because it must run after the session
/// container is gone (nothing to resolve it from), and it only touches process-global
/// state (Campaign, SaveHandler, the quit call).
/// </summary>
public static class ServerShutdown
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(ServerShutdown));

    // Bound the wait for a save that never starts (e.g. the server is not on the map state
    // where SaveTick runs); a save in progress finishes via the completion event, not this.
    private static readonly TimeSpan SaveStartTimeout = TimeSpan.FromSeconds(90);

    private static readonly object listenerOwner = new object();
    private static Timer saveStartTimer;
    private static int quitting;

    /// <summary>
    /// Saves the campaign back to <paramref name="saveName"/>, then quits to desktop when
    /// that save completes; quits directly when there is no campaign to save. Idempotent.
    /// Call on the game thread.
    /// </summary>
    public static void SaveAndQuit(string saveName)
    {
        if (Interlocked.Exchange(ref quitting, 1) != 0) return;

        var saveHandler = Campaign.Current?.SaveHandler;
        if (Game.Current == null || saveHandler == null || saveName == null)
        {
            Quit();
            return;
        }

        // Quit the moment OUR save is written; ignore an autosave that may complete first.
        CampaignEvents.OnSaveOverEvent.AddNonSerializedListener(listenerOwner, (isSuccessful, completedName) =>
        {
            if (!string.Equals(completedName, saveName, StringComparison.OrdinalIgnoreCase)) return;
            Logger.Information("Managed server save '{SaveName}' completed (success={Success}); quitting", saveName, isSuccessful);
            Quit();
        });

        // Arm the fallback before requesting the save so a synchronous SaveAs failure still quits.
        saveStartTimer = new Timer(_ => GameThread.RunSafe(OnSaveTimeout, context: "ServerShutdown"),
            null, SaveStartTimeout, Timeout.InfiniteTimeSpan);

        Logger.Information("Saving managed server campaign to '{SaveName}' before quitting", saveName);
        try
        {
            saveHandler.SaveAs(saveName);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Managed server save request failed; quitting without saving");
            Quit();
        }
    }

    // A save still draining finishes and raises OnSaveOverEvent, so wait it out rather than cut it
    // off mid-write; only force the quit when nothing is saving, i.e. the save never started.
    private static void OnSaveTimeout()
    {
        if (saveStartTimer == null) return;

        if (Campaign.Current?.SaveHandler?.IsSaving == true)
        {
            saveStartTimer.Change(SaveStartTimeout, Timeout.InfiniteTimeSpan);
            return;
        }

        Logger.Warning("Managed server save did not start within {Timeout}; quitting anyway", SaveStartTimeout);
        Quit();
    }

    /// <summary>Quits to desktop without saving. Call on the game thread.</summary>
    public static void QuitToDesktop()
    {
        if (Interlocked.Exchange(ref quitting, 1) != 0) return;
        Quit();
    }

    private static void Quit()
    {
        saveStartTimer?.Dispose();
        saveStartTimer = null;
        CampaignEvents.OnSaveOverEvent.ClearListeners(listenerOwner);
        Utilities.QuitGame();
    }
}
