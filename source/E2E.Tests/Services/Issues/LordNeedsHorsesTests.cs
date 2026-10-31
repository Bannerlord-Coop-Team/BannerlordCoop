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
using Issue = TaleWorlds.CampaignSystem.Issues.LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssueQuest;

namespace E2E.Tests.Services.Issues;

public class LordNeedsHorsesTests : IDisposable
{
    private const string IssueName = "LordNeedsHorsesIssue";
    private const string IssueId = "issue_lord_needs_horses";
    private const string ControllerId = "player-A";

    private readonly string giverId;
    private readonly string ownerHeroId;
    private readonly string ownerPartyId;
    private readonly string companionId;
    private readonly string troopId;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance ClientA => TestEnvironment.Clients.First();
    private EnvironmentInstance ClientB => TestEnvironment.Clients.Last();

    public LordNeedsHorsesTests(ITestOutputHelper output)
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
        Assert.Null(descriptor.FinalizationProofStrategy);
    }

    [Fact]
    public void CreationCapture_ForcesTheServersMountAndAmountOntoTheClientsIssue()
    {
        var mount = DefaultItems.Grain;
        byte[] captured = null!;

        Server.Call(() =>
        {
            var serverIssue = Uninitialized<Issue>();
            AccessTools.Field(typeof(Issue), nameof(Issue._mountObjectToBeDelivered)).SetValue(serverIssue, mount);
            serverIssue._numMountsToBeDelivered = 9;
            AccessTools.Field(typeof(Issue), nameof(Issue._mountValuePerUnit)).SetValue(serverIssue, 123);

            captured = Descriptor().CreationCaptureStrategy.Capture(serverIssue);
        });

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(giverId, out var giver));

            var clientIssue = (Issue)Descriptor().CreationCaptureStrategy.CreateIssue(giver, captured);

            Assert.Equal(mount.StringId, clientIssue._mountObjectToBeDelivered.StringId);
            Assert.Equal(9, clientIssue._numMountsToBeDelivered);
            Assert.Equal(123, clientIssue._mountValuePerUnit);
        });
    }

    [Fact]
    public void DebugCapture_IsRefusedForAGiverWithoutAParty()
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));

            Assert.False(Descriptor().CreationCaptureStrategy.TryBuildDebugCapture(giver, out _));
        });
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
    public void DebugCapture_IsRefusedForAGiverWhoIsNotAClanLeader()
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var party));

            using (new AllowedThread())
            {
                giver._partyBelongedTo = party;
            }

            Assert.False(Descriptor().CreationCaptureStrategy.TryBuildDebugCapture(giver, out _));
        });
    }

    [Fact]
    public void DebugCapture_IsBuiltForAClanLeaderWithFewMounts()
    {
        var clanId = TestEnvironment.CreateRegisteredObject<Clan>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));

            using (new AllowedThread())
            {
                giver.Clan = clan;
                clan.SetLeader(giver);
                giver._partyBelongedTo = party;
                party.MemberRoster.AddToCounts(troop, 20);
            }

            Assert.True(Descriptor().CreationCaptureStrategy.TryBuildDebugCapture(giver, out var captured));
            Assert.NotEmpty(captured);
        });
    }

    private const int SentTroops = 3;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [InlineData(true, true)]
    public void PendingPickerTroopsReturnWhenCancelledOrTheIssueEnds(bool finalize, bool delayedDone = false)
    {
        foreach (var instance in new[] { Server, ClientA, ClientB })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
                using (new AllowedThread())
                {
                    hero._partyBelongedTo = party;
                    troop.UpgradeTargets = Array.Empty<CharacterObject>();
                    var issue = new Issue(giver) { StringId = IssueId };
                    Campaign.Current.IssueManager._issues.Add(giver, issue);
                    giver.OnIssueCreatedForHero(issue);
                }
            });
        }
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            party.MemberRoster.AddToCounts(troop, 10);
        });
        TestEnvironment.FlushCoalescer();
        var membersBefore = OwnersMembers();
        if (delayedDone)
            ClientA.Resolve<E2E.Tests.Environment.TestNetworkRouter>().PauseLink(ClientA.NetPeer, Server.NetPeer);
        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(ClientA.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var party));
            Assert.True(ClientA.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var owner));
            using var ownerScope = new GameInterface.Services.Heroes.Patches.MainHeroSubstitutionScope(owner, party);
            var agent = new Moq.Mock<TaleWorlds.Core.IAgent>();
            agent.SetupGet(value => value.Character).Returns(giver.CharacterObject);
            Campaign.Current.ConversationManager._conversationAgents.Add(agent.Object);
            new TaleWorlds.CampaignSystem.CampaignBehaviors.IssuesCampaignBehavior()
                .issue_offer_player_accept_alternative_4_consequence();
            Campaign.Current.EncyclopediaManager ??= new TaleWorlds.CampaignSystem.Encyclopedia.EncyclopediaManager();
            Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
            var logic = new PartyScreenLogic();
            logic.Initialize(PartyScreenLogicInitializationData.CreateBasicInitDataWithMainParty(
                giver.Issue.AlternativeSolutionSentTroops,
                TaleWorlds.CampaignSystem.Roster.TroopRoster.CreateDummyTroopRoster(),
                PartyScreenLogic.TransferState.Transferable, PartyScreenLogic.TransferState.NotTransferable,
                PartyScreenLogic.TransferState.Transferable, null,
                Helpers.PartyScreenHelper.PartyScreenMode.QuestTroopManage,
                partyPresentationDoneButtonDelegate: Helpers.PartyScreenHelper.ManageTroopsAndPrisonersDoneHandler));
            logic._partyScreenMode = Helpers.PartyScreenHelper.PartyScreenMode.QuestTroopManage;
            var command = new PartyScreenLogic.PartyCommand();
            command.FillForTransferTroop(PartyScreenLogic.PartyRosterSide.Right,
                PartyScreenLogic.TroopType.Member, troop, SentTroops, 0, -1);
            using (new AllowedThread()) logic.TransferTroop(command, false);
            Assert.True(logic.DoneLogic(false));
        }, new[] { AccessTools.Method(typeof(Helpers.PartyScreenHelper), nameof(Helpers.PartyScreenHelper.OpenScreenAsQuest)) });
        TestEnvironment.FlushCoalescer();
        Assert.Equal(membersBefore - (delayedDone ? 0 : SentTroops), OwnersMembers());
        if (finalize)
        {
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                giver.Issue.CompleteIssueWithTimedOut();
            }, LogTexts);
        }
        else
        {
            ClientA.Call(() => TaleWorlds.CampaignSystem.CampaignBehaviors.IssuesCampaignBehavior
                .issue_offer_player_accept_alternative_5_b_consequence());
        }
        TestEnvironment.FlushCoalescer();
        if (delayedDone)
        {
            var router = ClientA.Resolve<E2E.Tests.Environment.TestNetworkRouter>();
            router.ResumeLink(ClientA.NetPeer, Server.NetPeer);
            router.DrainReady();
            TestEnvironment.FlushCoalescer();
        }
        Assert.Equal(membersBefore, OwnersMembers());
        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var party));
            Assert.True(ClientA.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            Assert.Equal(10, party.MemberRoster.GetTroopCount(troop));
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ClosingAnAlternativePickerRequestsOnlyOneStatePop(bool nativeCancel)
    {
        ClientA.Call(() =>
        {
            var giver = ClientA.GetRegisteredObject<Hero>(giverId);
            var owner = ClientA.GetRegisteredObject<Hero>(ownerHeroId);
            var party = ClientA.GetRegisteredObject<MobileParty>(ownerPartyId);
            using var ownerScope = new GameInterface.Services.Heroes.Patches.MainHeroSubstitutionScope(owner, party);
            Issue issue;
            using (new AllowedThread())
            {
                owner._partyBelongedTo = party;
                issue = new Issue(giver) { StringId = IssueId };
                Campaign.Current.IssueManager._issues.Add(giver, issue);
                giver.OnIssueCreatedForHero(issue);
            }
            var agent = new Moq.Mock<IAgent>();
            agent.SetupGet(value => value.Character).Returns(giver.CharacterObject);
            Campaign.Current.ConversationManager._conversationAgents.Add(agent.Object);
            var picker = ClientA.Resolve<IAlternativeSolutionPicker>();
            picker.Open(issue);
            var callbacks = 0;
            Campaign.Current.EncyclopediaManager ??= new TaleWorlds.CampaignSystem.Encyclopedia.EncyclopediaManager();
            Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
            var logic = new PartyScreenLogic();
            var initializationData = PartyScreenLogicInitializationData.CreateBasicInitDataWithMainParty(
                issue.AlternativeSolutionSentTroops,
                TaleWorlds.CampaignSystem.Roster.TroopRoster.CreateDummyTroopRoster(),
                PartyScreenLogic.TransferState.Transferable, PartyScreenLogic.TransferState.NotTransferable,
                PartyScreenLogic.TransferState.Transferable, null,
                Helpers.PartyScreenHelper.PartyScreenMode.QuestTroopManage);
            initializationData.PartyScreenClosedDelegate = (_, _, _, _, _, _, fromCancel) =>
                {
                    Assert.True(fromCancel);
                    callbacks++;
                    TaleWorlds.CampaignSystem.CampaignBehaviors.IssuesCampaignBehavior
                        .issue_offer_player_accept_alternative_5_b_consequence();
                };
            logic.Initialize(initializationData);
            var manager = Game.Current.GameStateManager;
            manager._gameStates.Add(manager.CreateState<TaleWorlds.CampaignSystem.GameState.MapState>());
            var state = manager.CreateState<TaleWorlds.CampaignSystem.GameState.PartyState>();
            state.PartyScreenLogic = logic;
            manager._gameStates.Add(state);
            var previousManager = GameStateManager.Current;
            GameStateManager._current = manager;
            try
            {
                // Record the engine-facing pop, leaving native close, callback and refund logic running.
                using var pops = new E2E.Tests.Util.MethodCallRecorder(
                    AccessTools.Method(typeof(GameStateManager), nameof(GameStateManager.PopState)));
                if (nativeCancel)
                {
                    Assert.Same(state, Helpers.PartyScreenHelper.GetActivePartyState());
                    Helpers.PartyScreenHelper.CloseScreen(false, fromCancel: true);
                    Assert.Equal(1, callbacks);
                    Assert.Null(state.PartyScreenLogic);
                }
                else
                {
                    picker.Close(issue, cancel: true);
                    Assert.Equal(0, callbacks);
                }
                Assert.False(picker.TryGetIssue(issue.AlternativeSolutionSentTroops, out _, out _));
                Assert.Equal(1, pops.CountFor(ClientA));
            }
            finally
            {
                GameStateManager._current = previousManager;
            }
        });
        Assert.Single(ClientA.NetworkSentMessages.GetMessages<GameInterface.Services.Issues.Framework.AcceptCoordination.RequestAlternativeSolutionCancellation>());
    }

    // The success texts read the companion's encyclopedia link, which these test heroes do not have. The troops coming back is what is tested.
    private static readonly System.Reflection.MethodBase[] LogTexts =
    {
        AccessTools.PropertyGetter(typeof(Issue), nameof(Issue.IssueAlternativeSolutionSuccessLog)),
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

    private IQuestTypeDescriptor Descriptor()
    {
        Assert.True(Server.Container.Resolve<IQuestTypeRegistry>().TryGetByName(IssueName, out var descriptor));
        return descriptor;
    }

    private static T Uninitialized<T>() where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
}
