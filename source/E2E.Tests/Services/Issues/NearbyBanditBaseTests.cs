using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.CreationCapture;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Tests.Utils;
using HarmonyLib;
using Helpers;
using SandBox.GauntletUI.Map;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssueQuest;

namespace E2E.Tests.Services.Issues;

public class NearbyBanditBaseTests : IDisposable
{
    private const int RewardGold = 3000;
    private const float PowerChange = 5f;
    private const float ProsperityChange = 10f;
    private const float CoercionPowerChange = 10f;
    private const int SentTroops = 40;

    private static readonly MethodBase[] QuestLogTexts =
    {
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.OnQuestSucceededLogText)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.OnQuestFailedLogText)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.OnQuestCanceledLogText)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.OnQuestFailedFromWarCausedByPlayerLogText)),
        AccessTools.Method(typeof(QuestBase), nameof(QuestBase.AddLog)),
    };

    private static readonly MethodBase[] QuestAcceptedMethods =
    {
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.OnQuestStartedLogText)),
        AccessTools.Method(typeof(StringHelpers), nameof(StringHelpers.SetCharacterProperties)),
        AccessTools.Method(typeof(QuestHelper), nameof(QuestHelper.AddMapArrowFromPointToTarget)),
        AccessTools.Method(typeof(MBInformationManager), nameof(MBInformationManager.AddQuickInformation)),
        AccessTools.Method(typeof(QuestBase), nameof(QuestBase.AddLog)),
    };

    private static readonly MethodBase[] MapEventMethods =
    {
        AccessTools.Method(typeof(GauntletMapEventVisual), nameof(GauntletMapEventVisual.Initialize)),
    };

    private readonly StubIssuePreconditions preconditions = new(typeof(Issue));

    private readonly string headmanId;
    private readonly string villageSettlementId;
    private readonly string townSettlementId;
    private readonly string hideoutSettlementId;
    private readonly string ownerHeroId;
    private readonly string ownerPartyId;
    private readonly string otherHeroId;
    private readonly string otherPartyId;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance ClientA => TestEnvironment.Clients.First();
    private EnvironmentInstance ClientB => TestEnvironment.Clients.Last();
    private EnvironmentInstance[] Instances => new[] { Server, ClientA, ClientB };

    public NearbyBanditBaseTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        headmanId = TestEnvironment.CreateRegisteredObject<Hero>();
        villageSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var villageId = TestEnvironment.CreateRegisteredObject<Village>();
        townSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        hideoutSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var hideoutId = TestEnvironment.CreateRegisteredObject<Hideout>();

        (ownerHeroId, ownerPartyId) = RegisterPlayer(ClientA, "player-A");
        (otherHeroId, otherPartyId) = RegisterPlayer(ClientB, "player-B");

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(headmanId, out var headman));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(villageSettlementId, out var villageSettlement));
                Assert.True(instance.ObjectManager.TryGetObject<Village>(villageId, out var village));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(townSettlementId, out var townSettlement));
                Assert.True(instance.ObjectManager.TryGetObject<Town>(townId, out var town));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(hideoutSettlementId, out var hideoutSettlement));
                Assert.True(instance.ObjectManager.TryGetObject<Hideout>(hideoutId, out var hideout));

                using (new AllowedThread())
                {
                    townSettlement.SetSettlementComponent(town);
                    villageSettlement.Village = village;
                    villageSettlement.SetSettlementComponent(village);
                    village.Bound = townSettlement;
                    hideoutSettlement.SetSettlementComponent(hideout);
                    hideoutSettlement.IsVisible = false;
                    headman.StayingInSettlement = villageSettlement;
                    headman.Occupation = Occupation.Headman;
                }
            });
        }
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
        preconditions.Dispose();
    }

    [Fact]
    public void EveryClientBuildsTheIssueWithTheServersHideout()
    {
        CreateIssueOnServer();

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueCreated>());

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(headmanId, out var headman));
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(hideoutSettlementId, out var hideout));
                var issue = Assert.IsType<Issue>(headman.Issue);

                Assert.Same(hideout, issue._targetHideout);
            });
        }
    }

    [Fact]
    public void AcceptingShowsTheHideoutOnTheServerAndEveryClient()
    {
        CreateIssueOnServer();
        AcceptOnClientA();

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(hideoutSettlementId, out var hideout));
                Assert.True(hideout.IsVisible);
            });
        }
    }

    [Fact]
    public void AHideoutBattleTheOwnerWinsPaysTheOwnerOnceAndEndsTheQuestEverywhere()
    {
        CreateIssueOnServer();
        AcceptOnClientA();

        var goldBefore = GoldOfOwner(Server);

        EndHideoutBattleOnServer(ownerPartyId, BattleState.AttackerVictory);
        TestEnvironment.FlushCoalescer();

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestSuccess, finalized.Outcome);
        Assert.Equal(goldBefore + RewardGold, GoldOfOwner(Server));
        Assert.Equal(goldBefore + RewardGold, GoldOfOwner(ClientA));
        AssertIssueEndedEverywhere();
    }

    [Fact]
    public void AHideoutClearedWithoutTheOwnerFailsTheOwnersQuest()
    {
        CreateIssueOnServer();
        AcceptOnClientA();

        var otherPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var powerBefore = PowerOfHeadman(Server);

        EndHideoutBattleOnServer(otherPartyId, BattleState.AttackerVictory);

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestFail, finalized.Outcome);
        Assert.Equal(powerBefore, PowerOfHeadman(Server));
        Assert.Equal(powerBefore, PowerOfHeadman(ClientA));
        AssertIssueEndedEverywhere();
    }

    [Fact]
    public void ATimeOutChangesTheHeadmansPowerOnlyOnce()
    {
        CreateIssueOnServer();
        AcceptOnClientA();

        var powerBefore = PowerOfHeadman(Server);

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(headmanId, out var headman));
            var quest = Assert.IsType<Quest>(headman.Issue.IssueQuest);

            quest.CompleteQuestWithTimeOut();
        }, QuestLogTexts);

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestTimeOut, finalized.Outcome);
        Assert.Equal(powerBefore - PowerChange, PowerOfHeadman(Server));
        Assert.Equal(powerBefore - PowerChange, PowerOfHeadman(ClientA));
    }

    [Fact]
    public void TheOwnersAcceptStartsTheQuestWithoutRevealingTheHideoutOnTheClient()
    {
        CreateIssueOnServer();

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(headmanId, out var headman));
            Assert.True(ClientA.ObjectManager.TryGetObject<Settlement>(hideoutSettlementId, out var hideout));

            headman.Issue.StartIssueWithQuest();
            var quest = Assert.IsType<Quest>(headman.Issue.IssueQuest);

            quest.OnQuestAccepted();

            Assert.Contains(quest, Campaign.Current.QuestManager.Quests);
            Assert.False(hideout.IsVisible);
        }, QuestAcceptedMethods);
    }

    [Fact]
    public void AHideoutBattleTheOwnerLosesFailsTheQuestEverywhere()
    {
        CreateIssueOnServer();
        AcceptOnClientA();

        var powerBefore = PowerOfHeadman(Server);

        EndHideoutBattleOnServer(ownerPartyId, BattleState.DefenderVictory);

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestFail, finalized.Outcome);
        Assert.Equal(powerBefore - PowerChange, PowerOfHeadman(Server));
        AssertIssueEndedEverywhere();
    }

    [Fact]
    public void AnEmptiedHideoutCancelsTheQuestEverywhere()
    {
        CreateIssueOnServer();
        AcceptOnClientA();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(hideoutSettlementId, out var hideout));

            CampaignEventDispatcher.Instance.OnHideoutDeactivated(hideout);
        }, QuestLogTexts);

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestCancel, finalized.Outcome);
        AssertIssueEndedEverywhere();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AWarFailsTheQuestOnlyWhenTheOwnersFactionCausesIt(bool ownersFaction)
    {
        var ownerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var otherClanId = TestEnvironment.CreateRegisteredObject<Clan>();

        CreateIssueOnServer();
        AcceptOnClientA();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(ownerClanId, out var ownerClan));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(otherClanId, out var otherClan));

            using (new AllowedThread())
            {
                owner.Clan = ownerClan;
            }

            var declarer = ownersFaction ? ownerClan : otherClan;
            var target = ownersFaction ? otherClan : ownerClan;
            DeclareWarAction.ApplyInternal(declarer, target, DeclareWarAction.DeclareWarDetail.CausedByKingdomCreation);
        }, QuestLogTexts);

        if (!ownersFaction)
        {
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
            return;
        }

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestFail, finalized.Outcome);
        AssertIssueEndedEverywhere();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AHostilityWarFailsTheQuestOnlyWhenTheOwnerStartedIt(bool ownerStarted)
    {
        var ownerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var otherClanId = TestEnvironment.CreateRegisteredObject<Clan>();

        CreateIssueOnServer();
        AcceptOnClientA();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(otherHeroId, out var otherPlayer));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(ownerClanId, out var ownerClan));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(otherClanId, out var otherClan));

            using (new AllowedThread())
            {
                owner.Clan = ownerClan;
            }

            var previous = ResolvedMainHeroContext.ResolvedMainHero;
            try
            {
                ResolvedMainHeroContext.ResolvedMainHero = ownerStarted ? owner : otherPlayer;
                CampaignEventDispatcher.Instance.OnWarDeclared(otherClan, ownerClan, DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility);
            }
            finally
            {
                ResolvedMainHeroContext.ResolvedMainHero = previous;
            }
        }, QuestLogTexts);

        if (!ownerStarted)
        {
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
            return;
        }

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestFail, finalized.Outcome);
        AssertIssueEndedEverywhere();
    }

    [Fact]
    public void AnOwnerWithoutAPartyIsNotPaidWhenAnotherPlayerClearsTheHideout()
    {
        CreateIssueOnServer();
        AcceptOnClientA();

        var goldBefore = GoldOfOwner(Server);
        var powerBefore = PowerOfHeadman(Server);
        RemoveTheOwnersParty();

        EndHideoutBattleOnServer(otherPartyId, BattleState.AttackerVictory, otherHeroId);

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestFail, finalized.Outcome);
        Assert.Equal(goldBefore, GoldOfOwner(Server));
        Assert.Equal(powerBefore, PowerOfHeadman(Server));
        AssertIssueEndedEverywhere();
    }

    [Fact]
    public void AnOwnerWithoutAPartyKeepsTheQuestWhenAnotherPlayerLosesTheHideoutBattle()
    {
        CreateIssueOnServer();
        AcceptOnClientA();

        var powerBefore = PowerOfHeadman(Server);
        RemoveTheOwnersParty();

        EndHideoutBattleOnServer(otherPartyId, BattleState.DefenderVictory, otherHeroId);

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(powerBefore, PowerOfHeadman(Server));
    }

    [Fact]
    public void AnOwnerCapturedLosingTheHideoutBattleFailsTheQuest()
    {
        CreateIssueOnServer();
        AcceptOnClientA();

        var powerBefore = PowerOfHeadman(Server);
        RemoveTheOwnersParty();

        EndHideoutBattleOnServer(ownerPartyId, BattleState.DefenderVictory, ownerCapturedByTheHideout: true);

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestFail, finalized.Outcome);
        Assert.Equal(powerBefore - PowerChange, PowerOfHeadman(Server));
        Assert.Equal(powerBefore - PowerChange, PowerOfHeadman(ClientA));
        AssertIssueEndedEverywhere();
    }

    [Fact]
    public void CoercingTheHeadmansVillageFailsTheQuestAndCostsTheHeadmanPowerOnce()
    {
        var townOwnerClanId = TestEnvironment.CreateRegisteredObject<Clan>();

        CreateIssueOnServer();
        AcceptOnClientA();

        var powerBefore = PowerOfHeadman(Server);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(villageSettlementId, out var village));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(townOwnerClanId, out var townOwnerClan));

            using (new AllowedThread())
            {
                village.Village.Bound.Town.OwnerClan = townOwnerClan;
                village.CollectNotablesToCache();
            }

            Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
            Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();

            var forcing = (MapEvent)FormatterServices.GetUninitializedObject(typeof(MapEvent));
            forcing.SetBattleType(MapEvent.BattleTypes.IsForcingSupplies);
            forcing.SetMapEventSettlement(village);
            CampaignEventDispatcher.Instance.OnMapEventStarted(forcing, ownerParty.Party, null);
        }, QuestLogTexts);

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestFail, finalized.Outcome);
        Assert.Equal(powerBefore - CoercionPowerChange, PowerOfHeadman(Server));
        Assert.Equal(powerBefore - CoercionPowerChange, PowerOfHeadman(ClientA));
        AssertIssueEndedEverywhere();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OnlyAPlayerHeldInTheHideoutKeepsAiLordsAway(bool player)
    {
        var prisonerId = player ? ownerHeroId : TestEnvironment.CreateRegisteredObject<Hero>();

        CreateIssueOnServer();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(headmanId, out var headman));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(prisonerId, out var prisoner));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(hideoutSettlementId, out var hideout));
            var issue = Assert.IsType<Issue>(headman.Issue);

            Assert.True(issue.CanBeCompletedByAI());

            using (new AllowedThread())
            {
                prisoner.CharacterObject.HeroObject = prisoner;
                hideout.Party.PrisonRoster.AddToCounts(prisoner.CharacterObject, 1);
            }

            Assert.Equal(!player, issue.CanBeCompletedByAI());
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ACompanionSentByTheOwnerAppliesTheOutcomeOnceOnTheServerAndTheOwner(bool success)
    {
        CreateIssueOnServer();
        SendCompanionFromClientA();

        var powerBefore = PowerOfHeadman(Server);
        var prosperityBefore = ProsperityOfTown(Server);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(headmanId, out var headman));
            var issue = Assert.IsType<Issue>(headman.Issue);
            Assert.True(issue.IsSolvingWithAlternative);

            issue._failureChance = success ? -1 : 1;
            issue.CompleteIssueWithAlternativeSolution();
        });

        var change = success ? 1 : -1;
        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.AlternativeSolution, finalized.Outcome);
        Assert.Equal(powerBefore + (PowerChange * change), PowerOfHeadman(Server));
        Assert.Equal(powerBefore + (PowerChange * change), PowerOfHeadman(ClientA));
        Assert.Equal(prosperityBefore + (ProsperityChange * change), ProsperityOfTown(Server));
        Assert.Equal(prosperityBefore + (ProsperityChange * change), ProsperityOfTown(ClientA));
        AssertIssueEndedEverywhere();
    }

    private void SendCompanionFromClientA()
    {
        var companionId = TestEnvironment.CreateRegisteredObject<Hero>();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var owner));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(townSettlementId, out var town));

                using (new AllowedThread())
                {
                    town.Town.Prosperity = 500f;
                    owner._partyBelongedTo = party;
                    owner.CharacterObject.HeroObject = owner;
                    companion.Clan = owner.Clan;
                    companion.CharacterObject.HeroObject = companion;
                    troop.Level = 15;
                    troop.UpgradeTargets = Array.Empty<CharacterObject>();
                }

                Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
            });
        }

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(headmanId, out var headman));
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var owner));
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(ClientA.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            var issue = Assert.IsType<Issue>(headman.Issue);

            Game.Current.PlayerTroop = owner.CharacterObject;
            using (new AllowedThread())
            {
                issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                issue.AlternativeSolutionSentTroops.AddToCounts(troop, SentTroops);
            }

            Assert.True(issue.DoTroopsSatisfyAlternativeSolution(issue.AlternativeSolutionSentTroops, out var explanation), explanation?.ToString());

            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var serverTroop));
                Assert.True(Server.ObjectManager.TryGetHandle(serverTroop, out var handle));
                var delta = new TroopRosterData(new[] { new TroopRosterElementData(handle, SentTroops, 0, 0) });
                Server.Resolve<IMessageBroker>().Publish(this, new PartyDoneLogicApplied(ClientA.NetPeer,
                    PartyScreenHelper.PartyScreenMode.QuestTroopManage, delta, ownerHeroId, headmanId, issue.StringId));
            });

            issue.StartIssueWithAlternativeSolution();
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAccepted>());
    }

    private float ProsperityOfTown(EnvironmentInstance instance)
    {
        var prosperity = 0f;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Settlement>(townSettlementId, out var town));
            prosperity = town.Town.Prosperity;
        });

        return prosperity;
    }

    private (string HeroId, string PartyId) RegisterPlayer(EnvironmentInstance client, string controllerId)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            using (new AllowedThread())
            {
                hero._partyBelongedTo = party;
                hero.CharacterObject.HeroObject = hero;
            }

            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player(controllerId, heroId, partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(client, controllerId);
        client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);

        return (heroId, partyId);
    }

    private void CreateIssueOnServer()
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(headmanId, out var headman));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(hideoutSettlementId, out var hideout));

            var potentialIssue = new PotentialIssueData(
                (in PotentialIssueData _, Hero hero) => new Issue(hero, hideout),
                typeof(Issue),
                IssueBase.IssueFrequency.VeryCommon);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potentialIssue, headman));
        });
    }

    private void AcceptOnClientA()
    {
        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(headmanId, out var headman));

            headman.Issue.StartIssueWithQuest();

            using (new AllowedThread())
            {
                headman.Issue.IssueQuest.StartQuest();
            }
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(headmanId, out var headman));
            Assert.IsType<Quest>(headman.Issue.IssueQuest);
        });
    }

    private void EndHideoutBattleOnServer(string attackerPartyId, BattleState battleState, string? finalizingHeroId = null, bool ownerCapturedByTheHideout = false)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(attackerPartyId, out var attacker));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(hideoutSettlementId, out var hideout));

            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            mapEvent.MapEventVisual = ObjectHelper.SkipConstructor<GauntletMapEventVisual>();
            mapEvent.Initialize(attacker.Party, hideout.Party, new HideoutEventComponent(mapEvent, false));
            mapEvent.MapEventVisual = null;
            mapEvent._battleState = battleState;

            if (ownerCapturedByTheHideout)
            {
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var owner));

                using (new AllowedThread())
                {
                    owner._heroState = Hero.CharacterStates.Prisoner;
                    owner.PartyBelongedToAsPrisoner = hideout.Party;
                    owner.CaptivityStartTime = mapEvent.BattleStartTime;
                }
            }

            if (finalizingHeroId == null)
            {
                CampaignEventDispatcher.Instance.OnMapEventEnded(mapEvent);
                return;
            }

            Assert.True(Server.ObjectManager.TryGetObject<Hero>(finalizingHeroId, out var finalizingHero));

            using (new MainHeroSubstitutionScope(finalizingHero, attacker))
            {
                CampaignEventDispatcher.Instance.OnMapEventEnded(mapEvent);
            }
        }, MapEventMethods.Concat(QuestLogTexts));
    }

    private void RemoveTheOwnersParty()
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var owner));

            using (new AllowedThread())
            {
                owner._partyBelongedTo = null;
            }
        });
    }

    private void AssertIssueEndedEverywhere()
    {
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(headmanId, out var headman));
                Assert.Null(headman.Issue);
            });
        }
    }

    private int GoldOfOwner(EnvironmentInstance instance)
    {
        var gold = 0;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var owner));
            gold = owner.Gold;
        });

        return gold;
    }

    private float PowerOfHeadman(EnvironmentInstance instance)
    {
        var power = 0f;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(headmanId, out var headman));
            power = headman.Power;
        });

        return power;
    }
}
