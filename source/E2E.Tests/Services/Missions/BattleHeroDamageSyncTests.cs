using System;
using System.Collections.Generic;
using Common.Messaging;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.MockEngine;
using Common.Logging;
using GameInterface.Services.Heroes.Messages;
using Missions;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Covers campaign-health propagation from a co-op battle hero's routed blows.
/// Client-to-server <see cref="Hero.HitPoints"/> forwarding is covered by the map-event environment tests.
/// </summary>
public class BattleHeroDamageSyncTests : MissionTestEnvironment
{
    public BattleHeroDamageSyncTests(ITestOutputHelper output) : base(output) { }

    // R49: the owner wrote the managed Hero.HitPoints itself, logging "Client updated managed Hero.HitPoints" on every hit.
    [Fact]
    public void RoutedDamageToHeroAgent_RequestsHeroHitPointsWithoutAClientWrite()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        SetControllerId(client, "owner");
        var diagnostics = new List<string>();
        Action<string> capture = message =>
        {
            if (message.Contains("Client updated managed") && message.Contains("HitPoints"))
                lock (diagnostics) diagnostics.Add(message);
        };
        var requests = new List<int>();
        // The broker holds handlers weakly, so the test keeps this one alive.
        Action<MessagePayload<HeroHitPointsChangeRequested>> onRequest = payload => requests.Add(payload.What.HitPoints);
        client.Resolve<IMessageBroker>().Subscribe(onRequest);
        OutputSinkManager.AddLogCallback(capture);
        try
        {
            ApplyRoutedHit(client, fixture);
        }
        finally
        {
            OutputSinkManager.RemoveLogCallback(capture);
        }

        Assert.Empty(diagnostics);
        Assert.Equal(new[] { 70 }, requests);
        GC.KeepAlive(onRequest);
    }

    // Routes a 30-damage hit to a hero agent this client owns and checks it landed on the agent.
    private void ApplyRoutedHit(EnvironmentInstance client, MissionEngineFixture fixture)
    {
        client.Call(() =>
        {
            var mock = fixture.CreateMission(client);
            var controller = client.Resolve<CoopBattleController>(); // ctor subscribes to NetworkApplyBattleDamage
            var registry = client.Resolve<INetworkAgentRegistry>();

            // A HERO agent owned by this client.
            var character = Game.Current.PlayerTroop;
            Assert.True(character is CharacterObject hc && hc.IsHero, "test needs a hero character");
            var hero = ((CharacterObject)character).HeroObject;

            var agent = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.AI));
            var heroAgentId = Guid.NewGuid();
            Assert.True(registry.TryRegisterAgent("owner", heroAgentId, agent));

            Assert.NotEqual(70, hero.HitPoints); // guard: the request below must be meaningful

            // The hero takes a 30-damage hit, routed to its owner (this client).
            var blow = new Blow(0) { InflictedDamage = 30, DamageType = DamageTypes.Pierce };
            AttackCollisionData collisionData = default;
            var damageData = client.Resolve<IBattleDamageDataMapper>().Pack(in blow, in collisionData);
            client.Resolve<IMessageBroker>().Publish(
                this,
                new NetworkApplyBattleDamage(
                    heroAgentId,
                    Guid.Empty,
                    damageData,
                    blow.IsMissile));

            // The blow landed on the agent (in-mission health 100 -> 70).
            Assert.True(AgentMirror.TryGet(agent, out var mirror));
            Assert.Equal(70f, mirror.Health);

            GC.KeepAlive(controller);
        });
    }
}
