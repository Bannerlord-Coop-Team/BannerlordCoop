using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using Missions.Agents.Messages;
using Missions.Agents.Voice;
using Serilog;
using System;
using System.Collections.Generic;
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
    private readonly Queue<PendingLocalVoice> pendingLocalVoices = new Queue<PendingLocalVoice>();
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

        if (voiceService.TrySelectClip(
                voice.Agent.GetAgentVoiceDefinition(),
                voice.VoiceTypeId,
                out var clip))
        {
            voice.Handled = true;
            // Leave the Agent.MakeVoice prefix before creating the native sound event.
            pendingLocalVoices.Enqueue(new PendingLocalVoice(
                voice.Agent,
                agentInfo.AgentId,
                voice.VoiceTypeId,
                clip));
            return;
        }

        network.SendAll(new NetworkAgentVoicePlayed(agentInfo.AgentId, voice.VoiceTypeId, null));
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

        while (pendingLocalVoices.Count > 0)
        {
            PendingLocalVoice pending = pendingLocalVoices.Dequeue();
            Agent agent = pending.Agent;
            if (Mission.Current == null || agent == null || agent.Mission != Mission.Current || !agent.IsActive())
                continue;

            string sampleName = null;
            if (voiceService.TryPlay(agent, pending.Clip))
            {
                sampleName = pending.Clip.SampleName;
                Logger.Debug("Playing local order voice sample {SampleName} for {VoiceTypeId}",
                    sampleName, pending.VoiceTypeId);
            }
            else
            {
                using (new AllowedThread())
                {
                    agent.MakeVoice(
                        new SkinVoiceManager.SkinVoiceType(pending.VoiceTypeId),
                        SkinVoiceManager.CombatVoiceNetworkPredictionType.NoPrediction);
                }
            }

            network.SendAll(new NetworkAgentVoicePlayed(
                pending.AgentId,
                pending.VoiceTypeId,
                sampleName));
        }
    }

    public void WarmUp()
    {
        voiceService.WarmUp();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        pendingLocalVoices.Clear();
        messageBroker.Unsubscribe<AgentVoicePlayed>(HandleLocalVoice);
        messageBroker.Unsubscribe<NetworkAgentVoicePlayed>(HandleNetworkVoice);
        voiceService.Dispose();
    }

    private sealed class PendingLocalVoice
    {
        public Agent Agent { get; }
        public Guid AgentId { get; }
        public string VoiceTypeId { get; }
        public VanillaOrderVoiceClip Clip { get; }

        public PendingLocalVoice(
            Agent agent,
            Guid agentId,
            string voiceTypeId,
            VanillaOrderVoiceClip clip)
        {
            Agent = agent;
            AgentId = agentId;
            VoiceTypeId = voiceTypeId;
            Clip = clip;
        }
    }
}
