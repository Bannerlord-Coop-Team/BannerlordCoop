using Common.Messaging;
using GameInterface.Services.UI.Messages;
using System;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.JoinCancel;

/// <summary>
/// View model behind the join-cancel button. Every press publishes <see cref="CancelJoinAttempt"/>;
/// the handler knows whether a teardown is already under way and drops the repeats.
/// </summary>
internal sealed class JoinCancelVM : ViewModel
{
    private readonly IMessageBroker messageBroker;

    private string cancelButtonText;

    public JoinCancelVM(string cancelButtonText)
        : this(cancelButtonText, MessageBroker.Instance)
    {
    }

    public JoinCancelVM(string cancelButtonText, IMessageBroker messageBroker)
    {
        if (messageBroker == null) throw new ArgumentNullException(nameof(messageBroker));

        this.messageBroker = messageBroker;
        this.cancelButtonText = cancelButtonText;
    }

    [DataSourceProperty]
    public string CancelButtonText
    {
        get => cancelButtonText;
        set
        {
            if (cancelButtonText == value) return;

            cancelButtonText = value;
            OnPropertyChanged(nameof(CancelButtonText));
        }
    }

    public void ActionCancel() => messageBroker.Publish(this, new CancelJoinAttempt());
}
