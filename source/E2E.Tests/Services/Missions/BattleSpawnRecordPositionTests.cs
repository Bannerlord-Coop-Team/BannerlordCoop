using System;
using System.Linq;
using Common.Messaging;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEvents.Messages;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>R50 F3: an initial spawn record carries where its agent stands when the batch goes out, not where native first
/// spawned it, so a peer's puppet doesn't spawn off its spot and snap there.</summary>
public class BattleSpawnRecordPositionTests : MissionTestEnvironment
{
    public BattleSpawnRecordPositionTests(ITestOutputHelper output) : base(output) { }

    // Native teleports an AI crew to its oars in the frame it spawns (NavalShipAgents.AssignAndTeleportCrewToShipMachines).
    [Fact]
    public void InitialRecord_CarriesThePositionTheAgentWasMovedToBeforeTheFlush()
    {
        using var fixture = new MissionEngineFixture();
        var (_, partyIds) = SetupCoopBattle("owner", "peer");
        var owner = Clients.First();
        SetControllerId(owner, "owner");
        string characterId = CreateRegisteredObject<CharacterObject>();
        var seat = new Vec3(42f, 7f, 3f);

        owner.Call(() =>
        {
            var mock = fixture.CreateMission(owner);
            var controller = owner.Resolve<CoopBattleController>();
            var network = owner.Resolve<MockBattleNetwork>();
            Assert.True(owner.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));
            Assert.True(owner.ObjectManager.TryGetObject<MobileParty>(partyIds[0], out var party));
            network.NetworkSentMessages.Clear();
            var origin = new DebugReplicationFixtureAgentOrigin(character, party.Party, rank: -1, banner: null,
                new UniqueTroopDescriptor(int.MaxValue));
            var agent = mock.SpawnAgent(new AgentBuildData(character).Controller(AgentControllerType.AI).TroopOrigin(origin));
            owner.Resolve<IMessageBroker>().Publish(this, new AgentSpawnedInBattle(agent));

            agent.TeleportToPosition(seat);
            controller.OnMissionTick(0f);

            var initial = network.NetworkSentMessages.GetMessages<NetworkSpawnBattleAgents>().Single();
            Assert.Equal(SpawnBatchPurpose.Initial, initial.Purpose);
            Assert.Equal(seat, initial.Agents.Single().Position);
            GC.KeepAlive(controller);
        });
    }
}
