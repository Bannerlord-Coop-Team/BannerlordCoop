using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Services.TroopRosters.Messages;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.TroopRosters;

/// <summary>
/// The server commit of a client recruit cart (<see cref="ITroopRosterInterface.HandleOnRecruitmentDone"/>). The cart
/// can be stale when an AI lord or the daily volunteer update changed a notable's slot after the client opened the
/// recruit screen, so only entries whose slot still holds the requested troop are recruited, each slot once.
/// </summary>
public class RecruitmentCommitTests : IDisposable
{
    private const int TroopCost = 10;
    private const int StartingGold = 1000;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;

    private readonly MethodBase[] disabledMethods =
    {
        AccessTools.Method(typeof(RecruitmentSideEffects), nameof(RecruitmentSideEffects.Apply)),
    };

    private string partyId;
    private string leaderId;
    private string notableId;
    private string infantryId;
    private string archerId;

    public RecruitmentCommitTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        leaderId = TestEnvironment.CreateRegisteredObject<Hero>();
        notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        infantryId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        archerId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

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
            notable.VolunteerTypes[0] = Get<CharacterObject>(infantryId);
            notable.VolunteerTypes[1] = Get<CharacterObject>(archerId);

            // Set after the leader joins its roster, because adding a hero reads the default models
            Campaign.Current._gameModels = new GameModels(new List<GameModel> { new FixedRecruitCostWageModel() });
        });
    }

    public void Dispose() => TestEnvironment.Dispose();

    [Fact]
    public void SlotEmptiedBeforeCommit_RecruitsOnlyTheRemainingSlot()
    {
        Server.Call(() => Get<Hero>(notableId).VolunteerTypes[0] = null);

        int skipped = Recruit(new TroopInfo(notableId, infantryId, 0), new TroopInfo(notableId, archerId, 1));

        Assert.Equal(1, skipped);
        AssertRecruited(infantry: 0, archers: 1);
    }

    [Fact]
    public void SlotNowHoldsADifferentTroop_DoesNotRecruitTheStaleTroop()
    {
        Server.Call(() => Get<Hero>(notableId).VolunteerTypes[0] = Get<CharacterObject>(archerId));

        int skipped = Recruit(new TroopInfo(notableId, infantryId, 0));

        Assert.Equal(1, skipped);
        AssertRecruited(infantry: 0, archers: 0);
        Server.Call(() => Assert.Same(Get<CharacterObject>(archerId), Get<Hero>(notableId).VolunteerTypes[0]));
    }

    [Fact]
    public void IndexOutOfRange_IsSkipped_AndTheRestOfTheCartIsRecruited()
    {
        int skipped = Recruit(new TroopInfo(notableId, infantryId, 6), new TroopInfo(notableId, archerId, 1));

        Assert.Equal(1, skipped);
        AssertRecruited(infantry: 0, archers: 1);
    }

    [Fact]
    public void SameSlotTwiceInOneCart_RecruitsOneTroop()
    {
        int skipped = Recruit(new TroopInfo(notableId, infantryId, 0), new TroopInfo(notableId, infantryId, 0));

        Assert.Equal(1, skipped);
        AssertRecruited(infantry: 1, archers: 0);
        Server.Call(() => Assert.Null(Get<Hero>(notableId).VolunteerTypes[0]));
    }

    [Fact]
    public void NotEnoughGold_RecruitsNothing_AndSkipsTheWholeCart()
    {
        const int startingGold = (TroopCost * 2) - 1;
        Server.Call(() =>
        {
            using (new AllowedThread())
            {
                Get<Hero>(leaderId).Gold = startingGold;
            }
        });

        int skipped = Recruit(new TroopInfo(notableId, infantryId, 0), new TroopInfo(notableId, archerId, 1));

        Assert.Equal(2, skipped);
        AssertRecruited(infantry: 0, archers: 0, startingGold);
        Server.Call(() =>
        {
            Assert.Same(Get<CharacterObject>(infantryId), Get<Hero>(notableId).VolunteerTypes[0]);
            Assert.Same(Get<CharacterObject>(archerId), Get<Hero>(notableId).VolunteerTypes[1]);
        });
    }

    [Fact]
    public void UnknownParty_RecruitsNothing_AndSkipsNothing()
    {
        int skipped = 0;
        var cart = new[] { new TroopInfo(notableId, infantryId, 0) };
        Server.Call(() => skipped = Server.Resolve<ITroopRosterInterface>().HandleOnRecruitmentDone("unknown party", cart), disabledMethods);

        Assert.Equal(0, skipped);
        AssertRecruited(infantry: 0, archers: 0);
        Server.Call(() => Assert.Same(Get<CharacterObject>(infantryId), Get<Hero>(notableId).VolunteerTypes[0]));
    }

    private int Recruit(params TroopInfo[] cart)
    {
        int skipped = 0;
        Server.Call(() => skipped = Server.Resolve<ITroopRosterInterface>().HandleOnRecruitmentDone(partyId, cart), disabledMethods);
        return skipped;
    }

    private void AssertRecruited(int infantry, int archers, int startingGold = StartingGold)
    {
        Server.Call(() =>
        {
            var party = Get<MobileParty>(partyId);
            Assert.Equal(infantry, party.MemberRoster.GetTroopRoster().Where(e => e.Character == Get<CharacterObject>(infantryId)).Sum(e => e.Number));
            Assert.Equal(archers, party.MemberRoster.GetTroopRoster().Where(e => e.Character == Get<CharacterObject>(archerId)).Sum(e => e.Number));
            Assert.Equal(startingGold - ((infantry + archers) * TroopCost), Get<Hero>(leaderId).Gold);
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
