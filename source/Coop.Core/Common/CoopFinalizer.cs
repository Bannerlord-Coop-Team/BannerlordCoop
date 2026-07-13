using Common.Messaging;
using Coop.Core.Common.Services.Connection.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.UI.Interfaces;

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
        loadingInterface.HideLoadingScreen();

        // Only show pop-up with valid message
        if (string.IsNullOrEmpty(closeText) == false)
        {
            messageBroker.Publish(this, new SendPopupMessage(closeText));
        }

        messageBroker.Publish(this, new EndCoopMode());
    }
}
