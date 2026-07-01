using System;
using Common.Messaging;
using E2E.Tests.Environment;
using E2E.Tests.Environment.MockEngine;
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
/// Reproduces the live bug: damage to a HERO in a coop battle does not update its <see cref="Hero.HitPoints"/>,
/// so the server (and everyone else) never sees the hero's health drop. The routed-damage handler
/// (<c>CoopBattleController.Handle_NetworkApplyBattleDamage</c>) applies the blow to the agent's in-mission
/// Health only; nothing bridges that back to the campaign <c>Hero.HitPoints</c>, which is the value that syncs
/// to the server (via <c>HeroHitPointsRequestPatch</c>). The handler now mirrors the owned hero's post-blow
/// <c>Agent.Health</c> onto <c>Hero.HitPoints</c>, so a surviving-but-wounded hero's health reaches the server.
/// This is the green spec for that fix; the HitPoints→server sync itself is covered by
/// <c>ClientWoundsControlledHero_RequestsHitPoints_SyncsToServerAndClients</c>.
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
}
