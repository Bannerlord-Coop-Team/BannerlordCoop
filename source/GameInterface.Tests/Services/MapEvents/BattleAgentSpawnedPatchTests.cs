using System;
using System.Collections.Generic;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Patches;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

/// <summary>Tests that the spawn capture patch skips puppets through the tracker the installed container hands out.</summary>
[Collection(ActiveCoopBattleCollection.Name)]
public class BattleAgentSpawnedPatchTests : IDisposable
{
    private readonly List<Agent> captured = new List<Agent>();

    // Subscribes an instance handler, since the broker holds subscriptions weakly.
    public BattleAgentSpawnedPatchTests()
    {
        MessageBroker.Instance.Subscribe<AgentSpawnedInBattle>(Handle_AgentSpawnedInBattle);
    }

    // Drops the subscription so later tests do not record into this instance.
    public void Dispose()
    {
        MessageBroker.Instance.Unsubscribe<AgentSpawnedInBattle>(Handle_AgentSpawnedInBattle);
    }

    // An open puppet spawn scope on the resolved tracker hides the spawn; closing it captures again.
    [Fact]
    public void Postfix_SkipsSpawn_OnlyWhileResolvedTrackerHasPuppetSpawnOpen()
    {
        using var battleScope = new ActiveCoopBattleScope("battle-1");
        var agent = ObjectHelper.SkipConstructor<Agent>();
        Assert.True(ContainerProvider.TryResolve<IPuppetSpawnTracker>(out var puppetSpawnTracker));

        using (puppetSpawnTracker.BeginPuppetSpawn())
        {
            BattleAgentSpawnedPatch.Postfix(agent);
        }

        Assert.DoesNotContain(agent, captured);

        BattleAgentSpawnedPatch.Postfix(agent);

        Assert.Single(captured, capturedAgent => ReferenceEquals(capturedAgent, agent));
    }

    // Records each captured spawn's agent.
    private void Handle_AgentSpawnedInBattle(MessagePayload<AgentSpawnedInBattle> payload)
    {
        captured.Add(payload.What.Agent);
    }
}
