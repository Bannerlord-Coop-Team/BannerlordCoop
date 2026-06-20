using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;

namespace Missions;

/// <summary>
/// The <see cref="INetwork"/> used inside a mission (battles, arenas, taverns, board games).
/// Distinguishes the mission-scoped P2P network from any other <see cref="INetwork"/> so mission
/// services bind to it explicitly.
/// </summary>
public interface IBattleNetwork
{
    void ConnectToInstance(string instanceId);
    void Start();
    void Stop();
    void Send(string controllerId, IPacket packet);
    void SendAll(IPacket packet);
    void SendAllBut(string controllerId, IPacket packet);
    void Send(string controllerId, IMessage message);
    void SendAll(IMessage message);
    void SendAllBut(string controllerId, IMessage message);
}
