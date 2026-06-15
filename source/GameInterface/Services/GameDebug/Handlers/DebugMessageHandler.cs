using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.GameDebug.Messages;
using Serilog;
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
    private static readonly ILogger Logger = LogManager.GetLogger<DebugMessageHandler>();

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
        // DisplayMessage touches UI, so run it on the main thread. No campaign guard: these
        // messages also fire outside a loaded campaign (connecting, disconnected, etc.).
        GameLoopRunner.RunOnMainThread(() =>
        {
            try
            {
                InformationManager.DisplayMessage(new InformationMessage(text));
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to display information message");
            }
        });
    }

    private void Handle(MessagePayload<SendPopupMessage> payload)
    {
        var text = payload.What.Text;
        // ShowInquiry pushes a screen/layer, which is only safe on the main thread. No campaign
        // guard: these popups also fire outside a loaded campaign (e.g. "Server has been stopped").
        GameLoopRunner.RunOnMainThread(() =>
        {
            try
            {
                var inquiry = new InquiryData("Notice", text,
                    isAffirmativeOptionShown: true,
                    affirmativeText: "Ok",
                    affirmativeAction: null,
                    isNegativeOptionShown: false,
                    negativeText: string.Empty,
                    negativeAction: null);
                InformationManager.ShowInquiry(inquiry);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to show popup message");
            }
        });
    }
}
