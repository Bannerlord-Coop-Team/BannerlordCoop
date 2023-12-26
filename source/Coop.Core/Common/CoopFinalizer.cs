using Common.Messaging;
using Coop.Core.Common.Services.Connection.Messages;
using GameInterface.Services.GameDebug.Messages;

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

    public CoopFinalizer(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
    }

    /// <summary>
    /// Sends relevant events to end the coop mod
    /// </summary>
    /// <param name="closeText">Text for ending notification pop-up message</param>
    public void Finalize(string closeText = null)
    {
        // Only show pop-up with valid message
        if (string.IsNullOrEmpty(closeText) == false)
        {
            messageBroker.Publish(this, new SendPopupMessage(closeText));
        }
        
        messageBroker.Publish(this, new EndCoopMode());
    }
}
