using Common.Messaging;
using Common.Network.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

/// <summary>
/// A disconnected player's follower that is last on its battle side leaves the battle with its leader, which finalizes it (#4945).
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

        // Attaching to the leader puts the follower on the leader's battle side after it
        Server.Call(() =>
        {
            var leader = Server.GetRegisteredObject<MobileParty>(leaderId);
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            party.Army = new Army(GameObjectCreator.CreateInitializedObject<Kingdom>(), leader, Army.ArmyTypes.Patrolling);
            party.AttachedTo = leader;
        }, MapEventDisabledMethods);
        PumpAll();

        // Parking waits for the battle to end
        Disconnect();
        AssertParty(partyId, active: true, inBattle: true);

        // The follower is last on its side and is removed inside the leader's attached party loop, so its removal finalizes the battle
        Server.Call(() =>
        {
            var leader = Server.GetRegisteredObject<MobileParty>(leaderId);
            leader.Party.MapEventSide = null;
        }, MapEventDisabledMethods);
        PumpAll();

        AssertFinalized(context.MapEventId);
        AssertParty(partyId, active: false, inBattle: false);
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
