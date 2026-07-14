using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using Missions.Agents.Messages;
using Serilog;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

/// <summary>
/// Replicates player battle-order voices between mission clients.
/// </summary>
public interface IAgentVoiceHandler : IHandler
{
}

/// <inheritdoc cref="IAgentVoiceHandler"/>
public class AgentVoiceHandler : IAgentVoiceHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<AgentVoiceHandler>();
    private readonly INetworkAgentRegistry agentRegistry;
    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private bool disposed;

    public AgentVoiceHandler(
        INetworkAgentRegistry agentRegistry,
        IBattleNetwork network,
        IMessageBroker messageBroker)
    {
        this.agentRegistry = agentRegistry;
        this.network = network;
        this.messageBroker = messageBroker;

        messageBroker.Subscribe<AgentVoicePlayed>(HandleLocalVoice);
        messageBroker.Subscribe<NetworkAgentVoicePlayed>(HandleNetworkVoice);
    }

    private void HandleLocalVoice(MessagePayload<AgentVoicePlayed> payload)
    {
        AgentVoicePlayed voice = payload.What;
        if (Mission.Current == null || voice.Agent != Mission.Current.MainAgent) return;
        if (!agentRegistry.IsLocallyControlled(voice.Agent)) return;

        if (!agentRegistry.TryGetAgentInfo(voice.Agent, out var agentInfo))
        {
            Logger.Warning("Failed to find the local player agent for order voice {VoiceTypeId}", voice.VoiceTypeId);
            return;
        }

        network.SendAll(new NetworkAgentVoicePlayed(agentInfo.AgentId, voice.VoiceTypeId));
    }

    private void HandleNetworkVoice(MessagePayload<NetworkAgentVoicePlayed> payload)
    {
        NetworkAgentVoicePlayed voice = payload.What;
        if (string.IsNullOrEmpty(voice.VoiceTypeId)) return;

        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;
            if (agentRegistry.IsLocallyControlled(voice.AgentId)) return;

            if (!agentRegistry.TryGetAgentInfo(voice.AgentId, out var agentInfo))
            {
                Logger.Warning("Failed to find agent {AgentId} for order voice {VoiceTypeId}",
                    voice.AgentId, voice.VoiceTypeId);
                return;
            }

            Agent agent = agentInfo.Agent;
            if (agent == null || agent.Mission != Mission.Current || !agent.IsActive()) return;

            using (new AllowedThread())
            {
                agent.MakeVoice(
                    new SkinVoiceManager.SkinVoiceType(voice.VoiceTypeId),
                    SkinVoiceManager.CombatVoiceNetworkPredictionType.NoPrediction);
            }
        });
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        messageBroker.Unsubscribe<AgentVoicePlayed>(HandleLocalVoice);
        messageBroker.Unsubscribe<NetworkAgentVoicePlayed>(HandleNetworkVoice);
    }
}
