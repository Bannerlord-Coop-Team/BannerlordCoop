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
        Thread spawnThread = null;
        int previousGameThreadId = GameThread.Instance.GameThreadId;
        GameThread.Instance.MarkGameThread();
        try
        {
            spawnThread = new Thread(() => GameThread.RunSafe(() =>
            {
                registered = partyPuppetRegistrar.TrySpawnAndRegister(
                    () => remotePuppet,
                    registry,
                    "remote",
                    agentId,
                    out _);
            }, blocking: true));
            spawnThread.Start();
            Assert.True(SpinWait.SpinUntil(
                () => GameThread.Instance.QueueLength == 1,
                TimeSpan.FromSeconds(2)));

            var replayThread = new Thread(() => GameThread.RunSafe(() =>
            {
                resolvedDuringReplay = LocationNpcGate.IsPlayerPartyAgent(remotePuppet);
            }));
            replayThread.Start();
            replayThread.Join();
            Assert.True(SpinWait.SpinUntil(
                () => GameThread.Instance.QueueLength == 2,
                TimeSpan.FromSeconds(2)));

            GameThread.Instance.Update(TimeSpan.Zero);
            spawnThread.Join();

            Assert.True(registered);
            Assert.True(resolvedDuringReplay);
        }
        finally
        {
            if (GameThread.Instance.QueueLength > 0)
                GameThread.Instance.Update(TimeSpan.Zero);
            spawnThread?.Join(TimeSpan.FromSeconds(1));
            GameThread.Instance.DiscardQueuedActions();
            GameThread.Instance.RestoreGameThread(previousGameThreadId);
            LocationNpcGate.EndMission();
        }
    }
}
