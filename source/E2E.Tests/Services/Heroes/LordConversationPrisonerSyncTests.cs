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

    [Fact]
    public void TakeLordPrisoner_LordInAnotherParty_StaysInThatParty()
    {
        var (_, playerPartyId) = CreatePlayerHeroParty("PlayerOne");
        var otherPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var memberHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(otherPartyId, out var other));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(memberHeroId, out var member));
            // A stale dialog: another party took the hero in before the answer arrived.
            other.MemberRoster.AddToCounts(member.CharacterObject, 1);
            Assert.Same(other, member.PartyBelongedTo);
            Assert.NotSame(member, other.LeaderHero);
        });

        PublishFromClient(playerPartyId, memberHeroId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(memberHeroId, out var member));
            Assert.False(member.IsPrisoner);
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(otherPartyId, out var other));
            Assert.Same(other, member.PartyBelongedTo);
        });
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
