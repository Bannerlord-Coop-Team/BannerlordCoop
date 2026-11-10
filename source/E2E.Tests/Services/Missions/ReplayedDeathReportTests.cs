using System;
using System.Linq;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Environment.MockEngine;
using GameInterface.Services.MapEventParties.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.TroopSupply;
using Missions;
using Missions.Battles;
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// A kill is reported to the server by the victim's owner. Every other client replays the death through
/// native <c>RegisterBlow</c>, which runs <see cref="BattleAgentLogic"/>'s hit and removal callbacks again.
/// </summary>
public class ReplayedDeathReportTests : MissionTestEnvironment
{
    public ReplayedDeathReportTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void RoutedKill_ReachesServerOnce_WhenTheKillerReplaysTheDeath()
    {
        using var fixture = new MissionEngineFixture();
        var owner = Clients.First();
        var killer = Clients.Skip(1).First();
        var (mapEventId, partyIds) = SetupCoopBattle("owner", "killer");
        var victimId = Guid.NewGuid();
        var killerTroopId = Guid.NewGuid();

        // Models the tracker the server commits with the battle; clients skip score hits without one.
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            mapEvent.TroopUpgradeTracker = new TroopUpgradeTracker();
        });
        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                instance.CreateRegisteredObject<CharacterObject>("e2e_victim");
                instance.CreateRegisteredObject<CharacterObject>("e2e_killer");
            });
        }

        BattleSpawnGate.BeginBattle(mapEventId);
        try
        {
            (CoopBattleController controller, Agent victim, Agent killerTroop) EnterMission(
                EnvironmentInstance client, string mainPartyId)
            {
                CoopBattleController controller = null!;
                Agent victim = null!;
                Agent killerTroop = null!;
                client.Call(() =>
                {
                    Assert.True(client.ObjectManager.TryGetObject<MobileParty>(mainPartyId, out var mainParty));
                    Campaign.Current.MainParty = mainParty;
                    Assert.NotNull(MapEvent.PlayerMapEvent?.TroopUpgradeTracker);

                    var mission = CreateConnectedMission(fixture, client, mapEventId);
                    controller = client.Resolve<CoopBattleController>();
                    mission.Shell.MissionBehaviors.Add(ObjectHelper.SkipConstructor<BattleAgentLogic>());
                    mission.Shell.MissionBehaviors.Add(controller);

                    victim = SpawnTroop(client, mission, "e2e_victim", partyIds[0], mission.AttackerTeam, client == owner);
                    killerTroop = SpawnTroop(client, mission, "e2e_killer", partyIds[1], mission.DefenderTeam, client == killer);
                    var registry = client.Resolve<INetworkAgentRegistry>();
                    Assert.True(registry.TryRegisterAgent("owner", victimId, victim));
                    Assert.True(registry.TryRegisterAgent("killer", killerTroopId, killerTroop));
                }, MapEventDisabledMethods);
                return (controller, victim, killerTroop);
            }

            var ownerMission = EnterMission(owner, partyIds[0]);
            var killerMission = EnterMission(killer, partyIds[1]);

            // The killer's hit on its puppet routes to the owner on the next tick, the owner applies it natively
            // and broadcasts the death, and the killer replays that death on its puppet.
            killer.Call(() =>
            {
                var blow = new Blow(killerMission.killerTroop.Index)
                {
                    InflictedDamage = 150,
                    DamageType = DamageTypes.Cut,
                    VictimBodyPart = BoneBodyPartType.Chest,
                    // The suppressed hit sound reads the native monster.
                    BlowFlag = BlowFlags.NoSound,
                };
                blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex = -1;
                AttackCollisionData collisionData = default;
                killerMission.victim.RegisterBlow(blow, in collisionData);
                killerMission.controller.OnPreDisplayMissionTick(0.016f);
            });

            Assert.True(AgentMirror.TryGet(ownerMission.victim, out var ownerVictim));
            Assert.True(AgentMirror.TryGet(killerMission.victim, out var replayedVictim));
            Assert.False(ownerVictim.IsActive);
            Assert.False(replayedVictim.IsActive);

            Assert.Single(Server.InternalMessages.GetMessages<NetworkTroopScoreHit>());
            Assert.Single(Server.InternalMessages.GetMessages<NetworkBattleHitReward>());
            Assert.Single(Server.InternalMessages.GetMessages<NetworkCheckUpgradeAfterAgentRemoved>());
        }
        finally
        {
            BattleSpawnGate.EndBattle();
        }
    }

    /// <summary>Spawns a troop of the party, AI-controlled on its owner and a puppet everywhere else.</summary>
    private static Agent SpawnTroop(
        EnvironmentInstance client, MockMission mission, string characterId, string partyId, MockTeam team, bool owned)
    {
        var character = client.GetRegisteredObject<CharacterObject>(characterId);
        Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

        Agent agent = mission.SpawnAgent(new AgentBuildData(character)
            .Controller(owned ? AgentControllerType.AI : AgentControllerType.None)
            .Team(team.Shell));
        agent.HealthLimit = 100f;
        // The origin skips AgentBuildData.TroopOrigin, whose equipment seed needs a loaded character.
        Assert.True(AgentMirror.TryGet(agent, out var mirror));
        mirror.Origin = new CoopAgentOrigin(character, party.Party, 0, null, new UniqueTroopDescriptor(1));
        // BattleAgentLogic's vanilla bodies run inside Harmony-patched methods.
        NativeAgentFields.Back(agent, mirror);
        return agent;
    }
}
