using Common.Messaging;
using E2E.Tests.Services.MapEvents;
using GameInterface.Services.Heroes.Messages.LordConversations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Heroes;

/// <summary>
/// A capture answer reaches the server after the player decides, so the server takes the lord prisoner
/// only while no party holds him: a free lord, or a prisoner the player just looted from a defeated party.
/// </summary>
public class LordConversationPrisonerSyncTests : MapEventTestBase
{
    public LordConversationPrisonerSyncTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void TakeLordPrisoner_FreeLord_BecomesThePlayersPrisonerEverywhere()
    {
        var (_, playerPartyId) = CreatePlayerHeroParty("PlayerOne");
        var lordHeroId = CreateLord();

        PublishFromClient(playerPartyId, lordHeroId);

        AssertCaptivity(Server, lordHeroId, playerPartyId);
        foreach (var client in Clients)
        {
            client.PumpGameThread();
            AssertCaptivity(client, lordHeroId, playerPartyId);
        }
    }

    [Fact]
    public void TakeLordPrisoner_LordAlreadyCaptured_StaysWithHisCaptor()
    {
        var (_, playerPartyId) = CreatePlayerHeroParty("PlayerOne");
        var lordHeroId = CreateLord();
        var captorPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(captorPartyId, out var captor));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(lordHeroId, out var lord));
            TakePrisonerAction.Apply(captor.Party, lord);
            Assert.True(lord.IsPrisoner);
        });

        PublishFromClient(playerPartyId, lordHeroId);

        AssertCaptivity(Server, lordHeroId, captorPartyId);
    }

    [Fact]
    public void TakeLordPrisoner_PrisonerLootedFromADefeatedParty_BecomesThePlayersPrisoner()
    {
        var (_, playerPartyId) = CreatePlayerHeroParty("PlayerOne");
        var lordHeroId = CreateLord();
        var defeatedPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(defeatedPartyId, out var defeated));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(lordHeroId, out var lord));
            TakePrisonerAction.Apply(defeated.Party, lord);
            // Battle looting takes him out of the defeated party's prison roster without ending his captivity.
            defeated.PrisonRoster.RemoveTroop(lord.CharacterObject);
            Assert.True(lord.IsPrisoner);
            Assert.Null(lord.PartyBelongedToAsPrisoner);
        });

        PublishFromClient(playerPartyId, lordHeroId);

        AssertCaptivity(Server, lordHeroId, playerPartyId);
    }

    private string CreateLord()
    {
        var lordPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        string lordHeroId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(lordPartyId, out var lordParty));
            Assert.NotNull(lordParty.LeaderHero);
            Assert.True(Server.ObjectManager.TryGetId(lordParty.LeaderHero, out lordHeroId));
        });
        return lordHeroId;
    }

    private void PublishFromClient(string playerPartyId, string lordHeroId)
    {
        var client = Clients.First();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(lordHeroId, out var lord));
            client.Resolve<IMessageBroker>().Publish(this, new TakeLordPrisoner(playerParty.Party, lord));
        });
        Server.PumpGameThread();
    }
}
