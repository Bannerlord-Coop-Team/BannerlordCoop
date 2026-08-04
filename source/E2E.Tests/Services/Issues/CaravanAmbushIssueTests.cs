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
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

/// <summary>
/// Real, executed multi-peer coverage for Caravan Ambush (source/GameInterface/Services/Issues/
/// {Interfaces,Messages,Handlers,Patches}/CaravanAmbush*.cs), following the same harness/conventions
/// established by <see cref="SmugglersIssueTests"/>.
///
/// Standing-lesson re-check findings this suite exercises for real (see
/// Patches.CaravanAmbushPartySpawnGatePatch/CaravanAmbushQuestOwnershipGatePatch/
/// CaravanAmbushCaravaneerDialogNullGuardPatch's own doc comments for the full derivation): the design doc's
/// "clean, no gate needed" classification for this quest was WRONG on three counts - (1) BOTH _caravanParty AND
/// _banditParty hit the same client-authority construction gap Smugglers needed, not just one, and (2) unlike
/// Smugglers there is NO separable private "CreateXParty()" method, so the real fix gates the WHOLE
/// <c>OnQuestAccepted()</c> method instead - one combined gate covering both parties, not two independent
/// gates, (3) the caravaneer's own gratitude dialogue has both a missing ownership check (this quest's two
/// convergent completion methods, <c>OnQuestSucceeded</c>/<c>OnPlayerHiredBandits</c>) AND a separate,
/// independently-found, pre-existing vanilla null-deref bug in that same dialogue's Condition.
///
/// Scope note on the party-spawn tests (same established precedent as
/// <see cref="SmugglersIssueTests"/>'s own type doc comment): this file deliberately does NOT drive
/// <see cref="ICaravanAmbushIssueInterface.CreateReplicatedAccept"/> all the way through vanilla's own
/// bandit-hideout pick (<c>SettlementHelper.FindNearestHideoutToMobileParty</c>, which walks <c>Hideout.All</c>)
/// and culture-caravan-template roll (<c>CaravanHelper.GetRandomCaravanTemplate</c>) - standing up that much
/// real map/faction/culture data is out of scope for this harness. What IS fully, genuinely exercised instead:
/// <see cref="Patches.CaravanAmbushPartySpawnGatePatch"/>'s Prefix (the actual bug fix - proven by calling the
/// REAL, private <c>OnQuestAccepted()</c> on a client via reflection and observing the real block, the real
/// captured accepter-own-MainParty-derived speed, and that no broken local party gets left behind), and the
/// full convergence/idempotency/ownership/crash-guard mechanics using parties constructed the same
/// already-proven-safe way <see cref="SmugglersIssueTests"/> does (a real, AutoRegistry-synced
/// <see cref="MobileParty"/>, not built via the caravan/bandit-specific factory call chain).
/// </summary>
public class CaravanAmbushIssueTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();
    private EnvironmentInstance OtherClient => TestEnvironment.Clients.Last();
    private IEnumerable<EnvironmentInstance> AllInstances => new[] { Server }.Concat(TestEnvironment.Clients);

    public CaravanAmbushIssueTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private record CaravanAmbushFixture(string HeroId, string TargetSettlementId);

    /// <summary>Builds the issue-owning Hero plus a target settlement. Also seeds
    /// <see cref="Campaign.EncyclopediaManager"/> on every instance - the real activation log eagerly resolves
    /// encyclopedia links, same hazard/fix as <see cref="SmugglersIssueTests.SetupIssueOwner"/>. Also sets the
    /// quest giver's own <c>CurrentSettlement</c> - <c>CaravanAmbushIssueQuestActivatedLogText</c>'s getter
    /// (run for real by <see cref="ICaravanAmbushIssueInterface.RunLocalAcceptSideEffects"/>) dereferences
    /// <c>base.QuestGiver.CurrentSettlement.EncyclopediaLinkWithName</c> unconditionally.</summary>
    private CaravanAmbushFixture SetupIssueOwner()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var targetSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();

                    Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var owner));
                    Assert.True(instance.ObjectManager.TryGetObject<Settlement>(targetSettlementId, out var target));
                    // Hero.CurrentSettlement is read-only, derived from (in priority order) PartyBelongedTo /
                    // PartyBelongedToAsPrisoner / StayingInSettlement - the last has a public setter and is the
                    // correct "a notable hero without their own party is physically at this settlement" shape.
                    owner.StayingInSettlement = target;
                }
            });
        }

        return new CaravanAmbushFixture(heroId, targetSettlementId);
    }

    /// <summary>Drives the real server-authoritative creation path: a genuine
    /// <see cref="IssueManager.CreateNewIssue"/> call handing back a real
    /// <see cref="CaravanAmbushIssueBehavior.CaravanAmbushIssue"/> constructed with the given (server-picked)
    /// settlement - exactly what <c>CaravanAmbushIssueBehavior.OnIssueSelected</c> does for real, just with the
    /// settlement supplied directly instead of re-running <c>GetTargetSettlement</c>'s own scan.</summary>
    private void CreateIssueOnServer(CaravanAmbushFixture fixture)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.TargetSettlementId, out var target));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) => new CaravanAmbushIssueBehavior.CaravanAmbushIssue(h, target),
                typeof(CaravanAmbushIssueBehavior.CaravanAmbushIssue),
                IssueBase.IssueFrequency.Common);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
        });
    }

    /// <summary>Real (unwrapped) accept on <paramref name="instance"/> - drives
    /// <see cref="IssueManager.StartIssueQuest"/> for real, which (per <see cref="GenericAcceptMirrorIssueTypes"/>)
    /// triggers the fully generic accept-broadcast/ownership-record path unchanged.</summary>
    private CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest AcceptOnInstance(EnvironmentInstance instance, string heroId)
    {
        CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest quest = null;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var owner));
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            quest = Assert.IsType<CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest>(owner.Issue.IssueQuest);
        });
        return quest;
    }

    /// <summary>Builds a real, fully-constructed <see cref="MobileParty"/> the same already-proven-safe way
    /// <see cref="SmugglersIssueTests.CreateRealPartyOnServer"/> does - used as this file's stand-in for "the
    /// server's genuine <see cref="ICaravanAmbushIssueInterface.CreateReplicatedAccept"/> result" wherever a
    /// test needs a real, converged party without standing up the hideout/caravan-template pipeline.</summary>
    private string CreateRealPartyOnServer(string customName)
    {
        string partyId = null;
        Server.Call(() =>
        {
            var settlement = Util.GameObjectCreator.CreateInitializedObject<Settlement>();
            var hero = Util.GameObjectCreator.CreateInitializedObject<Hero>();
            var clan = Util.GameObjectCreator.CreateInitializedObject<Clan>();
            var template = Util.GameObjectCreator.CreateInitializedObject<PartyTemplateObject>();

            var party = CustomPartyComponent.CreateCustomPartyWithPartyTemplate(
                new CampaignVec2(new Vec2(5, 5), true), 5, settlement, new TextObject(customName), clan, template, hero);

            Assert.True(Server.ObjectManager.TryGetId(party, out partyId));
        });
        return partyId;
    }

    private static void InvokePrivate(object instance, string methodName)
    {
        var method = AccessTools.Method(instance.GetType(), methodName);
        method.Invoke(instance, null);
    }

    // --- 1. Creation and replication ---

    [Fact]
    public void GenuineServerCreation_CapturesThePickedTargetSettlementAndReplicatesAByteIdenticalIssueToEveryClient()
    {
        var fixture = SetupIssueOwner();

        CreateIssueOnServer(fixture);

        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkCaravanAmbushIssueCreated>());
        Assert.Equal(fixture.HeroId, created.OwnerId);
        Assert.Equal(fixture.TargetSettlementId, created.TargetSettlementId);

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                var mirrored = Assert.IsType<CaravanAmbushIssueBehavior.CaravanAmbushIssue>(owner.Issue);

                Assert.True(client.ObjectManager.TryGetObject<Settlement>(fixture.TargetSettlementId, out var target));
                Assert.Same(target, mirrored._targetSettlement);
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
            Assert.True(Client.ObjectManager.TryGetObject<Settlement>(fixture.TargetSettlementId, out var target));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) => new CaravanAmbushIssueBehavior.CaravanAmbushIssue(h, target),
                typeof(CaravanAmbushIssueBehavior.CaravanAmbushIssue),
                IssueBase.IssueFrequency.Common);

            Assert.False(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
            Assert.Null(owner.Issue);
        });

        Assert.Empty(Client.NetworkSentMessages.GetMessages<NetworkCaravanAmbushIssueCreated>());
    }

    // --- 2. Accept-quest mirroring rides the fully generic mechanism (GenericAcceptMirrorIssueTypes) ---

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
                var quest = Assert.IsType<CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest>(owner.Issue.IssueQuest);
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(fixture.TargetSettlementId, out var target));
                Assert.Same(target, quest._targetSettlement);

                Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("host-controller", ownerControllerId);
            });
        }
    }

    // --- 3. The party-spawn gate (both parties, one combined gate - see the type doc comment) ---

    /// <summary>
    /// This file's highest-value test. A remote CLIENT (not the server) genuinely accepts and its own live
    /// dialogue Consequence reaches the real, private <c>OnQuestAccepted()</c> - proving the real bug this
    /// quest needed fixing is actually closed: (1) neither <c>_caravanParty</c> nor <c>_banditParty</c> ends up
    /// as a broken local reference (both stay null on the accepter's own machine, per the same "block before
    /// any party construction" shape as Smugglers), (2) the captured speed comes from the ACCEPTER'S OWN
    /// MainParty, genuinely different from what the SERVER's own MainParty would report, proving the forwarded
    /// request does not let the server silently substitute its own, and (3) the request is correctly addressed
    /// to the server. Also proves <see cref="ICaravanAmbushIssueInterface.RunLocalAcceptSideEffects"/> ran on
    /// the accepter's own machine even though blocked: <c>StartQuest()</c> genuinely fired (<c>quest.IsOngoing</c>
    /// is true), matching the Category A requirement that activation happens on the REAL accepter's machine, not
    /// the server's.
    /// </summary>
    [Fact]
    public void ClientAccepter_PartySpawnGate_BlocksTheBrokenLocalCreation_ButStillActivatesLocally_AndCapturesTheAccepterOwnMainPartySpeed()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        var serverPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var clientPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        // Deliberately different speeds via IsActive=false (soft 0-speed fallback, same technique
        // SmugglersIssueTests uses) so a divergence would be caught if the gate ever let the server re-derive
        // the speed from ITS OWN MainParty instead of the captured, forwarded value.
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(serverPartyId, out var serverParty));
            serverParty.IsActive = false;
            Campaign.Current.MainParty = serverParty;
        });

        var quest = AcceptOnInstance(Client, fixture.HeroId);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(clientPartyId, out var clientParty));
            clientParty.IsActive = false;
            Campaign.Current.MainParty = clientParty;
            var expectedSpeed = clientParty.Speed;

            // The real dialogue Consequence delegate a live "I'll help you myself" acceptance would invoke -
            // private, so reflection-invoked, matching this project's own established precedent for private
            // Quest-consequence methods.
            InvokePrivate(quest, "OnQuestAccepted");

            // Activated locally (Category A correctness) even though blocked.
            Assert.True(quest.IsOngoing);

            // No broken local party was ever left behind on either field.
            Assert.Null(quest._caravanParty);
            Assert.Null(quest._banditParty);

            var requested = Assert.Single(Client.InternalMessages.GetMessages<CaravanAmbushAcceptRequested>());
            Assert.True(Client.ObjectManager.TryGetId(requested.Owner, out var reqOwnerId));
            Assert.Equal(fixture.HeroId, reqOwnerId);
            Assert.Equal(expectedSpeed, requested.AccepterMainPartySpeed);
        });

        var forwarded = Assert.Single(Client.NetworkSentMessages.GetMessages<RequestCaravanAmbushAccept>());
        Assert.Equal(fixture.HeroId, forwarded.OwnerId);

        // Never broadcast to anyone - a client's own gate never reaches the real creation/broadcast path.
        Assert.Empty(Client.NetworkSentMessages.GetMessages<NetworkCaravanAmbushAccepted>());
    }

    /// <summary>
    /// Same real Prefix, on the SERVER (the genuine accepter here). <c>ModInformation.IsClient</c> is false, so
    /// the gate's very first line takes the "let vanilla run unmodified" branch - proven the same way the
    /// branch itself is structured: regardless of whatever happens afterward deeper in vanilla's own
    /// hideout/caravan-template pipeline (out of this harness's scope - see the type doc comment;
    /// <see cref="Record.Exception"/> tolerates it succeeding OR throwing), the one thing the CLIENT branch
    /// would unconditionally do FIRST - publish <see cref="CaravanAmbushAcceptRequested"/> - can never have
    /// happened.
    /// </summary>
    [Fact]
    public void HostAccepter_PartySpawnGate_NeverForwardsARequest()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        var mainPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");

        var quest = AcceptOnInstance(Server, fixture.HeroId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mainPartyId, out var mainParty));
            Campaign.Current.MainParty = mainParty;

            Record.Exception(() => InvokePrivate(quest, "OnQuestAccepted"));
        });

        Assert.Empty(Server.InternalMessages.GetMessages<CaravanAmbushAcceptRequested>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<RequestCaravanAmbushAccept>());
    }

    // --- 4. Convergence: every peer force-mirrors the SAME real, AutoRegistry-synced parties + reward items ---

    [Fact]
    public void NetworkCaravanAmbushAccepted_ForceWritesTheSameRealStateOntoEveryPeersMirror_IncludingTheOriginalAccepter()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        AcceptOnInstance(Client, fixture.HeroId);

        var caravanPartyId = CreateRealPartyOnServer("Caravan");
        var banditPartyId = CreateRealPartyOnServer("Raiders");

        // Simulate what Handlers.CaravanAmbushIssueHandler.Handle_RequestCaravanAmbushAccept does once
        // ICaravanAmbushIssueInterface.CreateReplicatedAccept genuinely succeeds - publish the same local event
        // it would, with the real (AutoRegistry-synced) parties and a synthetic reward-item list.
        List<ItemObject> rewardItems = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(caravanPartyId, out var caravanParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(banditPartyId, out var banditParty));

            rewardItems = new List<ItemObject> { Util.GameObjectCreator.CreateInitializedObject<ItemObject>() };

            Server.Resolve<ICaravanAmbushIssueInterface>().ForceAcceptedState(owner, caravanParty, banditParty, rewardItems);

            Server.Resolve<IMessageBroker>().Publish(null, new CaravanAmbushAccepted(owner, caravanParty, banditParty, rewardItems));
        });

        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkCaravanAmbushAccepted>());
        Assert.Equal(fixture.HeroId, accepted.OwnerId);
        Assert.Equal(caravanPartyId, accepted.CaravanPartyId);
        Assert.Equal(banditPartyId, accepted.BanditPartyId);
        Assert.Single(accepted.RewardItemIds);

        // Every peer - including the client that originally accepted and whose own local creation was blocked
        // (both party fields null right after OnQuestAccepted) - now references the exact same real state.
        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(caravanPartyId, out var expectedCaravan));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(banditPartyId, out var expectedBandit));

                var issueInterface = instance.Resolve<ICaravanAmbushIssueInterface>();
                Assert.True(issueInterface.TryCaptureAcceptedState(owner, out var actualCaravan, out var actualBandit, out var actualRewardItems));
                Assert.Same(expectedCaravan, actualCaravan);
                Assert.Same(expectedBandit, actualBandit);
                Assert.Single(actualRewardItems);
            });
        }
    }

    [Fact]
    public void RequestCaravanAmbushAccept_IsIdempotent_AlreadyAcceptedStateIsNeverReCreatedOrReBroadcast()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);
        AcceptOnInstance(Server, fixture.HeroId);

        var caravanPartyId = CreateRealPartyOnServer("Caravan");
        var banditPartyId = CreateRealPartyOnServer("Raiders");

        // Force the server's own quest mirror to already have real state - as if an earlier request had
        // already succeeded (e.g. this is a resend).
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(caravanPartyId, out var caravanParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(banditPartyId, out var banditParty));
            Server.Resolve<ICaravanAmbushIssueInterface>().ForceAcceptedState(owner, caravanParty, banditParty, null);
        });

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, new RequestCaravanAmbushAccept(fixture.HeroId, 5f));
        });

        // TryCaptureAcceptedState's guard in Handle_RequestCaravanAmbushAccept returned early, before ever
        // reaching CreateReplicatedAccept (which would otherwise try to spawn a SECOND pair of parties) - no
        // new broadcast at all.
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkCaravanAmbushAccepted>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.Resolve<ICaravanAmbushIssueInterface>().TryCaptureAcceptedState(owner, out var stillCaravan, out _, out _));
            Assert.True(Server.ObjectManager.TryGetId(stillCaravan, out var stillCaravanId));
            Assert.Equal(caravanPartyId, stillCaravanId);
        });
    }

    // --- 5. Ownership gate on both convergent completion paths ---

    /// <summary>
    /// Two layers of proof, matching this project's own review discipline (revert-and-confirm-fails):
    /// (1) end-to-end - the REAL, private <c>OnQuestSucceeded()</c>, invoked while a non-owner controller id is
    /// active, mutates nothing and throws nothing (the Prefix returns false BEFORE touching any of the deep
    /// vanilla Hero.MainHero/Clan.PlayerClan/MobileParty.MainParty state this harness doesn't stand up - see
    /// this file's own type doc comment on scope). (2) isolated - the gate's own Prefix method, invoked
    /// directly by reflection, returns exactly <see cref="VillageNeedsToolsIssueOwnership.IsLocalPeerOwner"/>'s
    /// verdict for both the non-owner and the real-owner controller id, independent of whether vanilla's own
    /// deep completion chain can run to completion in this harness.
    /// </summary>
    [Fact]
    public void OnQuestSucceeded_OwnershipGate_BlocksAnyoneOtherThanTheRecordedOwner()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);

        var prefix = AccessTools.Method(
            typeof(GameInterface.Services.Issues.Patches.CaravanAmbushQuestOwnershipGatePatch), "OnQuestSucceededPrefix");

        // Not the recorded owner (e.g. a dedicated server with no local player, or after someone else's accept
        // won a race) - the gate blocks the completion outright, even though nothing else here checks
        // ownership at all in vanilla. End-to-end: the real body is never reached, so nothing throws and
        // nothing mutates.
        Server.Resolve<IControllerIdProvider>().SetControllerId("someone-else");
        Server.Call(() =>
        {
            Assert.False((bool)prefix.Invoke(null, new object[] { quest }));

            var exception = Record.Exception(() => InvokePrivate(quest, "OnQuestSucceeded"));
            Assert.Null(exception);
            Assert.True(quest.IsOngoing);
        });

        // Restored to the real owner - the gate itself now genuinely allows the real body through.
        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        Server.Call(() =>
        {
            Assert.True((bool)prefix.Invoke(null, new object[] { quest }));
        });
    }

    /// <summary>Same two-layer proof as <see cref="OnQuestSucceeded_OwnershipGate_BlocksAnyoneOtherThanTheRecordedOwner"/>,
    /// for the alternate "recruit the bandits outright" success path.</summary>
    [Fact]
    public void OnPlayerHiredBandits_OwnershipGate_BlocksAnyoneOtherThanTheRecordedOwner()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);

        var prefix = AccessTools.Method(
            typeof(GameInterface.Services.Issues.Patches.CaravanAmbushQuestOwnershipGatePatch), "OnPlayerHiredBanditsPrefix");

        Server.Resolve<IControllerIdProvider>().SetControllerId("someone-else");
        Server.Call(() =>
        {
            Assert.False((bool)prefix.Invoke(null, new object[] { quest }));

            var exception = Record.Exception(() => InvokePrivate(quest, "OnPlayerHiredBandits"));
            Assert.Null(exception);
            Assert.True(quest.IsOngoing);
        });

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        Server.Call(() =>
        {
            Assert.True((bool)prefix.Invoke(null, new object[] { quest }));
        });
    }

    // --- 6. Independently-found vanilla crash-guard: GetCaravaneerDialogFlow's Condition null-deref ---

    /// <summary>
    /// Proves the fix for the pre-existing vanilla null-deref (see
    /// <see cref="Patches.CaravanAmbushCaravaneerDialogNullGuardPatch"/>'s doc comment): with <c>_caravanParty</c>
    /// still null (the exact state a non-owner's mirror - or, narrowly, even the genuine accepter - can be in
    /// before the party-spawn gate's force-write lands), evaluating the real, compiler-generated Condition
    /// delegate would previously throw a <see cref="NullReferenceException"/>. With the guard in place it
    /// returns false cleanly instead. Once a real (non-null) party is force-written, the guard steps aside and
    /// the real body runs (proven by it no longer short-circuiting to a hardcoded false without even touching
    /// <c>ConversationHelper</c> - tolerated via <see cref="Record.Exception"/> since standing up a real live
    /// conversation context is out of this harness's scope, matching this file's own scope note).
    /// </summary>
    [Fact]
    public void GetCaravaneerDialogFlowCondition_NullGuard_ReturnsFalseInsteadOfThrowing_WhenCaravanPartyIsNull()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);
        var quest = AcceptOnInstance(Server, fixture.HeroId);

        Server.Call(() =>
        {
            Assert.Null(quest._caravanParty);

            var method = AccessTools.GetDeclaredMethods(typeof(CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest))
                .First(m => m.Name.Contains("GetCaravaneerDialogFlow") && m.ReturnType == typeof(bool) && m.GetParameters().Length == 0);

            var exception = Record.Exception(() => method.Invoke(quest, null));
            Assert.Null(exception);

            var result = (bool)method.Invoke(quest, null);
            Assert.False(result);
        });
    }

    [Fact]
    public void GetCaravaneerDialogFlowCondition_RunsTheRealBody_OnceCaravanPartyIsForced()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);
        var quest = AcceptOnInstance(Server, fixture.HeroId);

        var partyId = CreateRealPartyOnServer("Caravan");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Server.Resolve<ICaravanAmbushIssueInterface>().ForceAcceptedState(owner, party, party, null);
            Assert.NotNull(quest._caravanParty);

            var method = AccessTools.GetDeclaredMethods(typeof(CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest))
                .First(m => m.Name.Contains("GetCaravaneerDialogFlow") && m.ReturnType == typeof(bool) && m.GetParameters().Length == 0);

            // With a real, non-null party, the guard's Prefix returns true (runs the real body) instead of
            // short-circuiting - real body execution against a synthetic, no-live-conversation context is
            // tolerated to succeed or throw (out of this harness's scope, matching this file's own scope note).
            Record.Exception(() => method.Invoke(quest, null));
        });
    }

    // --- 7. Accept-race arbitration (delegates to the shared, generic mechanism - no bespoke reject logic) ---

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
            Assert.IsType<CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest>(owner.Issue.IssueQuest);
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
                Assert.IsType<CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest>(owner.Issue.IssueQuest);
                Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("player-A", ownerControllerId);
            });
        }
    }

    // --- 8. Finalize / cleanup: matches vanilla's own "release to ambient", NOT a party-destroy shape ---

    /// <summary>
    /// Vanilla's own <c>CaravanAmbushIssueQuest</c> has NO <c>OnFinalize</c> override at all (confirmed by
    /// direct decompile read) - survivors get their AI re-enabled and sent home via
    /// <c>HandlePartyAiAfterCompletion()</c> from within the success/failure handlers themselves, never a
    /// party-destroy call from finalize. This test proves the fully generic
    /// <see cref="Patches.IssueFinalizedPatches"/>/<see cref="Handlers.VillageNeedsToolsIssueHandler"/> teardown
    /// this quest rides unchanged doesn't introduce one either: after a genuine finalize, the quest/issue are
    /// gone from every peer, but the real, AutoRegistry-synced caravan/bandit <see cref="MobileParty"/> objects
    /// are still alive and un-destroyed everywhere - the "release to ambient" shape, not "despawn".
    /// </summary>
    [Fact]
    public void RequestVillageIssueRemoved_FinalizesTheRealQuestOnEveryPeer_WithoutDestroyingTheSurvivingParties()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        AcceptOnInstance(Server, fixture.HeroId);

        var caravanPartyId = CreateRealPartyOnServer("Caravan");
        var banditPartyId = CreateRealPartyOnServer("Raiders");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(caravanPartyId, out var caravanParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(banditPartyId, out var banditParty));
            Server.Resolve<ICaravanAmbushIssueInterface>().ForceAcceptedState(owner, caravanParty, banditParty, null);
        });

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.NotNull(owner.Issue);
                Assert.IsType<CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest>(owner.Issue.IssueQuest);
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

                // Not destroyed - vanilla's own "release to ambient" shape (no OnFinalize override on this
                // quest type at all - see this test's own doc comment), still resolvable on every peer.
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(caravanPartyId, out _));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(banditPartyId, out _));
            });
        }
    }
}
