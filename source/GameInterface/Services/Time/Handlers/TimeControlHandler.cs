using Common.Messaging;
using Common.Network;
using GameInterface.Services.Time.Interaces;
using GameInterface.Services.Time.Messages;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Time.Handlers;

internal class TimeControlHandler : IHandler
{
    private readonly ITimeControlInterface timeControlInterface;
    private readonly IMessageBroker messageBroker;

    public TimeControlHandler(
        ITimeControlInterface timeControlInterface,
        IMessageBroker messageBroker,
        INetwork network)
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
    }
}
