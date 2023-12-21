using Common.Messaging;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Heroes.Messages;
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
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SetTimeControlMode>(Handle);
    }

    private void Handle(MessagePayload<SetTimeControlMode> obj)
    {
        var payload = obj.What;
        CampaignTimeControlMode newTimeMode = (CampaignTimeControlMode)payload.NewTimeMode;

        if (newTimeMode == CampaignTimeControlMode.StoppablePlay) { newTimeMode = CampaignTimeControlMode.UnstoppablePlay; }
        if (newTimeMode == CampaignTimeControlMode.StoppableFastForward) { newTimeMode = CampaignTimeControlMode.UnstoppableFastForward; }

        timeControlInterface.SetTimeControl(newTimeMode);

        messageBroker.Respond(obj.Who, new TimeControlModeSet(payload.NewTimeMode));
    }
}
