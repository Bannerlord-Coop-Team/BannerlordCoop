using Common.Network;

namespace Missions.Services.Network
{
    /// <summary>
    /// The <see cref="INetwork"/> used inside a mission (battles, arenas, taverns, board games).
    /// Distinguishes the mission-scoped P2P network from any other <see cref="INetwork"/> so mission
    /// services bind to it explicitly.
    /// </summary>
    public interface IMissionNetwork : INetwork
    {
    }
}
