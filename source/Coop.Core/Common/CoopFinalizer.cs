using Common;
using Common.Logging;
using Common.Messaging;
using Coop.Core.Common.Services.Connection.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.UI.Interfaces;
using Serilog;
using System;
using System.Threading;

namespace Coop.Core.Common;

/// <summary>
/// Finalizer for the Coop Module by sending finalizing events
/// </summary>
public interface ICoopFinalizer
{
    void Finalize(string closeText);
}

/// <inheritdoc cref="ICoopFinalizer"/>
public class CoopFinalizer : ICoopFinalizer
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopFinalizer>();

    private readonly IMessageBroker messageBroker;
    private readonly ILoadingInterface loadingInterface;

    public CoopFinalizer(IMessageBroker messageBroker, ILoadingInterface loadingInterface)
    {
        this.messageBroker = messageBroker;
        this.loadingInterface = loadingInterface;
    }

    /// <summary>
    /// Sends relevant events to end the coop mod
    /// </summary>
    /// <param name="closeText">Text for ending notification pop-up message</param>
    public void Finalize(string closeText = null)
    {
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
        int hideClaimed = 0;
        void HideLoadingScreenOnce()
        {
            if (Interlocked.Exchange(ref hideClaimed, 1) == 0)
                loadingInterface.HideLoadingScreen();
        }

        try
        {
            GameThread.RunSafe(HideLoadingScreenOnce, blocking: true);
        }
        catch (TimeoutException e)
        {
            // An expired hide never runs, so the hide and the teardown move to the game thread in the same
            // session. The claim skips the hide there when the first copy had already started.
            GameThread.RunSafe(() =>
            {
                HideLoadingScreenOnce();
                EndCoop(closeText);
            });
            Logger.Warning(e, "Hiding the loading screen timed out; coop ends on the game thread instead");
            return;
        }

        EndCoop(closeText);
    }

    private void EndCoop(string closeText)
    {
        // Only show pop-up with valid message
        if (string.IsNullOrEmpty(closeText) == false)
        {
            messageBroker.Publish(this, new SendPopupMessage(closeText));
        }

        messageBroker.Publish(this, new EndCoopMode());
    }
}
