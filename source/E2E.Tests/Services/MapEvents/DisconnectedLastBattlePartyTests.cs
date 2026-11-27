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
public class DisconnectedLastBattlePartyTests : MapEventTestBase
{
    private const string ControllerId = "PlayerOne";

    private EnvironmentInstance Client => Clients.First();

    public DisconnectedLastBattlePartyTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void LastFollowerOnItsSide_LeaderLeavesBattle_IsParkedAfterBattleEnds()
    {
        var context = CreateServerMapEvent();
        var leaderId = context.AttackerPartyId;
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        ConnectPlayer(partyId);
        FollowLeader(leaderId, partyId);
        PumpAll();

        // Parking waits for the battle to end
        Disconnect();
        AssertParty(partyId, active: true, inBattle: true);

        // The follower is last on its side and is removed inside the leader's attached party loop, so its removal finalizes the battle
        LeaveBattle(leaderId);
        PumpAll();

        AssertFinalized(context.MapEventId);
        AssertParty(partyId, active: false, inBattle: false);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Follower_LeaderJoinsAnotherBattleBeforeParking_IsParkedAfterThatBattleEnds(bool lastOnItsSide)
    {
        var first = CreateServerMapEvent();
        var second = CreateServerMapEvent();
        var leaderId = first.AttackerPartyId;
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        ConnectPlayer(partyId);
        FollowLeader(leaderId, partyId);
        if (!lastOnItsSide)
            JoinNewServerPartyToSide(first.MapEventId, BattleSideEnum.Attacker);
        PumpAll();

        Disconnect();
        AssertParty(partyId, active: true, inBattle: true);

        // The leader takes the follower into another battle in the same frame, before the park queued by a last follower's removal runs
        LeaveBattle(leaderId);
        JoinServerPartyToSide(second.MapEventId, leaderId, BattleSideEnum.Attacker);
        PumpAll();

        if (!lastOnItsSide)
        {
            DestroyServerMapEvent(first.MapEventId);
            PumpAll();
        }

        AssertFinalized(first.MapEventId);
        AssertParty(partyId, active: true, inBattle: true);
        Server.Call(() =>
        {
            var secondEvent = Server.GetRegisteredObject<MapEvent>(second.MapEventId);
            Assert.Single(Server.InternalMessages.GetMessages<PlayerDisconnectedFromMapEvent>(), message => message.MapEvent == secondEvent);
        });

        DestroyServerMapEvent(second.MapEventId);
        PumpAll();

        AssertFinalized(second.MapEventId);
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
