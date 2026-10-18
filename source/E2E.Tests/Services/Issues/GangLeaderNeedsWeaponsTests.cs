using Autofac;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Players;
using GameInterface.Services.Issues.Quests.GangLeaderNeedsWeapons.Patches;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue;
using OtherQuest = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest;
using Quest = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;

namespace E2E.Tests.Services.Issues;

public class GangLeaderNeedsWeaponsTests : IDisposable
{
    private const string IssueName = "GangLeaderNeedsWeaponsIssue";
    private const string IssueId = "issue_gang_leader_needs_weapons";
    private const string ControllerId = "player-A";
    private const int RequestedWeapons = 4;
    private const int SentTroops = 3;

    private readonly string giverId;
    private readonly string ownerHeroId;
    private readonly string ownerPartyId;
    private readonly string companionId;
    private readonly string troopId;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance ClientA => TestEnvironment.Clients.First();
    private EnvironmentInstance ClientB => TestEnvironment.Clients.Last();

    public GangLeaderNeedsWeaponsTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        giverId = TestEnvironment.CreateRegisteredObject<Hero>();
        ownerHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        ownerPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        companionId = TestEnvironment.CreateRegisteredObject<Hero>();
        troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        Server.Call(() => Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player(ControllerId, ownerHeroId, ownerPartyId, "", ""))));
        TestEnvironment.ConnectRegisteredPlayer(ClientA, ControllerId);
        ClientA.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void TheQuestIsRegisteredLikeTheOtherFrameworkQuests()
    {
        var descriptor = Descriptor();

        Assert.Equal(typeof(Issue), descriptor.IssueType);
        Assert.NotNull(descriptor.CreationCaptureStrategy);
        Assert.NotNull(descriptor.QuestSolutionAcceptStrategy);
        Assert.NotNull(descriptor.AlternativeSolutionAcceptStrategy);
        Assert.NotNull(descriptor.FinalizationProofStrategy);
    }

    [Fact]
    public void CreationCapture_ForcesTheServersWeaponClassAndPriceOntoTheClientsIssue()
    {
        byte[] captured = null!;

        Server.Call(() =>
        {
            var serverIssue = Uninitialized<Issue>();
            serverIssue._requiredWeaponClassIndex = 0;
            serverIssue._averagePriceForItem = 321;

            captured = Descriptor().CreationCaptureStrategy.Capture(serverIssue);
        });

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(giverId, out var giver));

            var clientIssue = (Issue)Descriptor().CreationCaptureStrategy.CreateIssue(giver, captured);

            Assert.Equal(0, clientIssue._requiredWeaponClassIndex);
            Assert.Equal(321, clientIssue._averagePriceForItem);
        });
    }

    [Fact]
    public void DebugCapture_IsRefusedForAGiverOutsideATown()
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));

            Assert.False(Descriptor().CreationCaptureStrategy.TryBuildDebugCapture(giver, out _));
        });
    }

    [Theory]
    [InlineData(RequestedWeapons - 1, true, false)]
    [InlineData(RequestedWeapons, true, true)]
    [InlineData(RequestedWeapons, false, false)]
    public void TheServerOnlyCompletesTheHandInWhenTheOwnerCarriesTheWeapons(int carried, bool ownerHasParty, bool completes)
    {
        var axe = CreateAxe();
        var serversOwnPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var giverSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var completed = false;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var ownerHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(serversOwnPartyId, out var serversOwnParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(giverSettlementId, out var giverSettlement));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            var previousMainParty = Campaign.Current.MainParty;

            using (new AllowedThread())
            {
                party.ItemRoster.Clear();
                party.ItemRoster.AddToCounts(axe, carried);
                ownerHero._partyBelongedTo = ownerHasParty ? party : null;
                giver.StayingInSettlement = giverSettlement;
                party.CurrentSettlement = giverSettlement;

                // The server's own party carries every requested weapon, an owner without a party must not be answered with it
                serversOwnParty.ItemRoster.Clear();
                serversOwnParty.ItemRoster.AddToCounts(axe, RequestedWeapons);
                Campaign.Current.MainParty = serversOwnParty;
            }

            var quest = new Quest("weapons_test", giver, CampaignTime.Never, 500, 0, RequestedWeapons, 0.5f, 250);
            quest._requestedWeaponClass = WeaponClass.OneHandedAxe;

            try
            {
                // What FinalizationCoordinator does before it runs a branch
                using (new MainHeroSubstitutionScope(ownerHero, ownerHero.PartyBelongedTo))
                {
                    completed = (bool)AccessTools.Method(typeof(GangLeaderNeedsWeaponsDeliveryPatch), "Prefix")
                        .Invoke(null, new object[] { quest })!;
                }
            }
            finally
            {
                using (new AllowedThread())
                {
                    Campaign.Current.MainParty = previousMainParty;
                }
            }

            Assert.Equal(RequestedWeapons, serversOwnParty.ItemRoster.GetItemNumber(axe));
        });

        Assert.Equal(completes, completed);
    }

    [Fact]
    public void TheProofStrategyOnlyKnowsTheDeliveryBranchOfItsOwnQuest()
    {
        Assert.False(Descriptor().FinalizationProofStrategy!.TryRunBranch(Uninitialized<Quest>(), proof: 0));
        Assert.False(Descriptor().FinalizationProofStrategy!.TryRunBranch(Uninitialized<OtherQuest>(), proof: 1));
    }

    [Fact]
    public void TheDeliveryBranchRunsTheHandInOfItsQuest()
    {
        var harmony = new Harmony("test-3769-delivery-branch");
        var target = AccessTools.Method(typeof(Quest), nameof(Quest.PlayerSuccessfullyDeliveredWeapons));
        var recorder = new HarmonyMethod(typeof(GangLeaderNeedsWeaponsTests), nameof(RecordHandIn)) { priority = Priority.First };
        ranHandIn = null;

        // The hand-in itself needs a whole quest, so only the call is recorded and vanilla's body is skipped
        harmony.Patch(target, prefix: recorder);

        try
        {
            var quest = Uninitialized<Quest>();

            Assert.True(Descriptor().FinalizationProofStrategy!.TryRunBranch(quest, proof: 1));
            Assert.Same(quest, ranHandIn);
        }
        finally
        {
            harmony.Unpatch(target, HarmonyPatchType.Prefix, harmony.Id);
        }
    }

    private static Quest? ranHandIn;

    private static bool RecordHandIn(Quest __instance)
    {
        ranHandIn = __instance;
        return false;
    }

    [Fact]
    public void ThePatchesAreAppliedToTheMethodsTheyAreMeantFor()
    {
        AssertPatched(typeof(Quest), nameof(Quest.PlayerSuccessfullyDeliveredWeapons), nameof(GangLeaderNeedsWeaponsDeliveryPatch));
        AssertPatched(typeof(Quest), nameof(Quest.OnSettlementEnter), nameof(GangLeaderNeedsWeaponsGuardsPatch));
    }

    [Fact]
    public void TheGuardsPatchSkipsVanillasSettlementEntry()
    {
        var prefix = AccessTools.Method(typeof(GangLeaderNeedsWeaponsGuardsPatch), "Prefix");

        Assert.False((bool)prefix.Invoke(null, null)!);
    }

    [Fact]
    public void TheHeroPowerWriterIsInstalledBeforeItsCallersCanInlineIt()
    {
        var category = Assert.Single(typeof(HeroAddPowerPatch).GetCustomAttributes(typeof(HarmonyPatchCategory), false));

        Assert.Equal(global::GameInterface.GameInterface.HARMONY_STATIC_FIXES_CATEGORY, ((HarmonyPatchCategory)category).info.category);
    }

    private static void AssertPatched(Type type, string method, string patchClass)
    {
        var info = Harmony.GetPatchInfo(AccessTools.Method(type, method));

        Assert.NotNull(info);
        Assert.Contains(info.Prefixes, patch => patch.PatchMethod.DeclaringType!.Name == patchClass);
    }

    [Fact]
    public void TheOwnerTakesTheTroopsBackWhenTheMissionEnds()
    {
        StartMissionOnServer();
        var membersBefore = OwnersMembers();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));

            giver.Issue.CompleteIssueWithAlternativeSolution();
        }, LogTexts);

        Assert.Equal(membersBefore + 1 + SentTroops, OwnersMembers());
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>(), message => message.Outcome == IssueOutcome.AlternativeSolution);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.Null(giver.Issue);
        });
    }

    [Fact]
    public void TheTroopsStayAwayWhileTheOwnerCannotTakeThemBackAndReturnTheNextDay()
    {
        StartMissionOnServer();
        var membersBefore = OwnersMembers();

        SetOwnerPrisoner(prisoner: true);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));

            giver.Issue.CompleteIssueWithAlternativeSolution();
        }, LogTexts);

        Assert.Equal(membersBefore, OwnersMembers());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.NotNull(giver.Issue);
            Assert.True(giver.Issue.IsSolvingWithAlternative);
        });

        SetOwnerPrisoner(prisoner: false);

        Server.Call(() => Campaign.Current.IssueManager.DailyTick(), LogTexts);

        Assert.Equal(membersBefore + 1 + SentTroops, OwnersMembers());
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>(), message => message.Outcome == IssueOutcome.AlternativeSolution);
    }

    // The success texts read the companion's encyclopedia link and the success consequence the giver's town, which these test
    // heroes do not have. The troops coming back is what is tested.
    private static readonly System.Reflection.MethodBase[] LogTexts =
    {
        AccessTools.Method(typeof(Issue), "AlternativeSolutionEndWithSuccessConsequence"),
        AccessTools.PropertyGetter(typeof(IssueBase), nameof(Issue.IssueAlternativeSolutionSuccessLog)),
        AccessTools.Method(typeof(IssueBase), nameof(IssueBase.AlternativeSolutionEndWithSuccess)),
    };

    private void StartMissionOnServer()
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var ownerHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));

            using (new AllowedThread())
            {
                ownerHero._partyBelongedTo = party;
                party.MemberRoster.AddToCounts(ownerHero.CharacterObject, 1);

                var issue = new Issue(giver);
                issue._averagePriceForItem = 250;
                issue._requiredWeaponClassIndex = 0;
                issue.StringId = IssueId;
                issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
                issue._failureChance = 0f;
                issue._companionRewardSkill = DefaultSkills.Trade;
                issue.AlternativeSolutionReturnTimeForTroops = CampaignTime.Zero;
                issue.AlternativeSolutionIssueEffectClearTime = CampaignTime.Zero;
                issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                issue.AlternativeSolutionSentTroops.AddToCounts(troop, SentTroops);

                Campaign.Current.IssueManager._issues.Add(giver, issue);
                giver.OnIssueCreatedForHero(issue);
            }

            Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TrySetOwner(giverId, IssueId, ControllerId));
        });
    }

    private int OwnersMembers()
    {
        var members = 0;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var party));
            members = party.MemberRoster.TotalManCount;
        });

        return members;
    }

    private void SetOwnerPrisoner(bool prisoner)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var ownerHero));

            using (new AllowedThread())
            {
                ownerHero._heroState = prisoner ? Hero.CharacterStates.Prisoner : Hero.CharacterStates.Active;
            }
        });
    }

    // The test world has no weapons, the quest only looks at the weapon class
    internal static ItemObject CreateAxe()
    {
        var axe = new ItemObject("test_axe");
        var component = new WeaponComponent(axe);

        component.AddWeapon(new WeaponComponentData(axe, WeaponClass.OneHandedAxe, (WeaponFlags)0), null);
        axe.ItemComponent = component;

        return axe;
    }

    private IQuestTypeDescriptor Descriptor()
    {
        Assert.True(Server.Container.Resolve<IQuestTypeRegistry>().TryGetByName(IssueName, out var descriptor));
        return descriptor;
    }

    private static T Uninitialized<T>() where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
}
