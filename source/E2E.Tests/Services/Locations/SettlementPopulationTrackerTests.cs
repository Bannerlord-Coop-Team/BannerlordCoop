using Common.Messaging;
using Common.Util;
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
    public void PrisonerReleasedIntoPlayerParty_RemovesExistingAmbientRosterEntry()
    {
        var (instanceId, partyIds) = CreateSettlement("A");
        var (heroId, characterId) = CreateHeroCharacter();
        string[] ids = instanceId.Split('|');

        Server.Call(() =>
        {
            Settlement settlement = Server.GetRegisteredObject<Settlement>(ids[0]);
            MobileParty playerParty = Server.GetRegisteredObject<MobileParty>(partyIds[0]);
            Hero prisoner = Server.GetRegisteredObject<Hero>(heroId);
            Location location = Server.GetRegisteredObject<Location>(ids[1]);

            Server.Resolve<SettlementPopulationTracker>().OnPartyEnteredSettlement(settlement, playerParty);

            prisoner._heroState = Hero.CharacterStates.Prisoner;
            prisoner.PartyBelongedToAsPrisoner = settlement.Party;
            location.AddCharacter(LocationCharacterFactory.Create(
                prisoner.CharacterObject,
                originParty: null,
                specialItem: null,
                spawnTag: "npc_common",
                actionSetCode: null,
                behaviorsMethodName: null,
                characterRelation: (int)LocationCharacter.CharacterRelations.Neutral,
                fixedLocation: false,
                useCivilianEquipment: true));

            Assert.Same(location, settlement.LocationComplex.GetLocationOfCharacter(prisoner));
        });
        Server.NetworkSentMessages.Clear();

        Server.Call(() =>
        {
            Settlement settlement = Server.GetRegisteredObject<Settlement>(ids[0]);
            MobileParty playerParty = Server.GetRegisteredObject<MobileParty>(partyIds[0]);
            Hero prisoner = Server.GetRegisteredObject<Hero>(heroId);

            using (new AllowedThread())
            {
                prisoner.PartyBelongedToAsPrisoner = null;
                prisoner._heroState = Hero.CharacterStates.Active;
                prisoner.PartyBelongedTo = playerParty;
            }

            Server.Resolve<IMessageBroker>().Publish(
                this,
                new SettlementRosterHeroesChanged(settlement, new[] { prisoner }));
        });

        NetworkLocationRosterSnapshot snapshot = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkLocationRosterSnapshot>());
        Assert.DoesNotContain(snapshot.Entries, entry => entry.CharacterId == characterId);
        Server.Call(() => Assert.Null(
            Server.GetRegisteredObject<Settlement>(ids[0]).LocationComplex.GetLocationOfCharacter(
                Server.GetRegisteredObject<Hero>(heroId))));
    }
}
