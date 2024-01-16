using Common.Messaging;
using GameInterface.Services.GameDebug.Messages;
using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.GameDebug.Handlers;

/// <summary>
/// Handler for managing debug messages
/// </summary>
internal class DebugMessageHandler : IHandler
{
    private readonly IMessageBroker messageBroker;

    public DebugMessageHandler(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<SendInformationMessage>(Handle);
        messageBroker.Subscribe<SendPopupMessage>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SendInformationMessage>(Handle);
        messageBroker.Unsubscribe<SendPopupMessage>(Handle);
    }

    private void Handle(MessagePayload<SendInformationMessage> payload)
    {
        var text = payload.What.Text;
        InformationManager.DisplayMessage(new InformationMessage(text));
    }

    private void Handle(MessagePayload<SendPopupMessage> payload)
    {
        var text = payload.What.Text;
        var inquiry = new InquiryData("Notice", text, 
            isAffirmativeOptionShown: true,
            affirmativeText: "Ok",
            affirmativeAction: null,
            isNegativeOptionShown: false, 
            negativeText: string.Empty,
            negativeAction: null);
        InformationManager.ShowInquiry(inquiry);
    }
}
