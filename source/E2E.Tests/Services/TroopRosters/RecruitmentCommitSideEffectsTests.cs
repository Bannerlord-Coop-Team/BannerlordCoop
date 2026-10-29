using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Services.TroopRosters.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.TroopRosters;

public class RecruitmentCommitSideEffectsTests : IDisposable
{
    private const int TroopCost = 10;
    private const int StartingGold = 1000;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;

    private readonly string partyId;
    private readonly string leaderId;
    private readonly string notableId;
    private readonly string[] troopIds;

    public RecruitmentCommitSideEffectsTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        leaderId = TestEnvironment.CreateRegisteredObject<Hero>();
        notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        troopIds = new[]
        {
            TestEnvironment.CreateRegisteredObject<CharacterObject>(),
            TestEnvironment.CreateRegisteredObject<CharacterObject>(),
            TestEnvironment.CreateRegisteredObject<CharacterObject>(),
        };

        Server.Call(() =>
        {
            var party = Get<MobileParty>(partyId);
            var leader = Get<Hero>(leaderId);
            using (new AllowedThread())
            {
                party.MemberRoster.AddToCounts(leader.CharacterObject, 1);
                leader.PartyBelongedTo = party;
                party.ChangePartyLeader(leader);
                leader.Gold = StartingGold;
            }

            var notable = Get<Hero>(notableId);
            for (int i = 0; i < troopIds.Length; i++)
            {
                notable.VolunteerTypes[i] = Get<CharacterObject>(troopIds[i]);
            }

            // Set after the leader joins its roster, because adding a hero reads the default models
            Campaign.Current.Models.PartyWageModel = new FixedRecruitCostWageModel();

            // The server registers vanilla's recruitment listeners like any campaign
            new RecruitmentCampaignBehavior().RegisterEvents();
        });
    }

    public void Dispose() => TestEnvironment.Dispose();

    [Fact]
    public void ServerMainHeroDead_RecruitsTheWholeCart_AndChargesItOnce()
    {
        // Birth and Aging can kill the dedicated server's hidden main hero, and death clears its HeroDeveloper
        Server.Call(() => Hero.MainHero.OnDeath());

        int skipped = 0;
        var cart = troopIds.Select((troopId, index) => new TroopInfo(notableId, troopId, index)).ToArray();
        Server.Call(() => skipped = Server.Resolve<ITroopRosterInterface>().HandleOnRecruitmentDone(partyId, cart));

        Assert.Equal(0, skipped);
        Server.Call(() =>
        {
            Assert.Null(Hero.MainHero.HeroDeveloper);
            var roster = Get<MobileParty>(partyId).MemberRoster;
            var notable = Get<Hero>(notableId);
            for (int i = 0; i < troopIds.Length; i++)
            {
                Assert.Equal(1, roster.GetTroopRoster().Where(e => e.Character == Get<CharacterObject>(troopIds[i])).Sum(e => e.Number));
                Assert.Null(notable.VolunteerTypes[i]);
            }
            Assert.Equal(StartingGold - (troopIds.Length * TroopCost), Get<Hero>(leaderId).Gold);
        });
    }

    private T Get<T>(string id) where T : class
    {
        Assert.True(Server.ObjectManager.TryGetObject<T>(id, out var obj));
        return obj;
    }

    private sealed class FixedRecruitCostWageModel : PartyWageModel
    {
        public override int MaxWagePaymentLimit => 0;

        public override int GetCharacterWage(CharacterObject character) => 0;

        public override ExplainedNumber GetTotalWage(MobileParty mobileParty, TaleWorlds.CampaignSystem.Roster.TroopRoster troopRoster, bool includeDescriptions = false) => new(0f);

        public override ExplainedNumber GetTroopRecruitmentCost(CharacterObject troop, Hero buyerHero, bool withoutItemCost = false) => new(TroopCost);
    }
}
