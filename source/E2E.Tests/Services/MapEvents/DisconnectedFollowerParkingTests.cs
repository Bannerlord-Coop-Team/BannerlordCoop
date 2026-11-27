using Common.Messaging;
using Common.Network.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.MapEvents.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// A disconnected player's follower leaves its battle with its leader before its deferred park runs (#4945, #4952).
/// </summary>
public class DisconnectedFollowerParkingTests : MapEventTestBase
{
    private const string ControllerId = "PlayerOne";

    private EnvironmentInstance Client => Clients.First();

    public DisconnectedFollowerParkingTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void LastFollowerOnItsSide_LeaderLeavesBattle_IsParkedAfterBattleEnds()
    {
        var context = CreateServerMapEvent();
        var leaderId = context.AttackerPartyId;
        var partyId = DisconnectedFollowerOf(leaderId);

        // The follower is last on its side and is removed inside the leader's attached party loop, so its removal finalizes the battle
        LeaveBattle(leaderId);
        PumpAll();

        AssertFinalized(context.MapEventId);
        AssertParty(partyId, active: false, inBattle: false);
    }

    [Fact]
    public void LastFollower_LeaderJoinsAnotherBattleInSameFrame_IsParkedAfterThatBattleEnds()
    {
        var first = CreateServerMapEvent();
        var second = CreateServerMapEvent();
        var partyId = DisconnectedFollowerOf(first.AttackerPartyId);

        // The follower's removal ends the first battle and queues its park, which runs after the leader took it into the second battle
        JoinOtherBattle(first.AttackerPartyId, second.MapEventId);
        PumpAll();

        EndSecondBattleAndAssertParkedOnlyThen(partyId, first.MapEventId, second.MapEventId);
    }

    [Fact]
    public void Follower_FirstBattleEndsAfterLeaderJoinsAnother_IsParkedAfterThatBattleEnds()
    {
        var first = CreateServerMapEvent();
        var second = CreateServerMapEvent();
        var partyId = DisconnectedFollowerOf(first.AttackerPartyId);

        // A party after the follower keeps the first battle going once the leader took the follower into the second battle
        JoinNewServerPartyToSide(first.MapEventId, BattleSideEnum.Attacker);
        JoinOtherBattle(first.AttackerPartyId, second.MapEventId);
        PumpAll();

        // The first battle ending parks right away and finds the follower in the second battle
        DestroyServerMapEvent(first.MapEventId);
        PumpAll();

        EndSecondBattleAndAssertParkedOnlyThen(partyId, first.MapEventId, second.MapEventId);
    }

    // A disconnected player's party following the leader, kept active in the leader's battle until it ends
    private string DisconnectedFollowerOf(string leaderId)
    {
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        ConnectPlayer(partyId);
        FollowLeader(leaderId, partyId);
        PumpAll();

        Disconnect();
        AssertParty(partyId, active: true, inBattle: true);
        return partyId;
    }

    // The leader leaves its battle and joins the other one in the same frame, taking its attached follower with it
    private void JoinOtherBattle(string leaderId, string mapEventId)
    {
        LeaveBattle(leaderId);
        JoinServerPartyToSide(mapEventId, leaderId, BattleSideEnum.Attacker);
    }

    private void EndSecondBattleAndAssertParkedOnlyThen(string partyId, string firstMapEventId, string secondMapEventId)
    {
        AssertFinalized(firstMapEventId);
        AssertParty(partyId, active: true, inBattle: true);
        Server.Call(() =>
        {
            var secondEvent = Server.GetRegisteredObject<MapEvent>(secondMapEventId);
            Assert.Single(Server.InternalMessages.GetMessages<PlayerDisconnectedFromMapEvent>(), message => message.MapEvent == secondEvent);
        });

        DestroyServerMapEvent(secondMapEventId);
        PumpAll();

        AssertFinalized(secondMapEventId);
        AssertParty(partyId, active: false, inBattle: false);
    }

    // Attaching to the leader puts the follower on the leader's battle side after it
    private void FollowLeader(string leaderId, string partyId)
    {
        Server.Call(() =>
        {
            var leader = Server.GetRegisteredObject<MobileParty>(leaderId);
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            party.Army = new Army(GameObjectCreator.CreateInitializedObject<Kingdom>(), leader, Army.ArmyTypes.Patrolling);
            party.AttachedTo = leader;
        }, MapEventDisabledMethods);
    }

    private void LeaveBattle(string leaderId)
    {
        Server.Call(() => Server.GetRegisteredObject<MobileParty>(leaderId).Party.MapEventSide = null, MapEventDisabledMethods);
    }

    private void ConnectPlayer(string partyId)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        RegisterAsPlayerParty(ControllerId, heroId, partyId);
        TestEnvironment.ConnectRegisteredPlayer(Client, ControllerId);

        Server.Call(() => Server.GetRegisteredObject<MobileParty>(partyId).IsActive = true);
        PumpAll();
    }

    private void Disconnect()
    {
        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(this, new PlayerDisconnected(Client.NetPeer, default)));
        PumpAll();
    }

    private void PumpAll()
    {
        Server.PumpGameThread();
        foreach (var client in Clients)
            client.PumpGameThread();
    }

    private void AssertFinalized(string mapEventId)
    {
        foreach (var instance in Clients.Append(Server))
            Assert.False(instance.ObjectManager.TryGetObject<MapEvent>(mapEventId, out _), $"{Name(instance)}: battle still registered");
    }

    private void AssertParty(string partyId, bool active, bool inBattle)
    {
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var party = instance.GetRegisteredObject<MobileParty>(partyId);
                Assert.True(party.IsActive == active, $"{Name(instance)}: party active is {party.IsActive}");
                Assert.True((party.MapEvent != null) == inBattle, $"{Name(instance)}: party battle is {party.MapEvent?.StringId}");
            });
        }
    }

    private string Name(EnvironmentInstance instance) => instance == Server ? "server" : "client";
}
