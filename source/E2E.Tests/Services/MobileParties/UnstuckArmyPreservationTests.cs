using Common.Commands;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.GameDebug.Commands;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Unstuck;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobileParties;

/// <summary>Verifies unstuck preserves player-led armies and replicates each recovered party exit.</summary>
public class UnstuckArmyPreservationTests : MapEventTestBase
{
    public UnstuckArmyPreservationTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void PlayerLeaderWithThreeParties_RepeatedUnstuckPreservesArmyOnEveryPeer()
    {
        var player = CreateRequester("unstuck-army-leader");
        var army = CreateArmy(player.partyId);
        AssertArmy(army, army.PartyIds);

        RequestUnstuck(player.partyId, player.heroId);
        RequestUnstuck(player.partyId, player.heroId);

        AssertArmy(army, army.PartyIds);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkRemovePartyInArmy>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPartyLeftBattle>());
        Assert.Equal(2, Server.NetworkSentMessages.GetMessages<NetworkPlayerUnstuckResult>().Count());
        AssertSuccessfulResults();
    }

    [Fact]
    public void PlayerLeaderWithThreeParties_UnstuckLeavesBattleAndPreservesArmyOnEveryPeer()
    {
        var player = CreateRequester("unstuck-battle-leader");
        var battle = CreateServerMapEvent();
        JoinBattle(player.partyId, battle.MapEventId);
        var army = CreateArmy(player.partyId);
        AssertArmy(army, army.PartyIds);
        AssertBattle(army.PartyIds, battle.MapEventId);

        RequestUnstuck(player.partyId, player.heroId);
        RequestUnstuck(player.partyId, player.heroId);

        AssertArmy(army, army.PartyIds);
        AssertBattle(army.PartyIds, null);
        AssertBattle(new[] { battle.AttackerPartyId, battle.DefenderPartyId }, battle.MapEventId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkRemovePartyInArmy>());
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPartyLeftBattle>());
        AssertSuccessfulResults();
    }

    [Fact]
    public void PlayerLeaderWithThreeParties_UnstuckLeavesSettlementAndPreservesArmyOnEveryPeer()
    {
        var player = CreateRequester("unstuck-settlement-leader");
        var army = CreateArmy(player.partyId);
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();

        // Model the same stuck occupancy in each replica; the actual exit runs through production patches.
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var settlement = instance.GetRegisteredObject<Settlement>(settlementId);
                settlement.SettlementComponent = instance.GetRegisteredObject<Town>(townId);
                foreach (var partyId in army.PartyIds)
                    instance.GetRegisteredObject<MobileParty>(partyId).SetCurrentSettlementDirectly(settlement);
            });
        }
        AssertArmy(army, army.PartyIds);

        RequestUnstuck(player.partyId, player.heroId);
        RequestUnstuck(player.partyId, player.heroId);

        AssertArmy(army, army.PartyIds);
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                foreach (var partyId in army.PartyIds)
                    Assert.Null(instance.GetRegisteredObject<MobileParty>(partyId).CurrentSettlement);
            });
        }
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkRemovePartyInArmy>());
        AssertSuccessfulResults();
    }

    [Fact]
    public void PlayerFollower_RepeatedUnstuckRemovesOnlyRequesterFromArmyAndBattle()
    {
        var player = CreateRequester("unstuck-army-follower");
        var battle = CreateServerMapEvent();
        var army = CreateArmy(battle.DefenderPartyId, player.partyId);
        AssertArmy(army, army.PartyIds);
        AssertBattle(army.PartyIds, battle.MapEventId);

        RequestUnstuck(player.partyId, player.heroId);
        RequestUnstuck(player.partyId, player.heroId);

        var remainingPartyIds = army.PartyIds.Where(id => id != player.partyId).ToArray();
        AssertArmy(army, remainingPartyIds);
        AssertBattle(remainingPartyIds, battle.MapEventId);
        AssertBattle(new[] { player.partyId }, null);
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var party = instance.GetRegisteredObject<MobileParty>(player.partyId);
                Assert.Null(party.Army);
                Assert.Null(party.AttachedTo);
            });
        }
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkRemovePartyInArmy>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkPartyLeftBattle>());
        AssertSuccessfulResults();
    }

    private (string heroId, string partyId) CreateRequester(string controllerId)
    {
        var player = CreatePlayerHeroParty(controllerId);
        var characterId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var client = Clients.First();
        client.Call(() =>
        {
            var hero = client.GetRegisteredObject<Hero>(player.heroId);
            var character = client.GetRegisteredObject<CharacterObject>(characterId);
            var party = client.GetRegisteredObject<MobileParty>(player.partyId);
            using (new AllowedThread())
            {
                character.HeroObject = hero;
                hero.PartyBelongedTo = party;
                Game.Current.PlayerTroop = character;
                Campaign.Current.MainParty = party;
            }
        });
        return player;
    }

    private ArmyState CreateArmy(string leaderPartyId, string firstFollowerId = null)
    {
        firstFollowerId ??= TestEnvironment.CreateRegisteredObject<MobileParty>();
        var secondFollowerId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var partyIds = new[] { leaderPartyId, firstFollowerId, secondFollowerId };
        string armyId = null;
        Server.Call(() =>
        {
            var leader = Server.GetRegisteredObject<MobileParty>(leaderPartyId);
            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            var army = new Army(kingdom, leader, Army.ArmyTypes.Patrolling);
            foreach (var partyId in partyIds.Skip(1))
            {
                var member = Server.GetRegisteredObject<MobileParty>(partyId);
                member.Army = army;
                member.AttachedTo = leader;
            }
            Assert.True(Server.ObjectManager.TryGetId(army, out armyId));
            Campaign.Current.MainParty = null;
        }, MapEventDisabledMethods);
        Assert.NotNull(armyId);

        var replicas = new Dictionary<EnvironmentInstance, Army>();
        foreach (var instance in Clients.Append(Server))
            instance.Call(() => replicas.Add(instance, instance.GetRegisteredObject<Army>(armyId)));
        return new ArmyState(armyId, partyIds, replicas);
    }

    private void JoinBattle(string partyId, string mapEventId)
    {
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            party.Party.MapEventSide = Server.GetRegisteredObject<MapEvent>(mapEventId).DefenderSide;
        }, MapEventDisabledMethods);
    }

    private void RequestUnstuck(string partyId, string heroId)
    {
        var client = Clients.First();
        client.Call(() =>
        {
            var command = new UnstuckCommand.UnstuckCoopCommand();
            var result = command.ProcessCommand(new CoopCommandArgsFactory().FromValues(Array.Empty<string>()));
            Assert.True(result.Succeeded, result.Output);
        });
        TestEnvironment.FlushCoalescer();
        var request = client.NetworkSentMessages.GetMessages<NetworkRequestPlayerUnstuck>().Last();
        Assert.Equal(partyId, request.PartyId);
        Assert.Equal(heroId, request.HeroId);
    }

    private void AssertArmy(ArmyState expected, string[] expectedPartyIds)
    {
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var army = instance.GetRegisteredObject<Army>(expected.ArmyId);
                var leader = instance.GetRegisteredObject<MobileParty>(expected.PartyIds[0]);
                Assert.Same(expected.Replicas[instance], army);
                Assert.Same(leader, army.LeaderParty);
                Assert.Equal(expectedPartyIds.Length, army.Parties.Count);
                Assert.Equal(expectedPartyIds.Length - 1, leader.AttachedParties.Count);
                foreach (var partyId in expectedPartyIds)
                {
                    var party = instance.GetRegisteredObject<MobileParty>(partyId);
                    Assert.Contains(party, army.Parties);
                    Assert.Same(army, party.Army);
                    if (party == leader)
                        Assert.Null(party.AttachedTo);
                    else
                    {
                        Assert.Same(leader, party.AttachedTo);
                        Assert.Contains(party, leader.AttachedParties);
                    }
                }
            });
        }
    }

    private void AssertBattle(string[] partyIds, string mapEventId)
    {
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var expectedEvent = mapEventId == null ? null : instance.GetRegisteredObject<MapEvent>(mapEventId);
                foreach (var partyId in partyIds)
                    Assert.Same(expectedEvent, instance.GetRegisteredObject<MobileParty>(partyId).MapEvent);
            });
        }
    }

    private void AssertSuccessfulResults()
    {
        Assert.Equal(2, Clients.First().InternalMessages.GetMessages<PlayerUnstuckCompleted>().Count());
        foreach (var otherClient in Clients.Skip(1))
            Assert.Empty(otherClient.InternalMessages.GetMessages<PlayerUnstuckCompleted>());
        foreach (var result in Server.NetworkSentMessages.GetMessages<NetworkPlayerUnstuckResult>())
            Assert.DoesNotContain(result.Actions, action => action.StartsWith("Failed "));
    }

    private record ArmyState(string ArmyId, string[] PartyIds, Dictionary<EnvironmentInstance, Army> Replicas);
}
