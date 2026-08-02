using Common.Logging;
using Common.Messaging;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using Serilog;

namespace Coop.Core.Server.Services.Time.Handlers;

/// <summary>
/// Handles time requests and commands the authoritative time control.
/// </summary>
public class TimeHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<TimeHandler>();

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
        var peer = obj.Who as NetPeer;

        Logger.Information(
            "Peer requested time control: peer={PeerId} mode={RequestedMode}",
            peer?.Id,
            obj.What.NewControlMode);

        timeControlInterface.ServerSetTimeControl(obj.What.NewControlMode);
    }

    internal void Handle_TimeSpeedChanged(MessagePayload<TimeSpeedChangedAttempted> obj)
    {
        Logger.Information(
            "Local time control requested by {Source}: mode={RequestedMode}",
            obj.Who?.GetType().Name ?? "<unknown>",
            obj.What.NewControlMode);

        timeControlInterface.ServerSetTimeControl(obj.What.NewControlMode);
    }
}
