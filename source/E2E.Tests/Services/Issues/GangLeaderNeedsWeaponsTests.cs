using Autofac;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
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
    [InlineData(RequestedWeapons - 1, false)]
    [InlineData(RequestedWeapons, true)]
    public void TheServerOnlyCompletesTheHandInWhenTheOwnerCarriesTheWeapons(int carried, bool completes)
    {
        var axe = CreateAxe();
        var completed = false;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var party));

            using (new AllowedThread())
            {
                party.ItemRoster.Clear();
                party.ItemRoster.AddToCounts(axe, carried);
            }

            var quest = Uninitialized<Quest>();
            quest._requestedWeaponAmount = RequestedWeapons;
            quest._requestedWeaponClass = WeaponClass.OneHandedAxe;

            var previousMainParty = Campaign.Current.MainParty;
            Campaign.Current.MainParty = party;

            try
            {
                completed = (bool)AccessTools.Method("GameInterface.Services.Issues.Quests.GangLeaderNeedsWeapons.Patches.GangLeaderNeedsWeaponsDeliveryPatch:Prefix")
                    .Invoke(null, new object[] { quest })!;
            }
            finally
            {
                Campaign.Current.MainParty = previousMainParty;
            }
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
    public void TheGuardsDoNotAppearWhenThePlayerEntersTheTown()
    {
        var ran = true;

        ClientA.Call(() =>
        {
            ran = (bool)AccessTools.Method("GameInterface.Services.Issues.Quests.GangLeaderNeedsWeapons.Patches.GangLeaderNeedsWeaponsGuardsPatch:Prefix")
                .Invoke(null, Array.Empty<object>())!;
        });

        Assert.False(ran);
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

    [Fact]
    public void AClientThatDidNotStartTheMissionStillTicksItsDay()
    {
        ClientB.Call(() =>
        {
            Assert.True(ClientB.ObjectManager.TryGetObject<Hero>(giverId, out var giver));

            using (new AllowedThread())
            {
                var issue = new Issue(giver);
                issue._averagePriceForItem = 250;
                issue._requiredWeaponClassIndex = 0;
                issue.StringId = IssueId;

                // What the accept does on a client that is not the owner
                issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
                issue.AlternativeSolutionReturnTimeForTroops = CampaignTime.DaysFromNow(3f);
                issue.AlternativeSolutionIssueEffectClearTime = CampaignTime.DaysFromNow(2f);

                Campaign.Current.IssueManager._issues.Add(giver, issue);
                giver.OnIssueCreatedForHero(issue);
            }

            Campaign.Current.IssueManager.DailyTick();

            Assert.True(giver.Issue.IsSolvingWithAlternative);
        });
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
    private static ItemObject CreateAxe()
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
