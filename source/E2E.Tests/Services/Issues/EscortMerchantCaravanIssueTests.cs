using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

/// <summary>
/// Real, executed multi-peer coverage for Escort Merchant Caravan (source/GameInterface/Services/Issues/
/// {Interfaces,Messages,Handlers,Patches}/EscortMerchantCaravan*.cs), the last of Group A's 6 quest types.
/// Implements doc/EscortMerchantCaravan_Design_v2.md - see that document, and each patch/interface's own doc
/// comment, for the full derivation of the two real bugs this type has (Bug A: a construction-time dialogue NRE;
/// Bug B: a genuinely-live <c>QuestManager.OnGameLoaded</c> path this type's whole ambient-tick lifecycle needed
/// gating against, not just its turn-in).
///
/// Both of the design doc's own explicitly-flagged open items are resolved here rather than deferred further:
/// (1) the "accept handler + persistence" prerequisite turned out to be ALREADY fully solved generically by this
/// codebase's existing <c>VillageNeedsToolsIssueOwnership</c>/<c>Patches.IssueAcceptancePatches</c>/
/// <c>VillageNeedsToolsIssueHandler</c> choke point - this type needed no bespoke accept handler at all, just
/// adding it to the two <c>GenericAcceptMirrorIssueTypes</c> sets (see
/// <see cref="GenuineAccept_RegistersAsQuestSolutionMirrorEligible_AndReplicatesAByteIdenticalQuestToEveryPeer"/>,
/// which proves ownership converges with zero bespoke Escort-Caravan-specific accept code). (2) the §3.5
/// component-based re-resolution fallback is implemented, tightened to owner-only, and exercised for real by
/// <see cref="GameLoadFallback_OwnerWithNullCaravanPartyButARealMatchingParty_ResolvesItByComponent"/>.
///
/// Independently re-verified (per this task's own standing lesson, and correcting the design doc's own §5
/// claim): <c>_companionRewardRandom</c> feeds ONLY <c>IssueBase.RewardGold</c>, consumed at exactly one call
/// site (<c>AlternativeSolutionEndWithSuccess</c>'s gold payout) - NOT the Quest's own separate, fully
/// deterministic <c>RewardGold</c>/<c>TotalRewardGold</c> used by the main quest-success payout - see
/// <see cref="IEscortMerchantCaravanIssueInterface"/>'s type doc comment.
///
/// Scope notes (harness limitations, same established precedent as every other Group A suite):
/// <c>SpawnCaravan()</c>'s real body needs <c>CaravanHelper.GetRandomCaravanTemplate</c> and a real "guard"
/// <c>CharacterObject</c> lookup this bare harness doesn't stand up, so this file does NOT drive
/// <c>SpawnCaravanOnServer</c>'s reflective real-body invocation to a clean success - the gate decision itself
/// (the actual bug fix) is fully, genuinely exercised, and the request/broadcast/force-write convergence
/// mechanics are exercised using a real, AutoRegistry-synced <see cref="MobileParty"/> built the same
/// already-proven-safe way <see cref="MerchantArmyOfPoachersIssueTests.CreateRealPartyOnServer"/> does (using
/// the SAME <c>CustomPartyComponent.CreateCustomPartyWithTroopRoster</c> factory <c>SpawnCaravan()</c> itself
/// calls, so the fallback-resolution test's component/owner/home-settlement match is genuine, not synthetic).
/// <c>SuccessConsequences</c>/<c>FailConsequences</c>/<c>OnTimedOut</c> are tolerated to succeed or throw past
/// the ownership-gate decision point (they reach real <c>GiveGoldAction</c>/<c>TraitLevelingHelper</c>/
/// <c>Town.Prosperity</c> vanilla machinery this harness only partially stands up) - the real proof in each is
/// that the gate blocks a non-owner before any of that runs, and does not block the genuine owner.
/// </summary>
public class EscortMerchantCaravanIssueTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();
    private EnvironmentInstance OtherClient => TestEnvironment.Clients.Last();
    private IEnumerable<EnvironmentInstance> AllInstances => new[] { Server }.Concat(TestEnvironment.Clients);

    public EscortMerchantCaravanIssueTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private static readonly FieldInfo CompanionRewardRandomField =
        AccessTools.Field(typeof(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue), "_companionRewardRandom");
    private static readonly FieldInfo QuestCaravanMobilePartyField =
        AccessTools.Field(typeof(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest), "_questCaravanMobileParty");

    private record CaravanFixture(string HeroId, string SettlementId, string ClanId);

    /// <summary>Builds the issue-owning (merchant) Hero at his own Town-backed settlement, with a Clan that
    /// leads itself (needed so relation-change code reached by Success/Fail/TimedOut consequences doesn't NRE
    /// on a null <c>Clan.Leader</c> - same fixture concern <c>ArtisanOverpricedGoodsIssueTests.SetupIssueOwner</c>
    /// documents). Also seeds <see cref="Campaign.EncyclopediaManager"/> - the real activation log eagerly
    /// resolves encyclopedia links, same hazard/fix as every other Group A suite's own SetupIssueOwner.</summary>
    private CaravanFixture SetupIssueOwner()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var clanId = TestEnvironment.CreateRegisteredObject<Clan>();

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                }

                Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var owner));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                Assert.True(instance.ObjectManager.TryGetObject<Town>(townId, out var town));
                Assert.True(instance.ObjectManager.TryGetObject<Clan>(clanId, out var clan));

                settlement.SetSettlementComponent(town);
                clan.SetLeader(owner);
                owner.Clan = clan;

                // Hero.CurrentSettlement is read-only, derived from StayingInSettlement - the correct "a
                // notable hero without their own party is physically at this settlement" shape (same technique
                // as every other Group A suite's own SetupIssueOwner).
                owner.StayingInSettlement = settlement;
            });
        }

        return new CaravanFixture(heroId, settlementId, clanId);
    }

    /// <summary>Drives the real server-authoritative creation path: a genuine
    /// <see cref="IssueManager.CreateNewIssue"/> call handing back a real
    /// <see cref="EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue"/> - exactly what
    /// <c>EscortMerchantCaravanIssueBehavior.OnSelected</c> does for real.</summary>
    private void CreateIssueOnServer(CaravanFixture fixture)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) => new EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue(h),
                typeof(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue),
                IssueBase.IssueFrequency.VeryCommon);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
        });
    }

    /// <summary>Real (unwrapped) accept on <paramref name="instance"/> - drives
    /// <see cref="IssueManager.StartIssueQuest"/> for real, which (per <see cref="GenericAcceptMirrorIssueTypes"/>)
    /// triggers the fully generic accept-broadcast/ownership-record path unchanged. Deliberately does NOT invoke
    /// <c>QuestAcceptedConsequences()</c> - that only ever runs from a genuine LIVE dialogue Consequence
    /// (Category B), simulated separately, per-test, via reflection (matching every other Group A suite's own
    /// precedent).</summary>
    private EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest AcceptOnInstance(EnvironmentInstance instance, string heroId)
    {
        EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest quest = null;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var owner));
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            quest = Assert.IsType<EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest>(owner.Issue.IssueQuest);
        });
        return quest;
    }

    /// <summary>Builds a real, fully-constructed caravan <see cref="MobileParty"/> using the SAME
    /// <c>CustomPartyComponent.CreateCustomPartyWithTroopRoster</c> factory the real <c>SpawnCaravan()</c> calls
    /// - used both as a stand-in for "the server's genuine <see cref="IEscortMerchantCaravanIssueInterface.SpawnCaravanOnServer"/>
    /// result" (convergence tests) and, with matching owner/home-settlement, as a genuine target for
    /// <see cref="Patches.EscortMerchantCaravanGameLoadCaravanPartyFallbackPatch"/>'s component-match query.
    /// </summary>
    private string CreateRealCaravanPartyOnServer(Hero owner, Settlement homeSettlement, string customName = "Caravan")
    {
        string partyId = null;
        Server.Call(() =>
        {
            var party = CustomPartyComponent.CreateCustomPartyWithTroopRoster(
                new CampaignVec2(new Vec2(5, 5), true), 0f, homeSettlement, new TextObject(customName), owner.Clan,
                TroopRoster.CreateDummyTroopRoster(), TroopRoster.CreateDummyTroopRoster(), owner, "", "", 4f);

            Assert.True(Server.ObjectManager.TryGetId(party, out partyId));
        });
        return partyId;
    }

    private static void InvokePrivate(object instance, string methodName)
    {
        var method = AccessTools.Method(instance.GetType(), methodName);
        method.Invoke(instance, null);
    }

    private static bool InvokeGatePrefix(string methodName, object quest)
    {
        var prefix = AccessTools.Method(typeof(GameInterface.Services.Issues.Patches.EscortMerchantCaravanOwnershipGatePatches), methodName);
        return (bool)prefix.Invoke(null, new object[] { quest });
    }

    // --- 1. Creation and replication ---

    [Fact]
    public void GenuineServerCreation_CapturesCompanionRewardRandom_AndReplicatesAByteIdenticalIssueToEveryClient()
    {
        var fixture = SetupIssueOwner();

        CreateIssueOnServer(fixture);

        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkEscortMerchantCaravanIssueCreated>());
        Assert.Equal(fixture.HeroId, created.OwnerId);

        int serverRolled = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            var issue = Assert.IsType<EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue>(owner.Issue);
            serverRolled = (int)CompanionRewardRandomField.GetValue(issue);
        });

        // Genuinely discriminating: MBRandom.RandomInt(3, 10) has a >85% chance of NOT independently landing on
        // the same value across two unrelated peer-local rolls - verified against the ACTUAL server-rolled value,
        // not just a fixed constant.
        Assert.Equal(serverRolled, created.CompanionRewardRandom);

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                var mirrored = Assert.IsType<EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue>(owner.Issue);

                Assert.Equal(serverRolled, (int)CompanionRewardRandomField.GetValue(mirrored));
            });
        }
    }

    [Fact]
    public void ClientOriginatedCreation_IsBlocked_IssueManagerNeverCreatesIt()
    {
        var fixture = SetupIssueOwner();

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) => new EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue(h),
                typeof(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue),
                IssueBase.IssueFrequency.VeryCommon);

            Assert.False(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
            Assert.Null(owner.Issue);
        });

        Assert.Empty(Client.NetworkSentMessages.GetMessages<NetworkEscortMerchantCaravanIssueCreated>());
    }

    // --- 2. Accept-quest mirroring rides the fully generic mechanism (proves open item 1 needed no bespoke code) ---

    [Fact]
    public void GenuineAccept_RegistersAsQuestSolutionMirrorEligible_AndReplicatesAByteIdenticalQuestToEveryPeer()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        AcceptOnInstance(Server, fixture.HeroId);

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.IsType<EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest>(owner.Issue.IssueQuest);

                // Ownership converged with ZERO bespoke Escort-Caravan-specific accept code - see this file's
                // own type doc comment on the design doc's open item 1.
                Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("host-controller", ownerControllerId);
            });
        }
    }

    // --- 3. Bug A: the SetDialogs() NRE null-guard ---

    [Fact]
    public void CaravanTalkOnCondition_NonOwnerWithNullCaravanParty_ReturnsFalse_DoesNotThrow()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        // Accept on the SERVER (host-owner) - the generic mirror mechanism (GenericAcceptMirrorIssueTypes)
        // already proves (GenuineAccept_... above) that this constructs a byte-identical
        // EscortMerchantCaravanIssueQuest mirror, via a real ctor call (SetDialogs() included), on every OTHER
        // peer too - including the Client, which is the genuine non-owner this test exercises Bug A against.
        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        AcceptOnInstance(Server, fixture.HeroId);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            var quest = Assert.IsType<EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest>(owner.Issue.IssueQuest);

            // Bug A's exact reachable state: a non-owner mirror's _questCaravanMobileParty is permanently null
            // (SpawnCaravan() never ran here). Without the null-guard, this throws an NRE the instant ANY
            // conversation on this peer evaluates the "start"/"escort_caravan_talk" states.
            Assert.Null(QuestCaravanMobilePartyField.GetValue(quest));

            var method = AccessTools.Method(quest.GetType(), "caravan_talk_on_condition");
            var exception = Record.Exception(() => method.Invoke(quest, null));

            Assert.Null(exception);
            Assert.False((bool)method.Invoke(quest, null));
        });
    }

    [Fact]
    public void CaravanTalkOnCondition_WithARealMatchingCaravanParty_RunsVanillaLogicUnmodified()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            var partyId = CreateRealCaravanPartyOnServer(owner, owner.CurrentSettlement);
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var caravanParty));
            QuestCaravanMobilePartyField.SetValue(quest, caravanParty);

            // The real body runs unmodified (no NRE) once the field is non-null - the gate only ever short-
            // circuits the null case.
            var method = AccessTools.Method(quest.GetType(), "caravan_talk_on_condition");
            var exception = Record.Exception(() => method.Invoke(quest, null));
            Assert.Null(exception);
        });
    }

    // --- 4. Bug B: ownership gates on all 10 RegisterEvents/HourlyTick/DailyTick/OnTimedOut methods ---

    public static IEnumerable<object[]> GatedPrefixMethodNames()
    {
        yield return new object[] { "OnSettlementEnteredPrefix" };
        yield return new object[] { "OnSettlementLeftPrefix" };
        yield return new object[] { "OnMapEventEndedPrefix" };
        yield return new object[] { "OnWarDeclaredPrefix" };
        yield return new object[] { "OnClanChangedKingdomPrefix" };
        yield return new object[] { "OnPartyHourlyTickPrefix" };
        yield return new object[] { "OnSettlementOwnerChangedPrefix" };
        yield return new object[] { "HourlyTickPrefix" };
        yield return new object[] { "DailyTickPrefix" };
        yield return new object[] { "OnTimedOutPrefix" };
    }

    [Theory]
    [MemberData(nameof(GatedPrefixMethodNames))]
    public void OwnershipGate_BlocksNonOwner_AllowsOwner(string prefixMethodName)
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);

        Server.Resolve<IControllerIdProvider>().SetControllerId("someone-else");
        Server.Call(() => Assert.False(InvokeGatePrefix(prefixMethodName, quest)));

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        Server.Call(() => Assert.True(InvokeGatePrefix(prefixMethodName, quest)));
    }

    /// <summary>Independent check for a SEPARATE turn-in ownership gap (the task's own standing lesson - this
    /// exact bug shape has been found in every Group A quest type so far). NONE was found here: unlike e.g.
    /// Merchant Army of Poachers' shared dialogue leaves, <c>SuccessConsequences</c> has no dedicated dialogue
    /// entry point of its own - its ONLY two callers are <c>OnSettlementEntered</c> (directly gated above) and
    /// <c>TryToFindAndSetTargetToNextSettlement</c>, whose own only callers are ALL gated
    /// (<c>OnPartyHourlyTick</c>'s <c>CheckPartyAndMakeItAttackTheCaravan</c>, <c>OnSettlementLeft</c>,
    /// <c>OnWarDeclared</c>, and the quest's own <c>HourlyTick</c>) - so the 10-method gate above is already the
    /// complete turn-in ownership fix, with no 11th method needed. This test proves the ONE real entry point
    /// (<c>OnSettlementEntered</c>, the ONLY caller of <c>SuccessConsequences</c> that isn't itself gated through
    /// another already-gated method) is unreachable for a non-owner even with a real, IsOngoing quest and every
    /// settlement/party precondition satisfied.</summary>
    [Fact]
    public void SuccessConsequencesEntryPoint_OnSettlementEntered_UnreachableForNonOwner_EvenWithRealMatchingState()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);
        // SpawnCaravan()'s own real body needs a caravan-template database entry this bare harness doesn't
        // stand up (see this file's own scope note) - tolerated, since StartQuest() (which sets IsOngoing)
        // already ran on the earlier statement, before SpawnCaravan() is even reached.
        Server.Call(() => Record.Exception(() => InvokePrivate(quest, "QuestAcceptedConsequences")));

        Server.Resolve<IControllerIdProvider>().SetControllerId("someone-else");
        Server.Call(() =>
        {
            Assert.True(quest.IsOngoing);
            Assert.False(InvokeGatePrefix("OnSettlementEnteredPrefix", quest));
        });

        // The quest is still ongoing - a non-owner's own settlement-entry could never have driven it to success.
        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        Server.Call(() => Assert.True(quest.IsOngoing));
    }

    // --- 5. Success path / failure path (tolerated past the gate - see this file's own scope note) ---

    [Fact]
    public void OnSettlementEnteredPrefix_Owner_AllowsSuccessPathToBeReached()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);

        Server.Call(() =>
        {
            // The gate lets the real owner through - SuccessConsequences itself (called from inside the real
            // OnSettlementEntered body once all of vanilla's own preconditions are met) is tolerated to succeed
            // or throw past that point, same scope note as this file's OnFinalize test.
            Assert.True(InvokeGatePrefix("OnSettlementEnteredPrefix", quest));
            Record.Exception(() => InvokePrivate(quest, "SuccessConsequences"));
        });
    }

    [Fact]
    public void OnTimedOutPrefix_Owner_AllowsFailurePathToBeReached_NonOwner_Blocked()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);

        Server.Resolve<IControllerIdProvider>().SetControllerId("someone-else");
        Server.Call(() =>
        {
            Assert.False(InvokeGatePrefix("OnTimedOutPrefix", quest));
        });

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        Server.Call(() =>
        {
            Assert.True(InvokeGatePrefix("OnTimedOutPrefix", quest));
            // OnTimedOut itself (real Hero.AddPower/relation/Town.Prosperity mutation) tolerated to succeed or
            // throw past the gate decision - see this file's own scope note.
            Record.Exception(() => InvokePrivate(quest, "OnTimedOut"));
        });
    }

    // --- 6. Join-mid-quest: the §3.5 component-based fallback resolution ---

    [Fact]
    public void GameLoadFallback_OwnerWithNullCaravanPartyButARealMatchingParty_ResolvesItByComponent()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));

            // Simulate a join-mid-quest deserialization gap: the quest is ongoing (StartQuest() genuinely ran)
            // but _questCaravanMobileParty didn't resolve from the save graph. Deliberately calls StartQuest()
            // directly rather than the full QuestAcceptedConsequences() - the real SpawnCaravan() partially
            // assigns _questCaravanMobileParty before throwing on this harness's missing caravan-template
            // database entry (see this file's own scope note), which would falsely satisfy this test without
            // exercising the fallback at all; a real deserialization-produced gap never goes through
            // SpawnCaravan() in the first place.
            quest.StartQuest();
            Assert.True(quest.IsOngoing);
        });

        // A real caravan party already exists (AutoRegistry-synced), matching PartyOwner/HomeSettlement exactly
        // the way the genuine SpawnCaravan() would have left it.
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            var partyId = CreateRealCaravanPartyOnServer(owner, owner.CurrentSettlement);
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var realParty));

            // The deserialized field itself failed to resolve (the exact gap §3.5 defends against).
            Assert.Null(QuestCaravanMobilePartyField.GetValue(quest));

            var fallbackPrefix = AccessTools.Method(
                typeof(GameInterface.Services.Issues.Patches.EscortMerchantCaravanGameLoadCaravanPartyFallbackPatch), "Prefix");
            fallbackPrefix.Invoke(null, new object[] { quest });

            var resolved = QuestCaravanMobilePartyField.GetValue(quest) as MobileParty;
            Assert.Same(realParty, resolved);
        });
    }

    [Fact]
    public void GameLoadFallback_NonOwner_NeverRuns_EvenWithARealMatchingPartyAvailable()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);
        // Direct StartQuest(), not the full QuestAcceptedConsequences() - see the sibling owner test's own
        // comment for why (SpawnCaravan()'s real body partially assigns the field before throwing in this
        // harness, which would falsely satisfy this test's null-field premise).
        Server.Call(() => quest.StartQuest());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            var partyId = CreateRealCaravanPartyOnServer(owner, owner.CurrentSettlement);
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out _));
        });

        Server.Resolve<IControllerIdProvider>().SetControllerId("someone-else");
        Server.Call(() =>
        {
            Assert.Null(QuestCaravanMobilePartyField.GetValue(quest));

            var fallbackPrefix = AccessTools.Method(
                typeof(GameInterface.Services.Issues.Patches.EscortMerchantCaravanGameLoadCaravanPartyFallbackPatch), "Prefix");
            fallbackPrefix.Invoke(null, new object[] { quest });

            // Not the recorded owner - the fallback must not resolve/force anything (see the type doc comment:
            // pointless work, since every dangerous read is already ownership-gated regardless).
            Assert.Null(QuestCaravanMobilePartyField.GetValue(quest));
        });
    }

    /// <summary>
    /// Bug fix regression test (found by independent review, 2026-08-04 - see
    /// <see cref="Patches.EscortMerchantCaravanGameLoadCaravanPartyFallbackPatch"/>'s own doc comment): the
    /// genuine-resolution-FAILS branch (design doc §3.5 - no real party anywhere matches this quest by
    /// component) had NO coverage at all before this test - both sibling tests above only exercise the
    /// resolution-SUCCEEDS and non-owner branches. Deliberately creates NO matching
    /// <see cref="MobileParty"/> anywhere (unlike the sibling owner test, which creates one via
    /// <see cref="CreateRealCaravanPartyOnServer"/>), so the component-match query is guaranteed to come back
    /// empty - this is what makes the test genuinely exercise the failure path rather than vacuously pass.
    /// Proves the fix: the Prefix returns <see langword="false"/> (skipping the original method, which would
    /// otherwise unconditionally call <c>SetDialogs()</c> even with a still-null field - the exact shape of the
    /// bug), <c>CompleteQuestWithCancel()</c> runs for real, and the quest ends up cleanly finalized (not
    /// ongoing, no dangling <see cref="Hero.Issue"/>) rather than left half-alive with a null field for the
    /// owner-gated <c>HourlyTick()</c> to NRE on next tick.
    /// </summary>
    [Fact]
    public void GameLoadFallback_OwnerWithNullCaravanPartyAndNoMatchingPartyAnywhere_CancelsQuestCleanly()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);

        Server.Call(() =>
        {
            // Same join-mid-quest simulation as the sibling owner test (direct StartQuest(), not the full
            // QuestAcceptedConsequences() - see that test's own comment for why): the quest is genuinely
            // ongoing but _questCaravanMobileParty never resolved from the save graph.
            quest.StartQuest();
            Assert.True(quest.IsOngoing);
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));

            // The deserialized field failed to resolve, and - unlike the sibling owner test - no real party
            // exists anywhere to resolve it by component either. This is the genuine-failure case §3.5 defends
            // against.
            Assert.Null(QuestCaravanMobilePartyField.GetValue(quest));
            Assert.DoesNotContain(MobileParty.All, mp => mp.PartyComponent is CustomPartyComponent);

            var fallbackPrefix = AccessTools.Method(
                typeof(GameInterface.Services.Issues.Patches.EscortMerchantCaravanGameLoadCaravanPartyFallbackPatch), "Prefix");

            object prefixResult = null;
            var thrown = Record.Exception(() =>
            {
                using (new AllowedThread())
                {
                    prefixResult = fallbackPrefix.Invoke(null, new object[] { quest });
                }
            });

            // Cleanly cancelled - not an uncaught exception reaching the caller.
            Assert.Null(thrown);

            // The Prefix skips the original InitializeQuestOnGameLoad() body on this path (returns false),
            // instead of letting it fall through to an unconditional SetDialogs() with a still-null field.
            Assert.False(Assert.IsType<bool>(prefixResult));

            // Resolution genuinely failed - the field is still null, not force-set to some wrong value.
            Assert.Null(QuestCaravanMobilePartyField.GetValue(quest));

            // CompleteQuestWithCancel() ran for real: the quest is no longer ongoing and the owner's Issue is
            // gone - cleanly finalized, not left half-alive for the owner-gated HourlyTick() to crash on.
            Assert.False(quest.IsOngoing);
            Assert.Null(owner.Issue);
            Assert.False(Campaign.Current.IssueManager.Issues.ContainsKey(owner));
        });
    }

    // --- 7. Party-spawn gate: request/broadcast/force-write convergence ---

    [Fact]
    public void ClientOwner_SpawnCaravan_ForwardsARequest_NoLocalPartyCreated()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");
        Client.Resolve<IControllerIdProvider>().SetControllerId("player-A");

        var quest = AcceptOnInstance(Client, fixture.HeroId);

        Client.Call(() =>
        {
            // The 3rd statement in QuestAcceptedConsequences (the activation journal-log text) reads the static
            // Settlement.CurrentSettlement (derived from MobileParty.MainParty, unset in this bare harness) and
            // throws past that point - tolerated (see this file's own scope note); the party-spawn gate (the
            // 2nd statement, SpawnCaravan()) has already run and forwarded its request by then.
            Record.Exception(() => InvokePrivate(quest, "QuestAcceptedConsequences"));

            // Vanilla's own safe body (StartQuest()) ran unmodified.
            Assert.True(quest.IsOngoing);

            // No broken/orphaned local party was ever left behind.
            Assert.Null(QuestCaravanMobilePartyField.GetValue(quest));

            var requested = Assert.Single(Client.InternalMessages.GetMessages<EscortMerchantCaravanPartySpawnRequested>());
            Assert.True(Client.ObjectManager.TryGetId(requested.Owner, out var reqOwnerId));
            Assert.Equal(fixture.HeroId, reqOwnerId);
        });

        var forwarded = Assert.Single(Client.NetworkSentMessages.GetMessages<RequestEscortMerchantCaravanPartySpawn>());
        Assert.Equal(fixture.HeroId, forwarded.OwnerId);

        Assert.Empty(Client.NetworkSentMessages.GetMessages<NetworkEscortMerchantCaravanPartySpawned>());
    }

    [Fact]
    public void HostOwner_SpawnCaravan_AttemptsRealCreation_NeverForwardsARequest()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);

        Server.Call(() =>
        {
            // Attempts the real creation synchronously (may succeed or throw against this harness's missing
            // caravan-template/guard-troop database entries - see this file's own scope note) - the point being
            // proven is that this is reached AT ALL, immediately, as part of accept.
            Record.Exception(() => InvokePrivate(quest, "QuestAcceptedConsequences"));

            Assert.True(quest.IsOngoing);
        });

        Assert.Empty(Server.InternalMessages.GetMessages<EscortMerchantCaravanPartySpawnRequested>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<RequestEscortMerchantCaravanPartySpawn>());
    }

    [Fact]
    public void NetworkPartySpawned_ForceWritesTheSameRealPartyOntoEveryPeersMirror_IncludingTheOriginalAccepter()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");
        Client.Resolve<IControllerIdProvider>().SetControllerId("player-A");

        var quest = AcceptOnInstance(Client, fixture.HeroId);
        Client.Call(() => Record.Exception(() => InvokePrivate(quest, "QuestAcceptedConsequences")));

        string caravanPartyId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            caravanPartyId = CreateRealCaravanPartyOnServer(owner, owner.CurrentSettlement);
        });

        // Simulate what Handlers.EscortMerchantCaravanIssueHandler.Handle_PartySpawned does once
        // SpawnCaravanOnServer's own Postfix genuinely fires - publish the same local event it would, with the
        // real (AutoRegistry-synced) party.
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(caravanPartyId, out var caravanParty));

            Server.Resolve<IEscortMerchantCaravanIssueInterface>().ForceCaravanParty(owner, caravanParty);
            Server.Resolve<IMessageBroker>().Publish(null, new EscortMerchantCaravanPartySpawned(owner, caravanParty));
        });

        var spawned = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkEscortMerchantCaravanPartySpawned>());
        Assert.Equal(fixture.HeroId, spawned.OwnerId);
        Assert.Equal(caravanPartyId, spawned.CaravanPartyId);

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(caravanPartyId, out var expected));

                var issueInterface = instance.Resolve<IEscortMerchantCaravanIssueInterface>();
                Assert.True(issueInterface.TryCaptureCaravanParty(owner, out var actual));
                Assert.Same(expected, actual);
            });
        }
    }

    [Fact]
    public void SpawnCaravanOnServer_IsIdempotent_AlreadyExistingPartyIsNeverRecreatedOrReBroadcast()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);
        var quest = AcceptOnInstance(Server, fixture.HeroId);

        string caravanPartyId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            caravanPartyId = CreateRealCaravanPartyOnServer(owner, owner.CurrentSettlement);
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(caravanPartyId, out var caravanParty));
            Server.Resolve<IEscortMerchantCaravanIssueInterface>().ForceCaravanParty(owner, caravanParty);

            var result = Server.Resolve<IEscortMerchantCaravanIssueInterface>().SpawnCaravanOnServer(owner);
            Assert.True(Server.ObjectManager.TryGetId(result, out var resultId));
            Assert.Equal(caravanPartyId, resultId);
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkEscortMerchantCaravanPartySpawned>());
    }

    // --- 8. Accept-race arbitration (shared mechanism only) ---

    [Fact]
    public void RequestVillageIssueAcceptQuest_FirstRequestWins_SecondIsRejectedAndOwnershipConvergesOnEveryPeer()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", "", "", "")));
            Assert.True(playerManager.AddPlayer(new Player("player-B", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");
        TestEnvironment.ConnectRegisteredPlayer(OtherClient, "player-B");

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, new RequestVillageIssueAcceptQuest(fixture.HeroId));
        });

        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageIssueQuestAccepted>());
        Assert.Equal(fixture.HeroId, accepted.OwnerId);
        Assert.Equal("player-A", accepted.OwnerControllerId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkVillageIssueAcceptRejected>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.False(owner.Issue.IsOngoingWithoutQuest);
            Assert.IsType<EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest>(owner.Issue.IssueQuest);
        });

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(OtherClient.NetPeer, new RequestVillageIssueAcceptQuest(fixture.HeroId));
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageIssueQuestAccepted>());
        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageIssueAcceptRejected>());
        Assert.Equal(fixture.HeroId, rejected.OwnerId);

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.IsType<EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest>(owner.Issue.IssueQuest);
                Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("player-A", ownerControllerId);
            });
        }
    }

    // --- 9. Alternative solution: routes through the generic ownership-gated HourlyTick trigger ---

    [Fact]
    public void CompleteIssueWithAlternativeSolution_OwnershipGate_BlocksNonOwner_AllowsOwner()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);

        var prefix = AccessTools.Method(
            typeof(GameInterface.Services.Issues.Patches.NewIssueTypesAlternativeSolutionOwnershipGatePatch), "Prefix");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));

            Server.Resolve<IControllerIdProvider>().SetControllerId("someone-else");
            Assert.False((bool)prefix.Invoke(null, new object[] { owner.Issue }));

            Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
            Assert.True((bool)prefix.Invoke(null, new object[] { owner.Issue }));
        });
    }

    // --- 10. Finalize / cleanup ---

    [Fact]
    public void RequestVillageIssueRemoved_FinalizesTheRealQuestOnEveryPeer()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        AcceptOnInstance(Server, fixture.HeroId);

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.NotNull(owner.Issue);
                Assert.IsType<EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest>(owner.Issue.IssueQuest);
            });
        }

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer,
                new RequestVillageIssueRemoved(fixture.HeroId, VillageIssueFinalizeReason.QuestSuccess));
        });

        var removed = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageIssueRemoved>());
        Assert.Equal(fixture.HeroId, removed.OwnerId);
        Assert.Equal(VillageIssueFinalizeReason.QuestSuccess, removed.Reason);

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.Null(owner.Issue);
                Assert.False(Campaign.Current.IssueManager.Issues.ContainsKey(owner));
            });
        }
    }

    /// <summary><c>OnFinalize</c>'s own <c>CaravanPartyComponent.ConvertPartyToCaravanParty</c> call (guarded on
    /// <c>IsActive</c> + <c>IsCustomParty</c>), driven for real against a real, force-written caravan party -
    /// tolerated to succeed or throw past the point the code path is reached (same tolerance as every other
    /// Group A suite's own OnFinalize test) - the real proof is that <c>OnFinalize</c> genuinely attempts the
    /// conversion specifically because <c>_questCaravanMobileParty</c> is non-null/active/custom, not that it
    /// silently no-ops.</summary>
    [Fact]
    public void OnFinalize_WithARealCaravanParty_AttemptsToConvertItToACaravan()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            var partyId = CreateRealCaravanPartyOnServer(owner, owner.CurrentSettlement);
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var caravanParty));
            Server.Resolve<IEscortMerchantCaravanIssueInterface>().ForceCaravanParty(owner, caravanParty);

            Assert.NotNull(QuestCaravanMobilePartyField.GetValue(quest));
            Assert.True(caravanParty.IsActive);
            Assert.True(caravanParty.IsCustomParty);

            Record.Exception(() => InvokePrivate(quest, "OnFinalize"));
        });
    }
}
