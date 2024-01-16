using Common.Messaging;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Heroes.Messages;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Handlers;

internal class TimeControlHandler : IHandler
{
    private readonly ITimeControlInterface timeControlInterface;
    private readonly IMessageBroker messageBroker;

    public TimeControlHandler(
        ITimeControlInterface timeControlInterface,
        IMessageBroker messageBroker)
    {
        this.timeControlInterface = timeControlInterface;
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<SetTimeControlMode>(Handle);
        messageBroker.Subscribe<GetTimeControlMode>(Handle);
    }

    private void Handle(MessagePayload<GetTimeControlMode> payload)
    {
        var mode = timeControlInterface.GetTimeControl();

        messageBroker.Respond(payload.Who, new TimeControlModeResponse(mode));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SetTimeControlMode>(Handle);
        messageBroker.Unsubscribe<GetTimeControlMode>(Handle);
    }

    private void Handle(MessagePayload<SetTimeControlMode> obj)
    {
        var payload = obj.What;

        timeControlInterface.SetTimeControl(payload.NewTimeMode);

        messageBroker.Respond(obj.Who, new TimeControlModeSet(payload.NewTimeMode));
    }
}
