using Common.Messaging;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Heroes.Messages;

namespace Coop.Core.Server.Services.Time.Handlers;

/// <summary>
/// Handles time requests and commands the authoritative time control.
/// </summary>
public class TimeHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly ITimeControlInterface timeControlInterface;

    public TimeHandler(IMessageBroker messageBroker, ITimeControlInterface timeControlInterface)
    {
        this.messageBroker = messageBroker;
        this.timeControlInterface = timeControlInterface;
        this.messageBroker.Subscribe<TimeSpeedChangedAttempted>(Handle_TimeSpeedChanged);
        this.messageBroker.Subscribe<NetworkRequestTimeSpeedChange>(Handle_NetworkRequestTimeSpeedChange);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<TimeSpeedChangedAttempted>(Handle_TimeSpeedChanged);
        messageBroker.Unsubscribe<NetworkRequestTimeSpeedChange>(Handle_NetworkRequestTimeSpeedChange);
    }

    internal void Handle_NetworkRequestTimeSpeedChange(MessagePayload<NetworkRequestTimeSpeedChange> obj)
    {
        timeControlInterface.ServerSetTimeControl(obj.What.NewControlMode);
    }

    internal void Handle_TimeSpeedChanged(MessagePayload<TimeSpeedChangedAttempted> obj)
    {
        timeControlInterface.ServerSetTimeControl(obj.What.NewControlMode);
    }
}
