using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Sessions;

namespace Missions.Sessions;

/// <summary>
/// Sends a mod's messages over the mission network, the way the battle parts send theirs: reliable and ordered,
/// peer to peer or through the relay. Refuses while no session mission is live.
/// </summary>
internal sealed class SessionModChannel : ISessionModChannel
{
    private readonly IBattleNetwork network;

    public SessionModChannel(IBattleNetwork network)
    {
        this.network = network;
    }

    public bool SendToSession(IMessage message)
    {
        if (message == null || !IsSessionMissionLive()) return false;

        network.SendAll(message);
        return true;
    }

    public bool SendToController(string controllerId, IMessage message)
    {
        if (message == null || string.IsNullOrEmpty(controllerId) || !IsSessionMissionLive()) return false;

        network.Send(controllerId, message);
        return true;
    }

    private static bool IsSessionMissionLive() => SessionInstanceId.IsSession(BattleSpawnGate.ActiveMapEventId);
}
