using Common.Messaging;
using Coop.Core.Server.Services.Instances.Messages;
using LiteNetLib;

namespace Coop.Core.Server.Services.Instances.Handlers;

/// <summary>
/// Server-side relay membership. As clients enter and leave mission instances they announce it with
/// <see cref="MissionEntered"/> / <see cref="MissionLeft"/>; this maps each announcing controller to the
/// connection the message arrived on (<c>payload.Who</c>) so the relay fallback can route a RelayPacket to
/// it. The mapped connection is whichever one the client sends these over — i.e. the one the relay uses.
/// </summary>
public class ServerMissionMembershipHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IMissionManager missionManager;

    public ServerMissionMembershipHandler(IMessageBroker messageBroker, IMissionManager missionManager)
    {
        this.messageBroker = messageBroker;
        this.missionManager = missionManager;

        messageBroker.Subscribe<MissionEntered>(Handle_MissionEntered);
        messageBroker.Subscribe<MissionLeft>(Handle_MissionLeft);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MissionEntered>(Handle_MissionEntered);
        messageBroker.Unsubscribe<MissionLeft>(Handle_MissionLeft);
    }

    private void Handle_MissionEntered(MessagePayload<MissionEntered> payload)
    {
        var peer = (NetPeer)payload.Who;
        var message = payload.What;

        missionManager.EnterMission(peer, message.ControllerId, message.InstanceId);
    }

    private void Handle_MissionLeft(MessagePayload<MissionLeft> payload)
    {
        var peer = (NetPeer)payload.Who;
        var message = payload.What;

        missionManager.LeaveMission(peer, message.ControllerId, message.InstanceId);
    }
}
