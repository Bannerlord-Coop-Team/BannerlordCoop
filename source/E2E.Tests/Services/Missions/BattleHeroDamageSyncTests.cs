using System;
using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
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
/// Covers campaign-health propagation from a co-op battle hero's routed blows and final agent removal.
/// Client-to-server <see cref="Hero.HitPoints"/> forwarding is covered by the map-event environment tests.
/// </summary>
public class BattleHeroDamageSyncTests : MissionTestEnvironment
{
    public BattleHeroDamageSyncTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void RoutedDamageToHeroAgent_UpdatesHeroHitPoints()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        SetControllerId(client, "owner");

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

            var hitPointsBefore = hero.HitPoints;
            Assert.NotEqual(70, hitPointsBefore); // guard: the assertion below must be meaningful

            // The hero takes a 30-damage hit, routed to its owner (this client).
            var blow = new Blow(0) { InflictedDamage = 30, DamageType = DamageTypes.Pierce };
            client.Resolve<IMessageBroker>().Publish(this, new NetworkApplyBattleDamage(heroAgentId, Guid.Empty, blow, default));

            // The blow landed on the agent (in-mission health 100 -> 70)...
            Assert.True(AgentMirror.TryGet(agent, out var mirror));
            Assert.Equal(70f, mirror.Health);

            // ...and the hero's campaign HitPoints should reflect it so it can sync to the server. BUG: it doesn't.
            Assert.Equal(70, hero.HitPoints);

            GC.KeepAlive(controller);
        });
    }

    [Theory]
    [InlineData(42.6f, 43)]
    [InlineData(0f, 1)]
    public void OwnedHeroAgentRemoval_UpdatesHeroHitPoints(float agentHealth, int expectedHitPoints)
    {
        AssertAgentRemovalHealth("owner", "owner", agentHealth, expectedHitPoints);
    }

    [Fact]
    public void RemoteHeroAgentRemoval_DoesNotUpdateHeroHitPoints()
    {
        AssertAgentRemovalHealth("owner", "observer", 25f, 100);
    }

    private void AssertAgentRemovalHealth(string ownerControllerId, string localControllerId, float agentHealth, int expectedHitPoints)
    {
        var client = Clients.First();
        SetControllerId(client, localControllerId);

        client.Call(() =>
        {
            var character = Assert.IsType<CharacterObject>(Game.Current.PlayerTroop);
            var hero = character.HeroObject;
            var objectManager = client.Resolve<IObjectManager>();
            if (!objectManager.TryGetId(hero, out var heroId))
            {
                heroId = $"Issue1835Hero_{Guid.NewGuid():N}";
                Assert.True(objectManager.AddExisting(heroId, hero));
            }

            var playerManager = client.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(ownerControllerId, heroId, string.Empty, string.Empty, string.Empty)));

            using (new AllowedThread())
            {
                hero.HitPoints = 100;
                var origin = new CoopAgentOrigin(character, null, -1, null, new UniqueTroopDescriptor(1));
                origin.OnAgentRemoved(agentHealth);
            }

            Assert.Equal(expectedHitPoints, hero.HitPoints);
        });
    }
}
