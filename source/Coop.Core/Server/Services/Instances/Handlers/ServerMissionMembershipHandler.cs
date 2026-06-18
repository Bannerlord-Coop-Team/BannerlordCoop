using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Instances.Messages;
using GameInterface.Missions.Services.Network.Messages;
using LiteNetLib;

namespace Coop.Core.Server.Services.Instances.Handlers;

/// <summary>
/// Server-side relay membership + join-info introduction. When a client announces it entered an instance
/// (<see cref="MissionEntered"/>) this maps the controller to the connection it arrived on (for the relay
/// fallback) and then introduces the newcomer and the existing members to each other via
/// <see cref="MissionPeerEntered"/> — the trigger that replaces a direct PeerConnected, so each side sends
/// its join info over the mesh. <see cref="MissionLeft"/> drops the controller from the routing table.
/// </summary>
public class ServerMissionMembershipHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IMissionManager missionManager;
    private readonly INetwork network;

    public ServerMissionMembershipHandler(IMessageBroker messageBroker, IMissionManager missionManager, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.missionManager = missionManager;
        this.network = network;

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

        var others = missionManager.EnterMission(peer, message.ControllerId, message.InstanceId);

        // Introduce the newcomer and each existing member to each other so BOTH sides send their join info
        // (replaces the direct PeerConnected trigger). The introduction travels over the campaign/relay
        // connection; the join info itself still flows over the IBattleNetwork mesh.
        foreach (var (otherControllerId, otherPeer) in others)
        {
            network.Send(otherPeer, new MissionPeerEntered(message.ControllerId, message.InstanceId));
            network.Send(peer, new MissionPeerEntered(otherControllerId, message.InstanceId));
        }
    }

    private void Handle_MissionLeft(MessagePayload<MissionLeft> payload)
    {
        var peer = (NetPeer)payload.Who;
        var message = payload.What;

        missionManager.LeaveMission(peer, message.ControllerId, message.InstanceId);
    }
}
