namespace Missions.Services.Agents
{
    /// <summary>
    /// Contains configuration for <see cref="AgentPublisher"/>.
    /// </summary>
    public interface IAgentPublisherConfig
    {
        /// <summary>
        /// The rate at which packets are getting updated.
        /// </summary>
        int PacketUpdateRate { get; }
    }

    /// <inheritdoc />
    public class AgentPublisherConfig : IAgentPublisherConfig
    {
        /// <inheritdoc />
        public int PacketUpdateRate { get; set; }
    }
}
