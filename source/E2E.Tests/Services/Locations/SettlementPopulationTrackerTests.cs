using GameInterface.Services.Locations;
using GameInterface.Services.Locations.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Locations;

public class SettlementPopulationTrackerTests : SettlementTestEnvironment
{
    public SettlementPopulationTrackerTests(ITestOutputHelper output) : base(output, numClients: 1)
    {
    }

    [Fact]
    public void PlayerPartyEntryAndRemoval_BroadcastRosterReconciliation()
    {
        var (instanceId, partyIds) = CreateSettlement("A");
        string characterId = CreateRegisteredObject<CharacterObject>();
        string[] ids = instanceId.Split('|');

        Server.Call(() =>
        {
            Location location = Server.GetRegisteredObject<Location>(ids[1]);
            CharacterObject character = Server.GetRegisteredObject<CharacterObject>(characterId);
            location.AddCharacter(LocationCharacterFactory.Create(
                character,
                originParty: null,
                specialItem: null,
                spawnTag: "npc_common",
                actionSetCode: null,
                behaviorsMethodName: null,
                characterRelation: (int)LocationCharacter.CharacterRelations.Neutral,
                fixedLocation: false,
                useCivilianEquipment: true));
        });
        Server.NetworkSentMessages.Clear();

        Server.Call(() =>
        {
            Settlement settlement = Server.GetRegisteredObject<Settlement>(ids[0]);
            MobileParty party = Server.GetRegisteredObject<MobileParty>(partyIds[0]);
            Server.Resolve<SettlementPopulationTracker>().OnPartyEnteredSettlement(settlement, party);
        });

        NetworkLocationRosterSnapshot snapshot = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkLocationRosterSnapshot>());
        LocationCharacterData entry = Assert.Single(snapshot.Entries);
        Assert.Equal(ids[0], snapshot.SettlementId);
        Assert.Equal(characterId, entry.CharacterId);

        Server.NetworkSentMessages.Clear();
        Server.Call(() =>
        {
            MobileParty party = Server.GetRegisteredObject<MobileParty>(partyIds[0]);
            Server.Resolve<SettlementPopulationTracker>().OnPartyLeftSettlement(party);
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkRemoveAllLocationCharacters>());
        Server.Call(() => Assert.Empty(Server.GetRegisteredObject<Location>(ids[1]).GetCharacterList()));
        foreach (var client in Clients)
            client.Call(() => Assert.Empty(client.GetRegisteredObject<Location>(ids[1]).GetCharacterList()));
    }
}
