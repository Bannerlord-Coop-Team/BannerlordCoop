using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using Missions.Agents.Messages;
using Missions.Agents.Voice;
using Serilog;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers;

/// <summary>
/// Selects and replicates exact vanilla player battle-order recordings between mission clients.
/// </summary>
public interface IAgentVoiceHandler : IHandler
{
    void WarmUp();
    void PollVoices();
}

/// <inheritdoc cref="IAgentVoiceHandler"/>
public class AgentVoiceHandler : IAgentVoiceHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<AgentVoiceHandler>();
    private readonly INetworkAgentRegistry agentRegistry;
    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IVanillaOrderVoiceService voiceService;
    private bool disposed;

    public AgentVoiceHandler(
        INetworkAgentRegistry agentRegistry,
        IBattleNetwork network,
        IMessageBroker messageBroker,
        IVanillaOrderVoiceService voiceService)
    {
        this.agentRegistry = agentRegistry;
        this.network = network;
        this.messageBroker = messageBroker;
        this.voiceService = voiceService;

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

        string sampleName = null;
        if (voiceService.TrySelectClip(
                voice.Agent.GetAgentVoiceDefinition(),
                voice.VoiceTypeId,
                out var clip) &&
            voiceService.TryPlay(voice.Agent, clip))
        {
            sampleName = clip.SampleName;
            voice.Handled = true;
            Logger.Debug("Playing local order voice sample {SampleName} for {VoiceTypeId}",
                sampleName, voice.VoiceTypeId);
        }

        network.SendAll(new NetworkAgentVoicePlayed(agentInfo.AgentId, voice.VoiceTypeId, sampleName));
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

            if (!string.IsNullOrEmpty(voice.SampleName) &&
                voiceService.TryGetClip(
                    voice.SampleName,
                    voice.VoiceTypeId,
                    agent.GetAgentVoiceDefinition(),
                    out var clip) &&
                voiceService.TryPlay(agent, clip))
            {
                Logger.Debug("Playing network order voice sample {SampleName} for agent {AgentId}",
                    voice.SampleName, voice.AgentId);
                return;
            }

            if (!string.IsNullOrEmpty(voice.SampleName))
            {
                Logger.Warning("Failed to play exact order voice sample {SampleName} for agent {AgentId}; using vanilla voice {VoiceTypeId}",
                    voice.SampleName, voice.AgentId, voice.VoiceTypeId);
            }

            using (new AllowedThread())
            {
                agent.MakeVoice(
                    new SkinVoiceManager.SkinVoiceType(voice.VoiceTypeId),
                    SkinVoiceManager.CombatVoiceNetworkPredictionType.NoPrediction);
            }
        });
    }

    public void PollVoices()
    {
        voiceService.Tick();
    }

    public void WarmUp()
    {
        voiceService.WarmUp();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        messageBroker.Unsubscribe<AgentVoicePlayed>(HandleLocalVoice);
        messageBroker.Unsubscribe<NetworkAgentVoicePlayed>(HandleNetworkVoice);
        voiceService.Dispose();
    }
}
