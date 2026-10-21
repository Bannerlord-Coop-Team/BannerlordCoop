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

/// <summary>Tests that the formation capture patch skips puppets through the tracker the installed container hands out.</summary>
[Collection(ActiveCoopBattleCollection.Name)]
public class BattleAgentFormationChangedPatchTests : IDisposable
{
    private readonly List<Agent> captured = new List<Agent>();

    // Subscribes an instance handler, since the broker holds subscriptions weakly.
    public BattleAgentFormationChangedPatchTests()
    {
        MessageBroker.Instance.Subscribe<BattleAgentFormationChanged>(Handle_BattleAgentFormationChanged);
    }

    // Drops the subscription so later tests do not record into this instance.
    public void Dispose()
    {
        MessageBroker.Instance.Unsubscribe<BattleAgentFormationChanged>(Handle_BattleAgentFormationChanged);
    }

    // An open puppet spawn scope on the resolved tracker hides the formation change; closing it captures again.
    [Fact]
    public void Postfix_SkipsFormationChange_OnlyWhileResolvedTrackerHasPuppetSpawnOpen()
    {
        using var battleScope = new ActiveCoopBattleScope("battle-1");
        var agent = ObjectHelper.SkipConstructor<Agent>();
        var previousFormation = ObjectHelper.SkipConstructor<Formation>();
        Assert.True(ContainerProvider.TryResolve<IPuppetSpawnTracker>(out var puppetSpawnTracker));

        using (puppetSpawnTracker.BeginPuppetSpawn())
        {
            BattleAgentFormationChangedPatch.Postfix(agent, previousFormation);
        }

        Assert.DoesNotContain(agent, captured);

        BattleAgentFormationChangedPatch.Postfix(agent, previousFormation);

        Assert.Single(captured, capturedAgent => ReferenceEquals(capturedAgent, agent));
    }

    // Records each captured formation change's agent.
    private void Handle_BattleAgentFormationChanged(MessagePayload<BattleAgentFormationChanged> payload)
    {
        captured.Add(payload.What.Agent);
    }
}
