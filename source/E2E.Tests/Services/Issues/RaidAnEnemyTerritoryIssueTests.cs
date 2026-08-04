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
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

/// <summary>
/// Real, executed multi-peer coverage for Raid an Enemy Territory (source/GameInterface/Services/Issues/
/// {Interfaces,Messages,Handlers,Patches}/RaidAnEnemyTerritory*.cs), following the same harness/conventions
/// established by <see cref="TheConquestOfSettlementIssueTests"/>.
///
/// The confirmed, root-caused bug this suite exists to prove fixed (see
/// <see cref="Patches.RaidAnEnemyTerritoryRaidCompletionPatches"/> for the full derivation): vanilla's own
/// <c>OnRaidCompleted</c> compared the real, correctly-known raiding party against <c>MobileParty.MainParty</c>
/// (via <c>MapEvent.IsPlayerMapEvent</c>/<c>MapEvent.PlayerSide</c>) - "whichever local avatar happens to be
/// running this process," not this quest's real recorded owner - and its trigger only ever reaches the coop
/// server's own process at all. Also covers the turn-in ownership gate
/// (<see cref="Patches.RaidAnEnemyTerritoryOwnershipGatePatches"/>) and the raid-progress network broadcast
/// (<see cref="Handlers.RaidAnEnemyTerritoryIssueHandler"/>) that keeps a non-server real owner's own mirror in
/// sync.
/// </summary>
public class RaidAnEnemyTerritoryIssueTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();
    private EnvironmentInstance OtherClient => TestEnvironment.Clients.Last();
    private IEnumerable<EnvironmentInstance> AllInstances => new[] { Server }.Concat(TestEnvironment.Clients);

    public RaidAnEnemyTerritoryIssueTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private record RaidFixture(string HeroId, string EnemyKingdomId);

    /// <summary>Builds the issue-giving Hero plus an at-war enemy Kingdom. Also seeds
    /// <see cref="Campaign.EncyclopediaManager"/> on every instance (the real journal logs eagerly resolve
    /// encyclopedia links, same hazard/fix as every other Group A/Tier 3 type's own <c>SetupIssueOwner</c>), and
    /// constructs a bare <see cref="RaidAnEnemyTerritoryIssueBehavior"/> and calls its real
    /// <c>RegisterEvents()</c> on every instance - this harness's bootstrap does not itself instantiate every one
    /// of vanilla's ~40 issue-type behaviors, so without this explicit call
    /// <see cref="Patches.RaidAnEnemyTerritoryRaidCompletionPatches"/>'s own module-level
    /// <c>RaidCompletedEvent</c> listener (subscribed from a Postfix on this exact method) would never get wired
    /// at all - harmless to call directly, matching this method's real production entry point.</summary>
    private RaidFixture SetupIssueOwner()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var enemyKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                }

                new RaidAnEnemyTerritoryIssueBehavior().RegisterEvents();
            });
        }

        return new RaidFixture(heroId, enemyKingdomId);
    }

    /// <summary>Drives the real server-authoritative creation path: a genuine <see cref="IssueManager.CreateNewIssue"/>
    /// call handing back a real <see cref="RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue"/>
    /// constructed for the given owner, with the enemy kingdom force-written directly (bypassing the real ctor's
    /// own RNG pick) - exactly what <see cref="Interfaces.IRaidAnEnemyTerritoryIssueInterface.ConstructReplicated"/>
    /// does for real.</summary>
    private void CreateIssueOnServer(RaidFixture fixture)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(fixture.EnemyKingdomId, out var enemyKingdom));

            var issueInterface = Server.Resolve<IRaidAnEnemyTerritoryIssueInterface>();

            // Constructed LAZILY, inside the factory CreateNewIssue itself invokes (using its own injected Hero
            // parameter) - not eagerly beforehand - so IssueBase's ctor side effect of setting owner.Issue
            // happens at exactly the same point in CreateNewIssue's own bookkeeping as a genuine, un-replicated
            // creation would; matches TheConquestOfSettlementIssueTests.CreateIssueOnServer's own shape.
            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) => issueInterface.ConstructReplicated(h, enemyKingdom),
                typeof(RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue),
                IssueBase.IssueFrequency.VeryCommon);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
        });
    }

    /// <summary>Real (unwrapped) accept on <paramref name="instance"/> - drives <see cref="IssueManager.StartIssueQuest"/>
    /// for real, which (per <see cref="GenericAcceptMirrorIssueTypes"/>) triggers the fully generic
    /// accept-broadcast/ownership-record path unchanged. Does NOT by itself call <c>StartQuest()</c>/
    /// <c>RegisterEvents()</c> - see <see cref="Patches.RaidAnEnemyTerritoryRaidCompletionPatches"/>'s doc
    /// comment for why that's a separate, live-dialogue-only step (invoked via <see cref="InvokePrivate"/>
    /// where a test needs the genuine per-quest listeners wired, e.g. the ownership-gate tests).</summary>
    private RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest AcceptOnInstance(EnvironmentInstance instance, string heroId)
    {
        RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest quest = null;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var owner));
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            quest = Assert.IsType<RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest>(owner.Issue.IssueQuest);
        });
        return quest;
    }

    /// <summary>Registers a connected player (a real, addressable <see cref="Player"/> entry that
    /// <see cref="RaidAnEnemyTerritoryIssueOwnershipResolver.TryResolveOwner"/> can resolve ControllerId -&gt;
    /// Hero/MobileParty through) on the server, with a fresh Hero and MobileParty of its own. Deliberately
    /// separate from <see cref="Hero.MainHero"/>/<c>Campaign.Current.MainParty</c> - see the type doc comment.</summary>
    private (string HeroId, string PartyId) RegisterConnectedPlayer(string controllerId)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(controllerId, heroId, partyId, "", "")));
        });

        return (heroId, partyId);
    }

    private static void InvokePrivate(object instance, string methodName, params object[] args)
    {
        var method = AccessTools.Method(instance.GetType(), methodName);
        method.Invoke(instance, args);
    }

    /// <summary>
    /// Directly invokes <see cref="Patches.RaidAnEnemyTerritoryRaidCompletionPatches"/>'s internal static
    /// <c>Resolve</c> method - the actual bug fix/decision logic (owner resolution + progress application +
    /// broadcast) - against the quest object <paramref name="quest"/> the caller already holds, bypassing both
    /// the genuine <c>CampaignEvents.RaidCompletedEvent</c> subscribe-then-dispatch round trip AND the
    /// <c>OnRaidCompleted</c> wrapper's own <c>Campaign.Current.IssueManager.Issues</c> scan/MapEvent
    /// construction.
    ///
    /// Harness limitation (confirmed empirically, not a fix-quality concern - same note as
    /// <c>TheConquestOfSettlementIssueTests.InvokeSiegeCompletedResolve</c>): this harness's <c>IObjectManager</c>
    /// does not return the SAME Hero instance for a given StringId across separate <c>EnvironmentInstance.Call()</c>
    /// invocations, so <c>Campaign.Current.IssueManager.Issues</c> can silently fail to dictionary-match a Hero
    /// re-resolved in a later call even against its own genuine entry. Calling <c>Resolve</c> directly against
    /// the already-held <c>quest</c> reference sidesteps that lookup entirely while still exercising the exact
    /// same decision logic <c>OnRaidCompleted</c>'s scan would have called it with for every attacker-side party.
    /// </summary>
    private static void InvokeResolve(object quest, Settlement raidedSettlement, MobileParty candidateAttackerParty)
    {
        var method = AccessTools.Method(
            typeof(GameInterface.Services.Issues.Patches.RaidAnEnemyTerritoryRaidCompletionPatches), "Resolve");
        Assert.True(method != null, "Resolve method not found via reflection");
        try
        {
            method.Invoke(null, new object[] { quest, raidedSettlement, candidateAttackerParty });
        }
        catch (System.Reflection.TargetInvocationException ex)
        {
            throw new Exception("InvokeResolve threw: " + ex.InnerException, ex.InnerException);
        }
    }

    // --- 1. Creation and replication ---

    [Fact]
    public void GenuineServerCreation_CapturesTheRolledEnemyKingdomAndReplicatesAByteIdenticalIssueToEveryClient()
    {
        var fixture = SetupIssueOwner();

        CreateIssueOnServer(fixture);

        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkRaidAnEnemyTerritoryIssueCreated>());
        Assert.Equal(fixture.HeroId, created.OwnerId);
        Assert.Equal(fixture.EnemyKingdomId, created.EnemyKingdomId);

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                var mirrored = Assert.IsType<RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue>(owner.Issue);

                Assert.True(client.ObjectManager.TryGetObject<Kingdom>(fixture.EnemyKingdomId, out var enemyKingdom));
                var issueInterface = client.Resolve<IRaidAnEnemyTerritoryIssueInterface>();
                Assert.True(issueInterface.TryCaptureEnemyKingdom(mirrored, out var capturedKingdom));
                Assert.Same(enemyKingdom, capturedKingdom);
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
            Assert.True(Client.ObjectManager.TryGetObject<Kingdom>(fixture.EnemyKingdomId, out var enemyKingdom));

            var issueInterface = Client.Resolve<IRaidAnEnemyTerritoryIssueInterface>();
            var issue = issueInterface.ConstructReplicated(owner, enemyKingdom);

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero _owner) => issue,
                typeof(RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue),
                IssueBase.IssueFrequency.VeryCommon);

            Assert.False(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
            Assert.Null(owner.Issue);
        });

        Assert.Empty(Client.NetworkSentMessages.GetMessages<NetworkRaidAnEnemyTerritoryIssueCreated>());
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
                var quest = Assert.IsType<RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest>(owner.Issue.IssueQuest);
                Assert.Empty(quest._raidedVillages);

                Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("host-controller", ownerControllerId);
            });
        }
    }

    // --- 3. Bug fix: OnRaidCompleted resolves the real owner, not MobileParty.MainParty ---

    /// <summary>The decisive test: the SERVER OPERATOR's own local avatar (<c>MobileParty.MainParty</c> on the
    /// server, which is what vanilla's own <c>mapEvent.IsPlayerMapEvent</c>/<c>PlayerSide</c> checks read) is a
    /// THIRD party, genuinely uninvolved in the raid - old code would find no match and never record any
    /// progress at all. The real owner is a SEPARATE connected player who genuinely led the raid in person - the
    /// fix must record the raided village using THAT identity, ignoring the server's own MainParty entirely.</summary>
    [Fact]
    public void OnRaidCompleted_OwnerPersonallyRaided_RecordsProgressUsingTheRealOwnersIdentity_IgnoringTheServersOwnMainParty()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("owner-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);

        var (_, ownerPartyId) = RegisterConnectedPlayer("owner-controller");
        var unrelatedPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var villageId = TestEnvironment.CreateRegisteredObject<Settlement>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(unrelatedPartyId, out var unrelatedParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(villageId, out var village));

            // The server operator's own local avatar - deliberately a totally different, uninvolved party. Old
            // code's mapEvent.IsPlayerMapEvent/PlayerSide checks would have read from THIS party.
            Campaign.Current.MainParty = unrelatedParty;

            // Old code (comparing against the unrelated MainParty) would never have matched, so the village is
            // never recorded. The fix, resolving the real owner instead, must record it.
            InvokeResolve(quest, village, unrelatedParty);
            Assert.Empty(quest._raidedVillages);

            InvokeResolve(quest, village, ownerParty);

            Assert.Single(quest._raidedVillages);
            Assert.Contains(village, quest._raidedVillages);
        });
    }

    /// <summary>Discriminating in the other direction: the real owner was genuinely NOT the one who led this
    /// particular raid (a different, unrelated party did) - the fix must NOT record it, not blindly record every
    /// raid completion for whichever quest happens to be active.</summary>
    [Fact]
    public void OnRaidCompleted_OwnerNotInvolvedInThisRaid_DoesNotRecordProgress()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("owner-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);
        RegisterConnectedPlayer("owner-controller");

        var unrelatedPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var villageId = TestEnvironment.CreateRegisteredObject<Settlement>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(unrelatedPartyId, out var unrelatedParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(villageId, out var village));

            InvokeResolve(quest, village, unrelatedParty);

            Assert.Empty(quest._raidedVillages);
        });
    }

    /// <summary>Duplicate-delivery idempotency: the same settlement resolved twice (e.g. a resent broadcast)
    /// must only be recorded once.</summary>
    [Fact]
    public void OnRaidCompleted_SameVillageResolvedTwice_OnlyRecordedOnce()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("owner-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);
        var (_, ownerPartyId) = RegisterConnectedPlayer("owner-controller");
        var villageId = TestEnvironment.CreateRegisteredObject<Settlement>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(villageId, out var village));

            InvokeResolve(quest, village, ownerParty);
            InvokeResolve(quest, village, ownerParty);

            Assert.Single(quest._raidedVillages);
        });
    }

    // --- 4. Raid-progress network sync: the real owner's OWN mirror is kept correct even when they aren't the server ---

    /// <summary>The other half of the fix's discriminating power (task requirement: "proving the real owner's
    /// raids count correctly regardless of who's the server operator/local player"): the SERVER resolves the
    /// raid completion (as it always must - RaidCompletedEvent only ever fires there), but the real OWNER is a
    /// separate CLIENT, not the server. Without the network broadcast this fix adds, only the server's own local
    /// mirror would ever learn about the raided village, and the real owner's own client-side mirror (the one
    /// their own turn-in dialogue actually reads) would incorrectly still show zero progress.</summary>
    [Fact]
    public void RaidResolvedOnServer_SyncsRaidedVillageToEveryClientMirror_EvenThoughServerOperatorIsSomeoneElse()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        // Accepting once, for real, on the SERVER auto-propagates a byte-identical Quest mirror to every client
        // via the real, unmocked network round trip this harness exercises end-to-end (same behavior
        // TheConquestOfSettlementIssueTests.GenuineAccept_... already relies on) - no separate per-client accept
        // call needed.
        Server.Resolve<IControllerIdProvider>().SetControllerId("owner-controller");
        var serverQuest = AcceptOnInstance(Server, fixture.HeroId);

        var (_, ownerPartyId) = RegisterConnectedPlayer("owner-controller");
        var villageId = TestEnvironment.CreateRegisteredObject<Settlement>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(villageId, out var village));

            // Server operator's own MainParty is unset/unrelated here - the point is the server never needs to
            // be personally involved at all for the fix to work.
            InvokeResolve(serverQuest, village, ownerParty);

            Assert.Single(serverQuest._raidedVillages);
        });

        var broadcast = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkRaidAnEnemyTerritoryVillageRaided>());
        Assert.Equal(fixture.HeroId, broadcast.QuestGiverId);
        Assert.Equal(villageId, broadcast.SettlementId);

        // Every client's OWN mirror - a totally separate quest object instance from the server's - must now also
        // show the raided village, via the broadcast this fix adds. Discriminating regardless of who is/isn't
        // the server operator: neither Client nor OtherClient is "owner-controller" here, yet both converge.
        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                var clientQuest = Assert.IsType<RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest>(owner.Issue.IssueQuest);
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(villageId, out var village));

                Assert.Single(clientQuest._raidedVillages);
                Assert.Contains(village, clientQuest._raidedVillages);
            });
        }
    }

    // --- 5. Turn-in ownership gate (RaidAnEnemyTerritoryOwnershipGatePatches) ---

    /// <summary>The real owner's own turn-in must be ALLOWED through the gate (the Harmony prefix must return
    /// true, letting vanilla's real <c>MainHeroRaidedAllVillages</c> run) - the positive counterpart to
    /// <see cref="MainHeroRaidedAllVillages_NonOwnerPeer_IsBlocked_NoPayoutNoFinalize"/>. Invokes the gate's own
    /// decision function directly (<c>VillageNeedsToolsIssueOwnership.IsLocalPeerOwner</c>, exactly what
    /// <c>RaidAnEnemyTerritoryOwnershipGatePatches.Gate</c> calls - see that type's doc comment) rather than
    /// letting the full vanilla success payout run to completion: <c>ApplyQuestSuccessConsequences</c> reaches
    /// several layers of real game-content lookups (<c>Campaign.Current.Models.CharacterDevelopmentModel</c>,
    /// static <c>DefaultTraits</c> definitions, etc.) this lightweight test harness's bootstrap does not
    /// populate - a pre-existing harness/vanilla-content gap orthogonal to this fix, not something the
    /// ownership gate itself controls.</summary>
    [Fact]
    public void MainHeroRaidedAllVillages_RealOwner_GateAllowsTheOriginalThrough()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("owner-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);

        Server.Call(() =>
        {
            Assert.True(VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(quest.QuestGiver));
        });
    }

    /// <summary>The decisive turn-in test: a NON-owner peer's own mirror quest (reachable via
    /// <c>SetDialogs()</c>'s unconditional construction on every peer, per this type's own doc comment) must NOT
    /// pay out or finalize when <c>MainHeroRaidedAllVillages</c> is reached on THEIR machine - only the recorded
    /// owner's own machine may.</summary>
    [Fact]
    public void MainHeroRaidedAllVillages_NonOwnerPeer_IsBlocked_NoPayoutNoFinalize()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("owner-controller");
        AcceptOnInstance(Server, fixture.HeroId);
        RegisterConnectedPlayer("owner-controller");

        // OtherClient is not the recorded owner - its own mirror quest object (constructed via the generic
        // accept-mirror, per GenericAcceptMirrorIssueTypes) is a SEPARATE instance from the server's.
        OtherClient.Call(() =>
        {
            Assert.True(OtherClient.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            var mirrorQuest = Assert.IsType<RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest>(owner.Issue.IssueQuest);

            var goldBefore = Hero.MainHero.Gold;

            InvokePrivate(mirrorQuest, "MainHeroRaidedAllVillages");

            Assert.False(mirrorQuest.IsFinalized);
            Assert.Equal(goldBefore, Hero.MainHero.Gold);
        });
    }

    // --- 6. Accept-race arbitration (shared mechanism only) ---

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
            Assert.IsType<RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest>(owner.Issue.IssueQuest);
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
                Assert.IsType<RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest>(owner.Issue.IssueQuest);
                Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("player-A", ownerControllerId);
            });
        }
    }

    // --- 7. Finalize / cleanup ---

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
                Assert.IsType<RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest>(owner.Issue.IssueQuest);
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
}
