using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.MapEventParties.Messages;
using GameInterface.Services.MapEvents.Messages;
using Missions.Messages;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Phase D: the "back up to the server" end of the battle vertical. A battle casualty reported by an owner
/// (<c>NetworkRequestBattleCasualty</c>) must be applied to the SERVER's authoritative map-event roster — the
/// point where the mission-side outcome syncs back to the campaign. Exercises the real server pipeline
/// (<c>BattleCasualtyHandler</c> resolving the troop character through <c>IObjectManager</c> →
/// <c>MapEventParty.OnTroopKilled</c>), including the character-keyed matching that fixed the descriptor-churn
/// KeyNotFound crash. The casualty is addressed purely by coop object id — never a raw StringId.
/// </summary>
public class BattleCasualtyVerticalTests : MissionTestEnvironment
{
    public BattleCasualtyVerticalTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ReportedBattleCasualty_ReducesServerMapEventRoster()
    {
        var (mapEventId, _) = SetupCoopBattle("attacker", "defender");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            var party = mapEvent.DefenderSide.Parties[0];
            Assert.True(Server.ObjectManager.TryGetId(party, out var partyId));

            // A troop character known to the object manager — the coop identity the casualty is keyed on.
            var troop = Server.CreateRegisteredObject<CharacterObject>("e2e_troop");
            Assert.True(Server.ObjectManager.TryGetId(troop, out var troopId));

            // Give the party two of that troop and let it build its flattened battle roster the way the engine
            // does — OnTroopKilled decrements the member roster too, so the two must stay consistent.
            party.Party.MemberRoster.AddToCounts(troop, 2);
            party.Update();
            Assert.Equal(2, CountLive(party.Troops, troop));

            int casualtiesBefore = mapEvent.DefenderSide.TroopCasualties;

            int applied = 0;
            var broker = Server.Resolve<IMessageBroker>();
            broker.Subscribe<OnTroopKilledAttempted>(_ => applied++);

            // An owner reports one of them as a (non-wounded) casualty, addressed by the character's object id.
            broker.Publish(this, new NetworkRequestBattleCasualty(partyId, troopId, wounded: false));

            Assert.True(applied > 0, "BattleCasualtyHandler did not apply the casualty on the server");
            Assert.Equal(1, CountLive(party.Troops, troop)); // one killed in the authoritative roster
            Assert.Equal(casualtiesBefore + 1, mapEvent.DefenderSide.TroopCasualties);
        });
    }

    [Fact]
    public void ClientPlayerPartyAdd_SendsNoRostersToServer()
    {
        var (mapEventId, _) = SetupCoopBattle("attacker", "defender");

        string joinerId = null!;
        Server.Call(() =>
        {
            var joiner = GameObjectCreator.CreateInitializedObject<MobileParty>();
            Assert.True(Server.ObjectManager.TryGetId(joiner, out joinerId));
        });
        RegisterAsPlayerParty("joiner", CreateRegisteredObject<Hero>(), joinerId);

        var client = Clients.First();
        client.NetworkSentMessages.Clear();

        // A replicated add runs the native MapEventSide.AddPartyInternal on the client, which raises MapEventInvolvedPartiesAdded there.
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(joinerId, out var joiner));
            using (new AllowedThread())
                joiner.Party.MapEventSide = mapEvent.AttackerSide;
            Assert.Contains(mapEvent.AttackerSide.Parties, party => party.Party == joiner.Party);
        }, MapEventDisabledMethods);

        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkUpdateMapEventParty>());
        Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkAddInvolvedParties>());
    }

    private static int CountLive(FlattenedTroopRoster roster, CharacterObject troop)
    {
        int n = 0;
        foreach (var element in roster)
            if (!element.IsKilled && element.Troop == troop) n++;
        return n;
    }
}
