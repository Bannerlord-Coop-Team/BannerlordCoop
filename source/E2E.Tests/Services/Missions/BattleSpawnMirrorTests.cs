using System.Linq;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.TroopSupply;
using TaleWorlds.CampaignSystem;
using E2E.Tests.Environment;
using E2E.Tests.Environment.MockEngine;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Phase A foundation: proves the <see cref="MissionEngineFixture"/> shims let the real engine entry points
/// (<c>Mission.Current</c>, <c>Mission.SpawnAgent</c>, <see cref="Agent"/> members) run headless against the
/// <see cref="MockMission"/> mirror — the substrate the spawn/damage/death tests build on.
/// </summary>
public class BattleSpawnMirrorTests : MissionTestEnvironment
{
    public BattleSpawnMirrorTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void RejoiningAgent_RestoresHealthBeforeSpawnBroadcast()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();
        client.Call(() =>
        {
            fixture.CreateMission(client);
            var character = (CharacterObject)Game.Current.PlayerTroop;
            var origin = new CoopAgentOrigin(character, null, 0, null, new UniqueTroopDescriptor(1), initialHealth: 37f);
            var broker = client.Resolve<IMessageBroker>();
            float? broadcastHealth = null;
            void Capture(MessagePayload<AgentSpawnedInBattle> payload) => broadcastHealth = payload.What.Agent.Health;
            broker.Subscribe<AgentSpawnedInBattle>(Capture);
            BattleSpawnGate.BeginBattle("retreat-health-test");
            try
            {
                var agent = Mission.Current.SpawnAgent(new AgentBuildData(character)
                    .Controller(AgentControllerType.AI).TroopOrigin(origin));
                Assert.Equal(37f, agent.Health);
                Assert.Equal(37f, broadcastHealth);
            }
            finally
            {
                BattleSpawnGate.EndBattle();
                broker.Unsubscribe<AgentSpawnedInBattle>(Capture);
            }
        });
    }

    [Fact]
    public void SpawnAgent_PopulatesMirror_AndAgentMembersWork()
    {
        using var fixture = new MissionEngineFixture();
        var client = Clients.First();

        client.Call(() =>
        {
            var mock = fixture.CreateMission(client);
            BasicCharacterObject character = Game.Current.PlayerTroop; // real CharacterObject from SetupMainHero

            var buildData = new AgentBuildData(character).Controller(AgentControllerType.AI);

            var agent = Mission.Current.SpawnAgent(buildData);

            Assert.NotNull(agent);
            Assert.Equal(AgentControllerType.AI, agent.Controller);
            Assert.True(agent.IsActive());
            Assert.Same(character, agent.Character);

            agent.Health = 42f;
            Assert.Equal(42f, agent.Health);

            Assert.Single(mock.Agents);
            Assert.Same(agent, Mission.Current.FindAgentWithIndex(agent.Index));
        });
    }
}
