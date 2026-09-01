using Common;
using GameInterface.Services.Entity;
using GameInterface.Services.Locations;
using Missions;
using Missions.Locations;
using Moq;
using System;
using System.Threading;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.MountAndBlade;
using FormatterServices = System.Runtime.Serialization.FormatterServices;

namespace E2E.Tests.Services.Locations;

/// <summary>Checks remote party puppet identity ordering against the population replay.</summary>
public class LocationRemotePartySpawnOrderingTests
{
    [Fact]
    public void SpawnAndPopulationReplayInSameDrain_ResolverSeesRemotePuppet()
    {
        using var registry = new NetworkAgentRegistry(Mock.Of<IControllerIdProvider>());
        var partyAgentMap = new LocationPartyAgentMap();
        var partyPuppetRegistrar = new LocationPartyPuppetRegistrar();
        var agentId = Guid.NewGuid();
        var remotePuppet = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
        remotePuppet.Origin = (SimpleAgentOrigin)FormatterServices.GetUninitializedObject(typeof(SimpleAgentOrigin));
        partyAgentMap.Record(agentId);
        LocationNpcGate.BeginMission(
            "settlement|tavern",
            agent => registry.TryGetAgentInfo(agent, out var info) && partyAgentMap.Contains(info.AgentId));

        bool registered = false;
        bool resolvedDuringReplay = false;
        int previousGameThreadId = GameThread.Instance.GameThreadId;
        GameThread.Instance.DiscardQueuedActions();
        GameThread.Instance.MarkGameThread();
        try
        {
            var producerThread = new Thread(() =>
            {
                GameThread.RunSafe(() =>
                {
                    registered = partyPuppetRegistrar.TrySpawnAndRegister(
                        () => remotePuppet,
                        registry,
                        "remote",
                        agentId,
                        out _);
                });
                GameThread.RunSafe(() =>
                {
                    resolvedDuringReplay = LocationNpcGate.IsPlayerPartyAgent(remotePuppet);
                });
            });
            producerThread.Start();
            Assert.True(
                producerThread.Join(GameThread.BlockingTimeout),
                "the producer should enqueue both actions without blocking");
            Assert.Equal(2, GameThread.Instance.QueueLength);

            GameThread.Instance.Update(TimeSpan.Zero);

            Assert.True(registered);
            Assert.True(resolvedDuringReplay);
        }
        finally
        {
            if (GameThread.Instance.QueueLength > 0)
                GameThread.Instance.Update(TimeSpan.Zero);
            GameThread.Instance.DiscardQueuedActions();
            GameThread.Instance.RestoreGameThread(previousGameThreadId);
            LocationNpcGate.EndMission();
        }
    }
}
