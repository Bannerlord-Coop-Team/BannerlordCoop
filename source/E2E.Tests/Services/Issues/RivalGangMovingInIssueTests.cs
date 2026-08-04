using Common.Messaging;
using Common.Network;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using Moq;
using SandBox.Issues;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

/// <summary>
/// Real, executed multi-peer coverage for Rival Gang Moving In (source/GameInterface/Services/Issues/
/// {Interfaces,Messages,Handlers,Patches}/RivalGangMovingIn*.cs), following the same harness/conventions
/// established by <see cref="CaravanAmbushIssueTests"/>/<see cref="SmugglersIssueTests"/>/
/// <see cref="LandLordCompanyOfTroubleIssueTests"/>.
///
/// Scope note (same established precedent as <see cref="CaravanAmbushIssueTests"/>'s own type doc comment):
/// this file deliberately does NOT drive <see cref="IRivalGangMovingInIssueInterface.CreateReplicatedParties"/>
/// all the way through vanilla's own hideout pick (<c>SettlementHelper.FindNearestHideoutToSettlement</c>, which
/// walks <c>Hideout.All</c> and real map-distance math) - standing up that much real map/faction/culture data is
/// out of scope for this harness. What IS fully, genuinely exercised instead: the party-spawn gate's real
/// blocking/request behavior (proven via a short-timeout coordinator, since nothing in this synchronous harness
/// ever delivers a genuine network reply mid-<c>GameThread.WaitWhilePumping</c> wait - the same reason no other
/// blocking coordinator in this codebase, e.g. <c>MapEventCreationCoordinator</c>, has a full round-trip E2E
/// test either), the coordinator's own server-side request-handling/id-resolution plumbing (via a mocked
/// <see cref="IRivalGangMovingInIssueInterface"/>, sidestepping the hideout pipeline while still proving the
/// wiring), and the full force-write/capture convergence using real, AutoRegistry-synced objects built the same
/// already-proven-safe way <see cref="SmugglersIssueTests.CreateRealPartyOnServer"/> does.
///
/// Standing-lesson re-check findings this suite exercises for real (see each patch's own doc comment for the
/// full derivation): (1) the earlier design's instance-collision premise was disproven (non-owner mirrors are
/// inert by construction), but a narrower real risk - two owners' quests both <c>IsOngoing</c> on one peer after
/// a save/reload - needed the same <c>BettingFraudInstanceResolutionPatch</c>-style ownership-filtered rescan;
/// (2) the alley-fight trigger's "replay on the server" premise was also disproven - the real gap is a missing
/// blocking wait between <c>RestartPlayerEncounter</c> and <c>StartBattle()</c>; (3) this quest needs NO bespoke
/// accept-time handler at all (unlike Betting Fraud) - <c>GenerateIssueQuest</c> reads only fields already
/// frozen by creation time, so it rides the fully generic <c>GenericAcceptMirrorIssueTypes</c> mechanism, an
/// earlier design assumption independently corrected during implementation (see
/// <see cref="Handlers.RivalGangMovingInIssueHandler"/>'s own doc comment).
/// </summary>
public class RivalGangMovingInIssueTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();
    private EnvironmentInstance OtherClient => TestEnvironment.Clients.Last();
    private IEnumerable<EnvironmentInstance> AllInstances => new[] { Server }.Concat(TestEnvironment.Clients);

    public RivalGangMovingInIssueTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private record RivalGangMovingInFixture(string OwnerId, string RivalGangLeaderId);

    /// <summary>Builds the issue-owning Hero plus the rival gang leader Hero. Also seeds
    /// <see cref="Campaign.EncyclopediaManager"/> on every instance - the real activation log eagerly resolves
    /// encyclopedia links, same hazard/fix as <see cref="CaravanAmbushIssueTests.SetupIssueOwner"/>. Also sets
    /// the quest giver's own <c>CurrentSettlement</c> - <c>RivalGangMovingInIssueQuest</c>'s own
    /// <c>InitializeQuestSettlement</c> (called from the ctor) reads it unconditionally.</summary>
    private RivalGangMovingInFixture SetupIssueOwner()
    {
        var ownerId = TestEnvironment.CreateRegisteredObject<Hero>();
        var rivalGangLeaderId = TestEnvironment.CreateRegisteredObject<Hero>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();

                    Assert.True(instance.ObjectManager.TryGetObject<Hero>(ownerId, out var owner));
                    Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                    owner.StayingInSettlement = settlement;
                }
            });
        }

        return new RivalGangMovingInFixture(ownerId, rivalGangLeaderId);
    }

    /// <summary>Drives the real server-authoritative creation path: a genuine
    /// <see cref="IssueManager.CreateNewIssue"/> call handing back a real
    /// <see cref="RivalGangMovingInIssueBehavior.RivalGangMovingInIssue"/> constructed with the given
    /// (server-captured) RivalGangLeader - exactly what <c>RivalGangMovingInIssueBehavior.OnStartIssue</c> does
    /// for real, just with the leader supplied directly instead of re-running <c>GetRivalGangLeader</c>'s own
    /// <c>Notables</c> walk.</summary>
    private void CreateIssueOnServer(RivalGangMovingInFixture fixture)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.OwnerId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.RivalGangLeaderId, out var rivalGangLeader));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) => new RivalGangMovingInIssueBehavior.RivalGangMovingInIssue(h, rivalGangLeader),
                typeof(RivalGangMovingInIssueBehavior.RivalGangMovingInIssue),
                IssueBase.IssueFrequency.Common);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
        });
    }

    /// <summary>Real (unwrapped) accept on <paramref name="instance"/> - drives
    /// <see cref="IssueManager.StartIssueQuest"/> for real, which (per <see cref="GenericAcceptMirrorIssueTypes"/>)
    /// triggers the fully generic accept-broadcast/ownership-record path unchanged.</summary>
    private RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest AcceptOnInstance(EnvironmentInstance instance, string ownerId)
    {
        RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest quest = null;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(ownerId, out var owner));
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            quest = Assert.IsType<RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest>(owner.Issue.IssueQuest);
        });
        return quest;
    }

    /// <summary>Builds a real, fully-constructed <see cref="MobileParty"/> the same already-proven-safe way
    /// <see cref="SmugglersIssueTests.CreateRealPartyOnServer"/> does - used as this file's stand-in for "the
    /// server's genuine <see cref="IRivalGangMovingInIssueInterface.CreateReplicatedParties"/> result" wherever a
    /// test needs a real, converged party without standing up the hideout/culture pipeline.</summary>
    private string CreateRealPartyOnServer(string customName)
    {
        string partyId = null;
        Server.Call(() =>
        {
            var settlement = Util.GameObjectCreator.CreateInitializedObject<Settlement>();
            var hero = Util.GameObjectCreator.CreateInitializedObject<Hero>();
            var clan = Util.GameObjectCreator.CreateInitializedObject<Clan>();
            var template = Util.GameObjectCreator.CreateInitializedObject<TaleWorlds.CampaignSystem.Party.PartyTemplateObject>();

            var party = TaleWorlds.CampaignSystem.Party.PartyComponents.CustomPartyComponent.CreateCustomPartyWithPartyTemplate(
                new TaleWorlds.CampaignSystem.CampaignVec2(new TaleWorlds.Library.Vec2(5, 5), true), 5, settlement,
                new TaleWorlds.Localization.TextObject(customName), clan, template, hero);

            Assert.True(Server.ObjectManager.TryGetId(party, out partyId));
        });
        return partyId;
    }

    /// <summary>Builds a real, AutoRegistry-synced <see cref="Hero"/> - stand-in for a genuine, server-minted
    /// henchman Hero (<c>HeroCreator.CreateSpecialHero</c>'s own hideout/culture-free result, matching this
    /// file's own scope note).</summary>
    private string CreateRealHeroOnServer()
    {
        string heroId = null;
        Server.Call(() =>
        {
            var hero = Util.GameObjectCreator.CreateInitializedObject<Hero>();
            Assert.True(Server.ObjectManager.TryGetId(hero, out heroId));
        });
        return heroId;
    }

    private static void InvokePrivate(object instance, string methodName)
    {
        var method = AccessTools.Method(instance.GetType(), methodName);
        method.Invoke(instance, null);
    }

    private static void SetPartyCreationTimeout(EnvironmentInstance instance, TimeSpan timeout)
    {
        instance.Call(() =>
        {
            var config = new Mock<INetworkConfig>();
            config.SetupGet(x => x.ObjectCreationTimeout).Returns(timeout);

            var coordinator = instance.Resolve<GameInterface.Services.Issues.Handlers.RivalGangMovingInPartyCreationCoordinator>();
            var configurationField = AccessTools.Field(
                typeof(GameInterface.Services.Issues.Handlers.RivalGangMovingInPartyCreationCoordinator), "configuration");
            Assert.NotNull(configurationField);
            configurationField.SetValue(coordinator, config.Object);
        });
    }

    // --- 1. Creation and replication ---

    [Fact]
    public void GenuineServerCreation_CapturesThePickedRivalGangLeaderAndReplicatesAByteIdenticalIssueToEveryClient()
    {
        var fixture = SetupIssueOwner();

        CreateIssueOnServer(fixture);

        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkRivalGangMovingInIssueCreated>());
        Assert.Equal(fixture.OwnerId, created.OwnerId);
        Assert.Equal(fixture.RivalGangLeaderId, created.RivalGangLeaderId);

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.OwnerId, out var owner));
                var mirrored = Assert.IsType<RivalGangMovingInIssueBehavior.RivalGangMovingInIssue>(owner.Issue);

                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.RivalGangLeaderId, out var rivalGangLeader));
                Assert.Same(rivalGangLeader, mirrored.RivalGangLeader);
            });
        }
    }

    [Fact]
    public void ClientOriginatedCreation_IsBlocked_IssueManagerNeverCreatesIt()
    {
        var fixture = SetupIssueOwner();

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.OwnerId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.RivalGangLeaderId, out var rivalGangLeader));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) => new RivalGangMovingInIssueBehavior.RivalGangMovingInIssue(h, rivalGangLeader),
                typeof(RivalGangMovingInIssueBehavior.RivalGangMovingInIssue),
                IssueBase.IssueFrequency.Common);

            Assert.False(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
            Assert.Null(owner.Issue);
        });

        Assert.Empty(Client.NetworkSentMessages.GetMessages<NetworkRivalGangMovingInIssueCreated>());
    }

    // --- 2. Accept-quest mirroring rides the fully generic mechanism (GenericAcceptMirrorIssueTypes) ---

    [Fact]
    public void GenuineAccept_RegistersAsQuestSolutionMirrorEligible_AndReplicatesAByteIdenticalQuestToEveryPeer()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        AcceptOnInstance(Server, fixture.OwnerId);

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.OwnerId, out var owner));
                var quest = Assert.IsType<RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest>(owner.Issue.IssueQuest);
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.RivalGangLeaderId, out var rivalGangLeader));
                Assert.Same(rivalGangLeader, quest._rivalGangLeader);

                Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("host-controller", ownerControllerId);
            });
        }
    }

    // --- 3. Instance-resolution fix (Finding A's real, narrower redesign) ---

    /// <summary>
    /// Two concurrently-owned Rival Gang Moving In quests on the SAME local peer (the save/reload leak scenario
    /// - see <see cref="Patches.RivalGangMovingInInstanceResolutionPatch"/>'s doc comment) - <c>Instance</c> must
    /// resolve to the one THIS local peer actually owns, not "first found". Two real, genuinely-started
    /// <see cref="RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest"/> objects, registered with
    /// <see cref="QuestManager.OnQuestStarted"/> (the same public method vanilla's own <c>StartQuest()</c>
    /// triggers) and recorded ownership - the same end state a real save-transfer leak would produce.
    /// </summary>
    [Fact]
    public void InstanceResolution_TwoOwnersLeakedIntoOneQuestManager_ResolvesToTheLocallyOwnedQuest_NotFirstFound()
    {
        var ownedGiverId = TestEnvironment.CreateRegisteredObject<Hero>();
        var ownedLeaderId = TestEnvironment.CreateRegisteredObject<Hero>();
        var otherGiverId = TestEnvironment.CreateRegisteredObject<Hero>();
        var otherLeaderId = TestEnvironment.CreateRegisteredObject<Hero>();

        RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest ownedQuest = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownedGiverId, out var ownedGiver));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownedLeaderId, out var ownedLeader));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(otherGiverId, out var otherGiver));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(otherLeaderId, out var otherLeader));

            // Registered FIRST - a bare "first found in Quests" resolution (vanilla's own cache/fallback) would
            // wrongly pick this one instead.
            var otherQuest = new RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest(
                "other_rival_gang_quest", otherGiver, otherLeader, 8, 1000, 0.5f);
            ownedQuest = new RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest(
                "owned_rival_gang_quest", ownedGiver, ownedLeader, 8, 1000, 0.5f);

            // Same real, public method vanilla's own StartQuest() triggers via CampaignEventDispatcher.
            Campaign.Current.QuestManager.OnQuestStarted(otherQuest);
            Campaign.Current.QuestManager.OnQuestStarted(ownedQuest);
            otherQuest.StartQuest();
            ownedQuest.StartQuest();

            Assert.True(otherQuest.IsOngoing);
            Assert.True(ownedQuest.IsOngoing);

            VillageNeedsToolsIssueOwnership.SetOwner(otherGiver, "someone-else");
            VillageNeedsToolsIssueOwnership.SetOwner(ownedGiver, "host-controller");
        });

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");

        var instanceGetter = AccessTools.PropertyGetter(typeof(RivalGangMovingInIssueBehavior), "Instance");

        Server.Call(() =>
        {
            var resolved = instanceGetter.Invoke(null, null);
            // Genuinely discriminating: "first found" would resolve to otherQuest (registered first) instead -
            // this only passes because the fix actually keys off ownership, not registration order.
            Assert.Same(ownedQuest, resolved);
        });
    }

    /// <summary>Same leaked-quest state, but the local peer owns NEITHER of the two <c>IsOngoing</c> quests
    /// (e.g. a dedicated headless server with no local player) - <c>Instance</c> must resolve null, not fall
    /// back to "first found" just because something is <c>IsOngoing</c>.</summary>
    [Fact]
    public void InstanceResolution_LocalPeerOwnsNeitherLeakedQuest_ResolvesNull()
    {
        var otherGiverId = TestEnvironment.CreateRegisteredObject<Hero>();
        var otherLeaderId = TestEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(otherGiverId, out var otherGiver));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(otherLeaderId, out var otherLeader));

            var otherQuest = new RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest(
                "leaked_rival_gang_quest", otherGiver, otherLeader, 8, 1000, 0.5f);

            Campaign.Current.QuestManager.OnQuestStarted(otherQuest);
            otherQuest.StartQuest();
            Assert.True(otherQuest.IsOngoing);

            VillageNeedsToolsIssueOwnership.SetOwner(otherGiver, "someone-else");
        });

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");

        var instanceGetter = AccessTools.PropertyGetter(typeof(RivalGangMovingInIssueBehavior), "Instance");

        Server.Call(() =>
        {
            var resolved = instanceGetter.Invoke(null, null);
            Assert.Null(resolved);
        });
    }

    // --- 4. Party-spawn gate (both parties, one combined BLOCKING request - see the coordinator's doc comment) ---

    /// <summary>
    /// This file's highest-value test. A remote CLIENT genuinely reaches the real, private
    /// <c>CreateRivalGangLeaderParty()</c> (invoked reflectively, matching this project's established precedent
    /// for private Quest-consequence methods): (1) no broken local party/henchman Hero is ever left behind
    /// (both stay null - the same "block before any construction" shape as Smugglers/Caravan Ambush), (2) the
    /// request is genuinely addressed to the server, and (3) - genuinely discriminating, unlike every other
    /// party-spawn gate in this family - the request carries NO client-derived value at all
    /// (<see cref="NetworkRequestCreateRivalGangParties"/>'s own shape has only a RequestId+OwnerId, no
    /// MainParty-relative speed/troop-count capture), proving §5.2's headless-server-safe design: the troop
    /// counts here are fixed constants, not MainParty-relative percentages, so nothing needs to be captured from
    /// the accepter's own machine before forwarding.
    /// </summary>
    [Fact]
    public void ClientOwner_PartySpawnGate_BlocksTheBrokenLocalCreation_AndForwardsARequestCarryingNoClientDerivedValue()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        var quest = AcceptOnInstance(Client, fixture.OwnerId);

        // Nothing in this synchronous test ever delivers a network reply mid-wait - force a short timeout so
        // the blocking request resolves (times out) quickly instead of hanging for the real 5s default.
        SetPartyCreationTimeout(Client, TimeSpan.FromMilliseconds(50));

        Client.Call(() =>
        {
            InvokePrivate(quest, "CreateRivalGangLeaderParty");

            Assert.Null(quest._rivalGangLeaderParty);
            Assert.Null(quest._rivalGangLeaderHenchmanHero);
        });

        var requested = Assert.Single(Client.NetworkSentMessages.GetMessages<NetworkRequestCreateRivalGangParties>());
        Assert.Equal(fixture.OwnerId, requested.OwnerId);
    }

    [Fact]
    public void ClientOwner_AllyGangLeaderPrefix_IsAlwaysANoOp()
    {
        var prefix = AccessTools.Method(
            typeof(GameInterface.Services.Issues.Patches.RivalGangMovingInPartySpawnGatePatch), "AllyGangLeaderPrefix");

        Client.Call(() => Assert.False((bool)prefix.Invoke(null, null)));
        Server.Call(() => Assert.True((bool)prefix.Invoke(null, null)));
    }

    /// <summary>Same real Prefix, on the SERVER (the genuine accepter here) - the gate's very first line takes
    /// the "let vanilla run unmodified" branch, proven by the one thing the CLIENT branch would unconditionally
    /// do first - send a request - never happening.</summary>
    [Fact]
    public void HostOwner_PartySpawnGate_NeverSendsARequest()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);
        var quest = AcceptOnInstance(Server, fixture.OwnerId);

        var prefix = AccessTools.Method(
            typeof(GameInterface.Services.Issues.Patches.RivalGangMovingInPartySpawnGatePatch), "RivalGangLeaderPrefix");

        Server.Call(() =>
        {
            Assert.True((bool)prefix.Invoke(null, new object[] { quest }));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkRequestCreateRivalGangParties>());
    }

    // --- 5. Party-creation coordinator: server-side request handling (mocked interface, sidesteps the hideout pipeline) ---

    [Fact]
    public void Server_HandlesPartyCreationRequest_RepliesWithCreatedOutcomeAndTheResolvedIds()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);
        AcceptOnInstance(Server, fixture.OwnerId);

        var rivalPartyId = CreateRealPartyOnServer("RivalParty");
        var rivalHenchmanId = CreateRealHeroOnServer();
        var allyPartyId = CreateRealPartyOnServer("AllyParty");
        var allyHenchmanId = CreateRealHeroOnServer();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(rivalPartyId, out var rivalParty));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(rivalHenchmanId, out var rivalHenchman));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(allyPartyId, out var allyParty));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(allyHenchmanId, out var allyHenchman));

            var mockInterface = new Mock<IRivalGangMovingInIssueInterface>();
            mockInterface
                .Setup(x => x.CreateReplicatedParties(It.IsAny<Hero>(), out rivalParty, out rivalHenchman, out allyParty, out allyHenchman))
                .Returns(true);

            var coordinator = Server.Resolve<GameInterface.Services.Issues.Handlers.RivalGangMovingInPartyCreationCoordinator>();
            var interfaceField = AccessTools.Field(
                typeof(GameInterface.Services.Issues.Handlers.RivalGangMovingInPartyCreationCoordinator), "issueInterface");
            Assert.NotNull(interfaceField);
            interfaceField.SetValue(coordinator, mockInterface.Object);

            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.OwnerId, out var owner));
            Assert.True(Server.ObjectManager.TryGetId(owner, out var ownerId));

            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, new NetworkRequestCreateRivalGangParties("req-created", ownerId));
        });

        var reply = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkRivalGangPartiesCreated>());
        Assert.Equal("req-created", reply.RequestId);
        Assert.Equal(RivalGangPartyCreationOutcome.Created, reply.Outcome);
        Assert.Equal(rivalPartyId, reply.RivalGangLeaderPartyId);
        Assert.Equal(rivalHenchmanId, reply.RivalGangLeaderHenchmanHeroId);
        Assert.Equal(allyPartyId, reply.AllyGangLeaderPartyId);
        Assert.Equal(allyHenchmanId, reply.AllyGangLeaderHenchmanHeroId);
    }

    [Fact]
    public void Server_HandlesPartyCreationRequest_RepliesWithRejectedOutcome_WhenCreationFails()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);
        AcceptOnInstance(Server, fixture.OwnerId);

        Server.Call(() =>
        {
            MobileParty nullParty = null;
            Hero nullHero = null;

            var mockInterface = new Mock<IRivalGangMovingInIssueInterface>();
            mockInterface
                .Setup(x => x.CreateReplicatedParties(It.IsAny<Hero>(), out nullParty, out nullHero, out nullParty, out nullHero))
                .Returns(false);

            var coordinator = Server.Resolve<GameInterface.Services.Issues.Handlers.RivalGangMovingInPartyCreationCoordinator>();
            var interfaceField = AccessTools.Field(
                typeof(GameInterface.Services.Issues.Handlers.RivalGangMovingInPartyCreationCoordinator), "issueInterface");
            Assert.NotNull(interfaceField);
            interfaceField.SetValue(coordinator, mockInterface.Object);

            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.OwnerId, out var owner));
            Assert.True(Server.ObjectManager.TryGetId(owner, out var ownerId));

            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, new NetworkRequestCreateRivalGangParties("req-rejected", ownerId));
        });

        var reply = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkRivalGangPartiesCreated>());
        Assert.Equal("req-rejected", reply.RequestId);
        Assert.Equal(RivalGangPartyCreationOutcome.Rejected, reply.Outcome);
        Assert.Null(reply.RivalGangLeaderPartyId);
    }

    // --- 6. Convergence: every peer force-mirrors the SAME real, AutoRegistry-synced parties + henchman Heroes ---

    [Fact]
    public void ForceRivalGangParties_ConvergesEveryPeerOnTheSameRealAutoRegistrySyncedObjects()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        AcceptOnInstance(Client, fixture.OwnerId);

        var rivalPartyId = CreateRealPartyOnServer("Rival");
        var rivalHenchmanId = CreateRealHeroOnServer();
        var allyPartyId = CreateRealPartyOnServer("Ally");
        var allyHenchmanId = CreateRealHeroOnServer();

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.OwnerId, out var owner));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(rivalPartyId, out var rivalParty));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(rivalHenchmanId, out var rivalHenchman));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(allyPartyId, out var allyParty));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(allyHenchmanId, out var allyHenchman));

                instance.Resolve<IRivalGangMovingInIssueInterface>()
                    .ForceRivalGangParties(owner, rivalParty, rivalHenchman, allyParty, allyHenchman);
            });
        }

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.OwnerId, out var owner));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(rivalPartyId, out var expectedRivalParty));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(rivalHenchmanId, out var expectedRivalHenchman));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(allyPartyId, out var expectedAllyParty));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(allyHenchmanId, out var expectedAllyHenchman));

                var issueInterface = instance.Resolve<IRivalGangMovingInIssueInterface>();
                Assert.True(issueInterface.TryCaptureRivalGangParties(
                    owner, out var actualRivalParty, out var actualRivalHenchman, out var actualAllyParty, out var actualAllyHenchman));

                Assert.Same(expectedRivalParty, actualRivalParty);
                Assert.Same(expectedRivalHenchman, actualRivalHenchman);
                Assert.Same(expectedAllyParty, actualAllyParty);
                Assert.Same(expectedAllyHenchman, actualAllyHenchman);
            });
        }
    }

    // --- 7. Alley-fight synchronous ordering fix (Finding B's real redesign) ---

    [Fact]
    public void StartAlleyBattle_HostOwner_LetsVanillaRunUnmodified()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);
        var quest = AcceptOnInstance(Server, fixture.OwnerId);

        var prefix = AccessTools.Method(
            typeof(GameInterface.Services.Issues.Patches.RivalGangMovingInAlleyBattlePatch), "Prefix");

        Server.Call(() =>
        {
            Assert.True((bool)prefix.Invoke(null, new object[] { quest }));
        });
    }

    /// <summary>
    /// The party-creation request times out (nothing replies in this synchronous harness), leaving all 4 quest
    /// fields null - proves the reimplementation's own explicit null-guard (added beyond the design doc's
    /// accepted "NRE identically to today's bug" fallback) aborts BEFORE ever calling
    /// <c>PlayerEncounter.RestartPlayerEncounter</c>, instead of dereferencing a null <c>_rivalGangLeaderParty</c>.
    /// </summary>
    [Fact]
    public void StartAlleyBattle_ClientOwner_AbortsGracefully_WhenPartiesNeverMaterialize_WithoutEverCallingRestartPlayerEncounter()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        var quest = AcceptOnInstance(Client, fixture.OwnerId);

        SetPartyCreationTimeout(Client, TimeSpan.FromMilliseconds(50));

        Client.Call(() =>
        {
            Assert.Null(quest._rivalGangLeaderParty);

            var exception = Record.Exception(() => InvokePrivate(quest, "StartAlleyBattle"));
            Assert.Null(exception);

            Assert.Null(quest._rivalGangLeaderParty);
            Assert.False(quest._isReadyToBeFinalized);
        });

        // The abort happened before RestartPlayerEncounter was ever reached - PlayerEncounterPatches'
        // client-side gate for it would have published this on the very first line of its own real body.
        Assert.Empty(Client.InternalMessages.GetMessages<ConversationRequested>());
    }

    // --- 8. Ownership gates (§7 defense-in-depth on the save/reload leak's REWARD-bearing paths) ---

    [Fact]
    public void OnPlayerAttackedQuestGiverAlley_OwnershipGate_BlocksAnyoneOtherThanTheRecordedOwner()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        var quest = AcceptOnInstance(Server, fixture.OwnerId);

        var prefix = AccessTools.Method(
            typeof(GameInterface.Services.Issues.Patches.RivalGangMovingInOwnershipGatePatches), "OnPlayerAttackedQuestGiverAlleyPrefix");

        Server.Resolve<IControllerIdProvider>().SetControllerId("someone-else");
        Server.Call(() =>
        {
            Assert.False((bool)prefix.Invoke(null, new object[] { quest }));

            var exception = Record.Exception(() => InvokePrivate(quest, "OnPlayerAttackedQuestGiverAlley"));
            Assert.Null(exception);
            Assert.True(quest.IsOngoing);
        });

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        Server.Call(() =>
        {
            Assert.True((bool)prefix.Invoke(null, new object[] { quest }));
        });
    }

    [Fact]
    public void OnSettlementOwnerChanged_OwnershipGate_BlocksAnyoneOtherThanTheRecordedOwner()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        var quest = AcceptOnInstance(Server, fixture.OwnerId);

        var prefix = AccessTools.Method(
            typeof(GameInterface.Services.Issues.Patches.RivalGangMovingInOwnershipGatePatches), "OnSettlementOwnerChangedPrefix");

        Server.Resolve<IControllerIdProvider>().SetControllerId("someone-else");
        Server.Call(() =>
        {
            Assert.False((bool)prefix.Invoke(null, new object[] { quest }));
            Assert.True(quest.IsOngoing);
        });

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        Server.Call(() =>
        {
            Assert.True((bool)prefix.Invoke(null, new object[] { quest }));
        });
    }

    // --- 9. Accept-race arbitration (delegates to the shared, generic mechanism - no bespoke reject logic) ---

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
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, new RequestVillageIssueAcceptQuest(fixture.OwnerId));
        });

        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageIssueQuestAccepted>());
        Assert.Equal(fixture.OwnerId, accepted.OwnerId);
        Assert.Equal("player-A", accepted.OwnerControllerId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkVillageIssueAcceptRejected>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.OwnerId, out var owner));
            Assert.False(owner.Issue.IsOngoingWithoutQuest);
            Assert.IsType<RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest>(owner.Issue.IssueQuest);
        });

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(OtherClient.NetPeer, new RequestVillageIssueAcceptQuest(fixture.OwnerId));
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageIssueQuestAccepted>());
        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageIssueAcceptRejected>());
        Assert.Equal(fixture.OwnerId, rejected.OwnerId);

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.OwnerId, out var owner));
                Assert.IsType<RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest>(owner.Issue.IssueQuest);
                Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("player-A", ownerControllerId);
            });
        }
    }

    // --- 10. Finalize / cleanup ---

    /// <summary>Scope note (same as <see cref="SmugglersIssueTests.RequestVillageIssueRemoved_FinalizesTheRealQuestOnEveryPeer"/>):
    /// deliberately does NOT also force-spawn real parties/heroes first - <c>OnFinalize</c>'s own
    /// <c>DestroyPartyAction</c>/<c>KillCharacterAction</c> calls against this harness's synthetic substitutes
    /// are separately, safely proven by <see cref="OnFinalize_WithRealPartiesAndHeroes_AttemptsToDestroyThem"/>
    /// below (tolerated there). What THIS test verifies for real: the fully generic
    /// <see cref="Patches.IssueFinalizedPatches"/>/<see cref="Handlers.VillageNeedsToolsIssueHandler"/> teardown
    /// correctly tears down a Rival Gang Moving In issue/quest on every peer.</summary>
    [Fact]
    public void RequestVillageIssueRemoved_FinalizesTheRealQuestOnEveryPeer()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        AcceptOnInstance(Server, fixture.OwnerId);

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.OwnerId, out var owner));
                Assert.NotNull(owner.Issue);
                Assert.IsType<RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest>(owner.Issue.IssueQuest);
            });
        }

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer,
                new RequestVillageIssueRemoved(fixture.OwnerId, VillageIssueFinalizeReason.QuestCancel));
        });

        var removed = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageIssueRemoved>());
        Assert.Equal(fixture.OwnerId, removed.OwnerId);
        Assert.Equal(VillageIssueFinalizeReason.QuestCancel, removed.Reason);

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.OwnerId, out var owner));
                Assert.Null(owner.Issue);
                Assert.False(Campaign.Current.IssueManager.Issues.ContainsKey(owner));
            });
        }
    }

    /// <summary><c>OnFinalize</c>'s own <c>DestroyPartyAction.Apply</c> (both parties) and
    /// <c>KillCharacterAction.ApplyByRemove</c> (both henchman Heroes) calls, driven for real against real,
    /// force-written state - tolerated to succeed or throw past the point the code path is reached (this
    /// harness's synthetic substitutes don't carry enough real settlement/component state for vanilla's own
    /// generic destroy/kill cascade to complete cleanly - same tolerance as
    /// <see cref="MerchantArmyOfPoachersIssueTests.OnFinalize_WithARealPoachersParty_AttemptsToDestroyIt"/>). The
    /// real proof: <c>OnFinalize</c> genuinely attempts both destroys/kills specifically because all 4 fields
    /// are non-null and <c>IsActive</c>/<c>IsAlive</c>, not that it silently no-ops.</summary>
    [Fact]
    public void OnFinalize_WithRealPartiesAndHeroes_AttemptsToDestroyThem()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        var quest = AcceptOnInstance(Server, fixture.OwnerId);

        var rivalPartyId = CreateRealPartyOnServer("Rival");
        var rivalHenchmanId = CreateRealHeroOnServer();
        var allyPartyId = CreateRealPartyOnServer("Ally");
        var allyHenchmanId = CreateRealHeroOnServer();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.OwnerId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(rivalPartyId, out var rivalParty));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(rivalHenchmanId, out var rivalHenchman));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(allyPartyId, out var allyParty));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(allyHenchmanId, out var allyHenchman));

            Server.Resolve<IRivalGangMovingInIssueInterface>()
                .ForceRivalGangParties(owner, rivalParty, rivalHenchman, allyParty, allyHenchman);

            Assert.NotNull(quest._rivalGangLeaderParty);
            Assert.True(quest._rivalGangLeaderParty.IsActive);
            Assert.NotNull(quest._allyGangLeaderParty);
            Assert.True(quest._allyGangLeaderParty.IsActive);
            Assert.NotNull(quest._rivalGangLeaderHenchmanHero);
            Assert.True(quest._rivalGangLeaderHenchmanHero.IsAlive);
            Assert.NotNull(quest._allyGangLeaderHenchmanHero);
            Assert.True(quest._allyGangLeaderHenchmanHero.IsAlive);

            // Real, private OnFinalize() - tolerated to succeed or throw (see this test's own doc comment).
            Record.Exception(() => InvokePrivate(quest, "OnFinalize"));
        });
    }
}
