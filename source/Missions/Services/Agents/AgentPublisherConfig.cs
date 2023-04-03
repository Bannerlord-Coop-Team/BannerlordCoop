using System;

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
        
        /// <summary>
        /// The amount of packets to be sent in <see cref="TimeBetweenPackets"/>.
        /// </summary>
        int Packets { get; }

        /// <summary>
        /// The <see cref="TimeSpan"/> between packents.
        /// </summary>
        TimeSpan TimeBetweenPackets { get; }
    }

    /// <inheritdoc />
    public class AgentPublisherConfig : IAgentPublisherConfig
    {
        /// <inheritdoc />
        public int PacketUpdateRate { get => (int)Math.Round(TimeBetweenPackets.TotalMilliseconds / Packets); }

        // TODO: maybe read this from a config file in the future

        /// <inheritdoc />
        public int Packets { get => 30; }

        /// <inheritdoc />
        public TimeSpan TimeBetweenPackets { get => TimeSpan.FromSeconds(1); }
    }
}
