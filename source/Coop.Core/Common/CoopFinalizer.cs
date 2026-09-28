using Common;
using Common.Messaging;
using Coop.Core.Common.Services.Connection.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.UI.Interfaces;
using System.Threading;

namespace Coop.Core.Common;

/// <summary>
/// Finalizer for the Coop Module by sending finalizing events
/// </summary>
public interface ICoopFinalizer
{
    void Finalize(string closeText);

    /// <summary>
    /// Makes every later <see cref="Finalize"/> in this session show <paramref name="closeText"/> instead of its own text.
    /// </summary>
    void SetCloseText(string closeText);

    /// <summary>
    /// Shows the <see cref="SetCloseText"/> text once for a session that ended without a <see cref="Finalize"/> showing it.
    /// </summary>
    void ShowCloseText();
}

/// <inheritdoc cref="ICoopFinalizer"/>
public class CoopFinalizer : ICoopFinalizer
{
    private readonly IMessageBroker messageBroker;
    private readonly ILoadingInterface loadingInterface;
    // Set on the network thread and read by a finalize on the game thread.
    private volatile string closeTextOverride;
    // Set when a finalize or ShowCloseText reaches its popup, so ShowCloseText never shows a second one.
    private int closeTextShown;

    public CoopFinalizer(IMessageBroker messageBroker, ILoadingInterface loadingInterface)
    {
        this.messageBroker = messageBroker;
        this.loadingInterface = loadingInterface;
    }

    public void SetCloseText(string closeText)
    {
        closeTextOverride = closeText;
    }

    public void ShowCloseText()
    {
        if (Interlocked.Exchange(ref closeTextShown, 1) != 0) return;

        string closeText = closeTextOverride;
        if (string.IsNullOrEmpty(closeText) == false)
        {
            messageBroker.Publish(this, new SendPopupMessage(closeText));
        }
    }

    /// <summary>
    /// Sends relevant events to end the coop mod
    /// </summary>
    /// <param name="closeText">Text for ending notification pop-up message</param>
    public void Finalize(string closeText = null)
    {
        closeText = closeTextOverride ?? closeText;

        // A join/load flow may have force-shown the global loading window (ILoadingInterface keeps
        // it up across state transitions via the static LoadingWindowPatches.ForceLoadingWindow
        // flag, which even blocks native disables and survives the container teardown below).
        // Coop ending must always release it, or a pre-campaign teardown — e.g. a client whose
        // module validation was denied — leaves the player stuck on the loading screen forever,
        // with the pop-up explaining why hidden behind it. No-op when no loading window exists
        // (headless server).
        //
        // HideLoadingScreen disables the native Gauntlet loading window, so it must run on the game
        // thread. Finalize is reached from the network poller thread as well (a client state's
        // message handler denying validation runs there), so marshal it. Blocking so the screen is
        // down before the teardown messages below, and inline (no marshal) when already on the game
        // thread — e.g. the validation-timeout path.
        GameThread.RunSafe(loadingInterface.HideLoadingScreen, blocking: true);

        // After the marshal above, which throws once the session is cancelled, and before EndCoopMode,
        // whose teardown can wake a caller that shows the close text itself.
        Interlocked.Exchange(ref closeTextShown, 1);

        // Only show pop-up with valid message
        if (string.IsNullOrEmpty(closeText) == false)
        {
            messageBroker.Publish(this, new SendPopupMessage(closeText));
        }

        messageBroker.Publish(this, new EndCoopMode());
    }
}
