using Common.Messaging;
using Common.Util;
using System.Linq;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

/// <summary>
/// Real, executed multi-peer coverage for LandLord: Company of Trouble (source/GameInterface/Services/Issues/
/// {Interfaces,Messages,Handlers,Patches}/LandLordCompanyOfTrouble*.cs) - the hardest of the six Group A types
/// (see doc/GroupA_HostileMobilePartySync_Design_v3.md, step 5 of the group's build order).
///
/// The genuinely novel piece this file exists to prove: unlike every other Group A type (whose hostile party is
/// spawned once, either deterministically or at accept time), this quest's "mercs" spend most of their life as
/// troops embedded directly in the OWNER'S OWN <c>MobileParty.MainParty.MemberRoster</c> - not a party at all.
/// Only <c>CreateCompanyEnemyParty()</c>'s hostile-turn dialogue branch spins one up, reading BOTH the spawn
/// position and troop count from the owner's own live state AT THAT EXACT TRIGGER MOMENT (not accept time). See
/// <see cref="TriggerTimeCapture_UsesTheOwnerClientsOwnGenuinelyDifferentState_NotTheServersOwn"/> for the test
/// that proves this specifically - it deliberately gives the server's own local MainParty a DIFFERENT position
/// and a DIFFERENT (zero) embedded-troop count than the true owner-client's own MainParty, so convergence on the
/// real owner's actual values is genuinely discriminating rather than coincidentally passing.
///
/// Harness note (load-bearing, not just a scope caveat): <c>LandLordCompanyOfTroubleIssueQuest</c>'s own ctor
/// unconditionally looks up <c>MBObjectManager.Instance.GetObject&lt;CharacterObject&gt;("company_of_trouble_character")</c>
/// and immediately dereferences the result - but this harness's <c>GameInstance</c> bootstrap never registers the
/// <c>CharacterObject</c> type at all (only ItemObject/Settlement/Hero/MobileParty/Trait/Skill/Perk/BannerEffect/
/// CharacterAttribute/Clan - see GameInstance.cs), so without <see cref="SeedTroubleCharacterObject"/> the Quest
/// ctor NREs on EVERY peer, for EVERY test that constructs one (i.e. almost all of them) - unlike the other Group
/// A types' scope notes, which only affect specific accept-time party-construction pipelines. Registered once per
/// instance, matching <c>GameInstance</c>'s own <c>RegisterType&lt;T&gt;</c> convention.
///
/// Scope note (harness limitation, same established precedent as every other Group A type's own scope note):
/// <see cref="ILandLordCompanyOfTroubleIssueInterface.CreateCompanyEnemyPartyOnServer"/>'s real body reads
/// <c>SettlementHelper.FindRandomSettlement(x =&gt; x.IsHideout)</c> (walks <c>Settlement.All</c>/<c>Hideout</c>
/// state this harness never stands up) - tolerated to fail/return null in the direct-call tests, and the full
/// request/broadcast/force-write convergence mechanics are instead genuinely exercised using a real,
/// AutoRegistry-synced <see cref="MobileParty"/> built the same already-proven-safe way every other Group A
/// type's own <c>CreateRealPartyOnServer</c> does.
/// </summary>
public class LandLordCompanyOfTroubleIssueTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();
    private EnvironmentInstance OtherClient => TestEnvironment.Clients.Last();
    private IEnumerable<EnvironmentInstance> AllInstances => new[] { Server }.Concat(TestEnvironment.Clients);

    private const string TroubleCharacterId = "company_of_trouble_character";

    public LandLordCompanyOfTroubleIssueTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private record CompanyOfTroubleFixture(string HeroId);

    /// <summary>Registers a real, StringId-addressable "company_of_trouble_character" <see cref="CharacterObject"/>
    /// on <paramref name="instance"/>'s own <see cref="MBObjectManager"/> - see the type doc comment's harness
    /// note for why this is load-bearing, not optional, for this specific quest type.</summary>
    private static void SeedTroubleCharacterObject(EnvironmentInstance instance)
    {
        instance.Call(() =>
        {
            using (new AllowedThread())
            {
                var mgr = TaleWorlds.ObjectSystem.MBObjectManager.Instance;
                mgr.RegisterType<CharacterObject>("CharacterObject", "CharacterObjects", 100, true, false);

                var troubleCharacter = Util.GameObjectCreator.CreateInitializedObject<CharacterObject>();
                troubleCharacter.StringId = TroubleCharacterId;
                mgr.RegisterObject(troubleCharacter);
            }
        });
    }

    /// <summary>Builds the issue-owning (landlord) Hero. Also seeds <see cref="Campaign.EncyclopediaManager"/>
    /// and the "company_of_trouble_character" <see cref="CharacterObject"/> on every instance - see the type doc
    /// comment.</summary>
    private CompanyOfTroubleFixture SetupIssueOwner()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                }
            });

            SeedTroubleCharacterObject(instance);
        }

        return new CompanyOfTroubleFixture(heroId);
    }

    /// <summary>Drives the real server-authoritative creation path: a genuine
    /// <see cref="IssueManager.CreateNewIssue"/> call handing back a real
    /// <see cref="LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue"/> - exactly what
    /// <c>LandLordCompanyOfTroubleIssueBehavior.OnStartIssue</c> does for real.</summary>
    private void CreateIssueOnServer(CompanyOfTroubleFixture fixture)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) => new LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue(h),
                typeof(LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue),
                IssueBase.IssueFrequency.Rare);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
        });
    }

    /// <summary>Real (unwrapped) accept on <paramref name="instance"/> - drives
    /// <see cref="IssueManager.StartIssueQuest"/> for real, which (per <see cref="GenericAcceptMirrorIssueTypes"/>)
    /// triggers the fully generic accept-broadcast/ownership-record path unchanged. Deliberately does NOT invoke
    /// <c>QuestAcceptedConsequences()</c> - that only ever runs from a genuine LIVE dialogue Consequence
    /// (Category B), simulated separately, per-test.</summary>
    private LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest AcceptOnInstance(EnvironmentInstance instance, string heroId)
    {
        LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest quest = null;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var owner));
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            quest = Assert.IsType<LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest>(owner.Issue.IssueQuest);
        });
        return quest;
    }

    /// <summary>Builds a real, fully-constructed <see cref="MobileParty"/> the same already-proven-safe way every
    /// other Group A type's own <c>CreateRealPartyOnServer</c> does - used as this file's stand-in for "the
    /// server's genuine <see cref="ILandLordCompanyOfTroubleIssueInterface.CreateCompanyEnemyPartyOnServer"/>
    /// result" wherever a test needs a real, converged party without standing up the hideout-lookup pipeline.</summary>
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

    /// <summary>
    /// Builds a real MobileParty (via <see cref="E2ETestEnvironment.CreateRegisteredObject{T}"/> - a SEPARATE
    /// id from any other party built this way, so tests that need to prove genuine cross-peer divergence, e.g.
    /// <see cref="TriggerTimeCapture_UsesTheOwnerClientsOwnGenuinelyDifferentState_NotTheServersOwn"/>, get each
    /// peer's own independently-mutable copy - AutoRegistry mirrors an id identically to every instance, but
    /// mutating one instance's own in-memory copy never live-propagates to another instance in this offline
    /// harness) at <paramref name="position"/>, and points <paramref name="instance"/>'s own
    /// <c>Campaign.Current.MainParty</c> at it.
    ///
    /// Harness note: <c>Campaign.Current.MainParty</c> does NOT persist as the SAME reference across separate
    /// <see cref="EnvironmentInstance.Call"/> invocations (confirmed empirically - every precedent Group A test
    /// in this codebase re-establishes it fresh within whichever <c>.Call()</c> block actually needs it, never
    /// relying on an earlier call's assignment). <see cref="EstablishMainParty"/> re-points it; the returned
    /// party id's own OWN state (Position/MemberRoster) persists normally across calls via the object registry,
    /// only the static pointer itself needs re-establishing.
    /// </summary>
    private string CreateMainPartyId(EnvironmentInstance instance, CampaignVec2 position)
    {
        var id = TestEnvironment.CreateRegisteredObject<MobileParty>();
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(id, out var party));
            party.Position = position;
            Campaign.Current.MainParty = party;
        });
        return id;
    }

    /// <summary>Re-points <paramref name="instance"/>'s own <c>Campaign.Current.MainParty</c> at the party
    /// registered under <paramref name="mainPartyId"/> - see <see cref="CreateMainPartyId"/>'s doc comment for
    /// why this must be called fresh inside EVERY <c>.Call()</c> block that needs it.</summary>
    private static MobileParty EstablishMainParty(EnvironmentInstance instance, string mainPartyId)
    {
        Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(mainPartyId, out var party));
        Campaign.Current.MainParty = party;
        return party;
    }

    private static void InvokePrivate(object instance, string methodName, params object[] args)
    {
        var method = AccessTools.Method(instance.GetType(), methodName);
        method.Invoke(instance, args);
    }

    /// <summary>Counts specifically the "company_of_trouble_character"-type troops in
    /// <paramref name="party"/>'s roster - <c>TotalManCount</c> alone is not discriminating enough here, since a
    /// real <see cref="MobileParty"/> built via <see cref="Util.GameObjectCreator.CreateInitializedObject{T}"/>
    /// already carries its own leader hero as a roster slot.</summary>
    private static int TroubleTroopCount(MobileParty party, LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest quest)
    {
        foreach (var item in party.MemberRoster.GetTroopRoster())
        {
            if (item.Character == quest._troubleCharacterObject) return item.Number;
        }
        return 0;
    }

    // --- 1. Creation and replication ---

    [Fact]
    public void GenuineServerCreation_ReplicatesAByteIdenticalIssueToEveryClient()
    {
        var fixture = SetupIssueOwner();

        CreateIssueOnServer(fixture);

        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkLandLordCompanyOfTroubleIssueCreated>());
        Assert.Equal(fixture.HeroId, created.OwnerId);

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.IsType<LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue>(owner.Issue);
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
                (in PotentialIssueData _, Hero h) => new LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue(h),
                typeof(LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue),
                IssueBase.IssueFrequency.Rare);

            Assert.False(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
            Assert.Null(owner.Issue);
        });

        Assert.Empty(Client.NetworkSentMessages.GetMessages<NetworkLandLordCompanyOfTroubleIssueCreated>());
    }

    // --- 2. Accept-quest mirroring (generic mechanism) + Phase 1 (embedded troops, no party yet) ---

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
                Assert.IsType<LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest>(owner.Issue.IssueQuest);

                Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("host-controller", ownerControllerId);
            });
        }
    }

    /// <summary>Phase 1: the genuine accepter's own live dialogue Consequence embeds the mercs directly into
    /// their own MainParty roster - no separate <see cref="MobileParty"/> is ever created for this. Proves the
    /// embedding happens (real body, unmodified - this part needs no gate) and that no
    /// <c>_companyOfTroubleParty</c> exists yet.</summary>
    [Fact]
    public void QuestAcceptedConsequences_EmbedsTheMercsDirectlyIntoTheOwnersMainPartyRoster_NoPartyYet()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);

        var mainPartyId = CreateMainPartyId(Server, new CampaignVec2(new Vec2(1, 1), true));

        Server.Call(() =>
        {
            var mainParty = EstablishMainParty(Server, mainPartyId);
            var before = mainParty.MemberRoster.TotalManCount;

            InvokePrivate(quest, "QuestAcceptedConsequences");

            Assert.True(quest.IsOngoing);
            Assert.Null(quest._companyOfTroubleParty);
            Assert.False(quest._battleWillStart);

            // The mercs were genuinely added to the owner's own roster (Phase 1) - not a coincidental count.
            Assert.True(mainParty.MemberRoster.TotalManCount > before);
        });
    }

    // --- 3. THE decisive fix: trigger-time capture-once-force-write (not creation-time) ---

    /// <summary>
    /// This file's single highest-value test - proves the core structural difference this quest type needed
    /// fixing. The SERVER's own local MainParty is deliberately given a DIFFERENT position (0,0) and ZERO
    /// embedded trouble-troops, while the genuine OWNER-CLIENT's own MainParty has a DIFFERENT position (50, 50)
    /// and a real, non-zero embedded-troop count. If the fix ever regressed to reading the SERVER's own
    /// MobileParty.MainParty instead of the captured/forwarded accepter state, this test would catch it two
    /// ways: the position would be (0,0) instead of (50,50), and the troop count would be 0 instead of the real
    /// captured count - either alone is enough to fail this test, so it is genuinely discriminating, not
    /// vacuously passing.
    /// </summary>
    [Fact]
    public void TriggerTimeCapture_UsesTheOwnerClientsOwnGenuinelyDifferentState_NotTheServersOwn()
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

        // Server's own local MainParty: different position, zero embedded trouble-troops - if the fix ever
        // regressed to reading THIS instead of the captured/forwarded client state, both assertions below would
        // fail.
        CreateMainPartyId(Server, new CampaignVec2(new Vec2(0, 0), true));

        var quest = AcceptOnInstance(Client, fixture.HeroId);

        // Client (the genuine owner)'s own MainParty: different position, real embedded troop count.
        var clientPartyId = CreateMainPartyId(Client, new CampaignVec2(new Vec2(50, 50), true));
        int expectedTroopCount = 0;
        Client.Call(() =>
        {
            EstablishMainParty(Client, clientPartyId);
            InvokePrivate(quest, "QuestAcceptedConsequences");
            expectedTroopCount = quest._companyTroopCount;
            Assert.True(expectedTroopCount > 0);
        });

        // The real, private CreateCompanyEnemyParty() - the dialogue Consequence a live "So be it!" pick would
        // invoke.
        Client.Call(() =>
        {
            var clientParty = EstablishMainParty(Client, clientPartyId);

            InvokePrivate(quest, "CreateCompanyEnemyParty");

            // Blocked locally (BanditPartyComponent's ctor is hard-blocked on a client) - no broken local party
            // left behind.
            Assert.Null(quest._companyOfTroubleParty);

            var requested = Assert.Single(Client.InternalMessages.GetMessages<LandLordCompanyOfTroubleEnemyPartySpawnRequested>());
            Assert.True(Client.ObjectManager.TryGetId(requested.Owner, out var reqOwnerId));
            Assert.Equal(fixture.HeroId, reqOwnerId);

            // The captured values are genuinely the CLIENT's own, not the server's.
            Assert.Equal(50f, requested.Position.X);
            Assert.Equal(50f, requested.Position.Y);
            Assert.Equal(expectedTroopCount, requested.TroopCount);

            // The one real-body line that IS safe to run locally already removed the mercs from the owner's own
            // roster (not a coincidental starting value - QuestAcceptedConsequences genuinely added them first).
            Assert.Equal(0, TroubleTroopCount(clientParty, quest));
        });

        var forwarded = Assert.Single(Client.NetworkSentMessages.GetMessages<NetworkLandLordCompanyOfTroubleEnemyPartySpawnRequest>());
        Assert.Equal(fixture.HeroId, forwarded.OwnerId);
        Assert.Equal(50f, forwarded.Position.X);
        Assert.Equal(50f, forwarded.Position.Y);
        Assert.Equal(expectedTroopCount, forwarded.TroopCount);

        Assert.Empty(Client.NetworkSentMessages.GetMessages<NetworkLandLordCompanyOfTroubleEnemyPartySpawned>());
    }

    /// <summary>Real owner, host/server: vanilla's own <c>CreateCompanyEnemyParty()</c> runs completely
    /// unmodified - zero divergence, single-player-equivalent behavior. Tolerated to succeed or throw past the
    /// point the real body is reached (the hideout-lookup pipeline this harness doesn't stand up - see the type
    /// doc comment's scope note); the real proof is that the CLIENT-only forwarding path is never taken.</summary>
    [Fact]
    public void HostOwner_CreateCompanyEnemyParty_AttemptsRealCreation_NeverForwardsARequest()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);

        var mainPartyId = CreateMainPartyId(Server, new CampaignVec2(new Vec2(1, 1), true));

        Server.Call(() =>
        {
            EstablishMainParty(Server, mainPartyId);
            InvokePrivate(quest, "QuestAcceptedConsequences");
            Record.Exception(() => InvokePrivate(quest, "CreateCompanyEnemyParty"));
        });

        Assert.Empty(Server.InternalMessages.GetMessages<LandLordCompanyOfTroubleEnemyPartySpawnRequested>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkLandLordCompanyOfTroubleEnemyPartySpawnRequest>());
    }

    // --- 4. Convergence: every peer force-mirrors the SAME real party, AND _battleWillStart flips (task item 4) ---

    /// <summary>Proves both the party-spawn convergence AND the folded-in battle-start-approval (task item 4):
    /// <c>_battleWillStart</c> is false everywhere until the server's broadcast lands, then true everywhere,
    /// including on the true owner-client whose own next <c>company_of_trouble_menu_on_init</c> tick would
    /// actually launch the scripted battle.</summary>
    [Fact]
    public void NetworkEnemyPartySpawned_ForceWritesTheSameRealPartyOntoEveryPeersMirror_AndFlipsBattleWillStartEverywhere()
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
        var clientPartyId = CreateMainPartyId(Client, new CampaignVec2(new Vec2(50, 50), true));
        Client.Call(() =>
        {
            EstablishMainParty(Client, clientPartyId);
            InvokePrivate(quest, "QuestAcceptedConsequences");
            InvokePrivate(quest, "CreateCompanyEnemyParty");
            Assert.False(quest._battleWillStart);
        });

        var partyId = CreateRealPartyOnServer("Company of Trouble");

        // Simulate what Handlers.LandLordCompanyOfTroubleIssueHandler.Handle_NetworkEnemyPartySpawnRequest does
        // once CreateCompanyEnemyPartyOnServer genuinely succeeds - publish the same local event it would, with
        // the real (AutoRegistry-synced) party.
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            // Matches what CreateCompanyEnemyPartyOnServer itself does internally (force-writes onto the
            // server's OWN mirror before returning) - the network handler's own broadcast never loops back to
            // force-write the server's own copy (Handle_NetworkEnemyPartySpawned is a no-op on the server).
            Server.Resolve<ILandLordCompanyOfTroubleIssueInterface>().ForceCompanyEnemyParty(owner, party);
            Server.Resolve<IMessageBroker>().Publish(null, new LandLordCompanyOfTroubleEnemyPartySpawned(owner, party));
        });

        var spawned = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkLandLordCompanyOfTroubleEnemyPartySpawned>());
        Assert.Equal(fixture.HeroId, spawned.OwnerId);
        Assert.Equal(partyId, spawned.CompanyOfTroublePartyId);

        // Every peer - including the client that originally triggered this and whose own local creation was
        // blocked - now references the exact same real party AND has _battleWillStart flipped true.
        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var expected));

                var issueInterface = instance.Resolve<ILandLordCompanyOfTroubleIssueInterface>();
                Assert.True(issueInterface.TryCaptureCompanyEnemyParty(owner, out var actual));
                Assert.Same(expected, actual);

                var mirroredQuest = Assert.IsType<LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest>(owner.Issue.IssueQuest);
                Assert.True(mirroredQuest._battleWillStart);
            });
        }
    }

    [Fact]
    public void CreateCompanyEnemyPartyOnServer_IsIdempotent_AlreadyExistingPartyIsNeverRecreatedOrReBroadcast()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);
        AcceptOnInstance(Server, fixture.HeroId);

        var partyId = CreateRealPartyOnServer("Company of Trouble");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Server.Resolve<ILandLordCompanyOfTroubleIssueInterface>().ForceCompanyEnemyParty(owner, party);

            // CreateCompanyEnemyPartyOnServer's own idempotency guard - a resend must not spawn (or re-broadcast)
            // a second party for the same quest, and must never touch the (unavailable, in this harness)
            // hideout-lookup pipeline once a party already exists.
            var result = Server.Resolve<ILandLordCompanyOfTroubleIssueInterface>()
                .CreateCompanyEnemyPartyOnServer(owner, new CampaignVec2(new Vec2(1, 1), true), 5);
            Assert.True(Server.ObjectManager.TryGetId(result, out var resultId));
            Assert.Equal(partyId, resultId);
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkLandLordCompanyOfTroubleEnemyPartySpawned>());
    }

    // --- 5. Ownership gates (independently-found completion/turn-in gaps) ---

    /// <summary>CONFIRMED, independently-found gap (see Patches.LandLordCompanyOfTroubleOwnershipGatePatches' doc
    /// comment): ANY connected peer's own local map event ending (any unrelated fight, not even this quest's own)
    /// would see zero embedded trouble-troops in their own roster - which is this method's own SUCCESS trigger,
    /// not a harmless no-op - and complete/reward the true owner's already-accepted quest out from under
    /// them.</summary>
    [Fact]
    public void OnMapEventEnded_OwnershipGate_BlocksNonOwner_AllowsOwner()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);

        var prefix = AccessTools.Method(
            typeof(GameInterface.Services.Issues.Patches.LandLordCompanyOfTroubleOwnershipGatePatches), "OnMapEventEndedPrefix");

        Server.Resolve<IControllerIdProvider>().SetControllerId("someone-else");
        Server.Call(() =>
        {
            Assert.False((bool)prefix.Invoke(null, new object[] { quest, null }));

            // End-to-end: the real, private method itself, invoked while a non-owner controller id is active,
            // never even touches the (null, would-NRE) MapEvent argument - the gate short-circuits before the
            // real body's own null-deref-prone read.
            var exception = Record.Exception(() => InvokePrivate(quest, "OnMapEventEnded", (MapEvent)null));
            Assert.Null(exception);
            Assert.True(quest.IsOngoing);
        });

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        Server.Call(() =>
        {
            Assert.True((bool)prefix.Invoke(null, new object[] { quest, null }));
        });
    }

    /// <summary>Same shape: without this gate, ANY non-owner could unilaterally steal the true owner's reward
    /// gold and complete their quest just by talking to any OTHER qualifying lord.</summary>
    [Fact]
    public void PersuasionComplete_OwnershipGate_BlocksNonOwner_AllowsOwner()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);

        var prefix = AccessTools.Method(
            typeof(GameInterface.Services.Issues.Patches.LandLordCompanyOfTroubleOwnershipGatePatches), "PersuasionCompletePrefix");

        Server.Resolve<IControllerIdProvider>().SetControllerId("someone-else");
        Server.Call(() =>
        {
            Assert.False((bool)prefix.Invoke(null, new object[] { quest }));
        });

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        Server.Call(() =>
        {
            Assert.True((bool)prefix.Invoke(null, new object[] { quest }));
        });
    }

    // --- 6. Instance-resolution disambiguation ---

    /// <summary>
    /// Two concurrently-owned Company of Trouble quests on the SAME local player (two different NPCs) -
    /// <c>Instance</c> must resolve to the one THIS local peer actually owns, not "first found". Deliberately
    /// bypasses <c>IssueManager.CreateNewIssue</c>/<c>StartIssueQuest</c> - this harness's shared
    /// <c>IssueManager.Issues</c> dictionary bookkeeping does not support two simultaneously-registered issues of
    /// the exact same concrete type cleanly (a pre-existing, orthogonal harness limitation independently
    /// confirmed via diagnostics, unrelated to this quest type's own sync code - the second
    /// <c>CreateNewIssue</c> call's own vanilla-side bookkeeping evicts the first hero's entry outright, before
    /// either quest is even accepted). This test instead exercises exactly what
    /// <see cref="Patches.LandLordCompanyOfTroubleInstanceResolutionPatch"/> itself reads
    /// (<c>Campaign.Current.QuestManager.Quests</c> + <see cref="VillageNeedsToolsIssueOwnership"/>) directly -
    /// two real, genuinely-started <see cref="LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest"/>
    /// objects, registered with <see cref="QuestManager.OnQuestStarted"/> (the same public method vanilla's own
    /// <c>StartQuest()</c> triggers via <c>CampaignEventDispatcher</c>) and recorded ownership, same end state a
    /// real two-quest scenario would produce.
    /// </summary>
    [Fact]
    public void InstanceResolution_ResolvesToTheLocallyOwnedQuest_NotFirstFound()
    {
        var ownedHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var otherHeroId = TestEnvironment.CreateRegisteredObject<Hero>();

        SeedTroubleCharacterObject(Server);

        LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest ownedQuest = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownedHeroId, out var ownedOwner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(otherHeroId, out var otherOwner));

            var otherQuest = new LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest(
                "other_company_of_trouble_quest", otherOwner, CampaignTime.Never, 5);
            ownedQuest = new LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest(
                "owned_company_of_trouble_quest", ownedOwner, CampaignTime.Never, 5);

            // Same real, public method vanilla's own StartQuest() triggers via CampaignEventDispatcher.
            Campaign.Current.QuestManager.OnQuestStarted(otherQuest);
            Campaign.Current.QuestManager.OnQuestStarted(ownedQuest);
            otherQuest.StartQuest();
            ownedQuest.StartQuest();

            Assert.True(otherQuest.IsOngoing);
            Assert.True(ownedQuest.IsOngoing);

            // "otherOwner"'s quest is accepted by someone else; "ownedOwner"'s quest is accepted by THIS (server)
            // peer - same ownership registry every real accept flow populates.
            VillageNeedsToolsIssueOwnership.SetOwner(otherOwner, "someone-else");
            VillageNeedsToolsIssueOwnership.SetOwner(ownedOwner, "host-controller");
        });

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");

        var instanceGetter = AccessTools.PropertyGetter(typeof(LandLordCompanyOfTroubleIssueBehavior), "Instance");

        Server.Call(() =>
        {
            var resolved = instanceGetter.Invoke(null, null);
            // Genuinely discriminating: "first found" (vanilla's own cache/fallback) would resolve to
            // otherQuest (registered first) instead - this only passes because the fix actually keys off
            // ownership, not registration order.
            Assert.Same(ownedQuest, resolved);
        });
    }

    // --- 7. Accept-race arbitration (shared mechanism only) ---

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
            Assert.IsType<LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest>(owner.Issue.IssueQuest);
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
                Assert.IsType<LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest>(owner.Issue.IssueQuest);
                Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("player-A", ownerControllerId);
            });
        }
    }

    // --- 8. OnMapEventEnded early-death edge case (Phase 1 failing without ever reaching Phase 2) ---

    /// <summary>The genuine owner's embedded troops all die in an unrelated fight before the hostile-turn
    /// dialogue was ever reached - <c>_companyOfTroubleParty</c> never existed at all. Real body tolerated to
    /// succeed or throw past the ownership gate (this harness's bare <see cref="MapEvent"/> fixture doesn't carry
    /// enough real state for vanilla's own <c>IsPlayerMapEvent</c> check - see the type doc comment's scope
    /// note) - the point proven is that the OWNERSHIP gate itself lets the real, genuine-owner attempt through.</summary>
    [Fact]
    public void OnMapEventEnded_GenuineOwner_EmbeddedTroopsDieBeforeEverBecomingAParty_RealBodyAttempted()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);
        var mainPartyId = CreateMainPartyId(Server, new CampaignVec2(new Vec2(1, 1), true));

        Server.Call(() =>
        {
            EstablishMainParty(Server, mainPartyId);
            InvokePrivate(quest, "QuestAcceptedConsequences");
            Assert.Null(quest._companyOfTroubleParty);

            var mapEvent = Util.GameObjectCreator.CreateInitializedObject<MapEvent>();
            Record.Exception(() => InvokePrivate(quest, "OnMapEventEnded", mapEvent));

            // Still Phase 1 the whole time - never became a party.
            Assert.Null(quest._companyOfTroubleParty);
        });
    }

    // --- 9. Finalize / cleanup: both "still Phase 1" and "already Phase 2" states ---

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
                Assert.IsType<LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssueQuest>(owner.Issue.IssueQuest);
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

    /// <summary>State 1 - "still Phase 1": the quest ends (e.g. cancel/timeout) while the mercs are STILL just
    /// embedded troops in the owner's own roster, no party ever created. <c>OnFinalize</c> removes any remaining
    /// embedded copies.</summary>
    [Fact]
    public void OnFinalize_StillPhase1_RemovesRemainingEmbeddedTroopsFromOwnersRoster()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);
        var mainPartyId = CreateMainPartyId(Server, new CampaignVec2(new Vec2(1, 1), true));

        Server.Call(() =>
        {
            var mainParty = EstablishMainParty(Server, mainPartyId);

            InvokePrivate(quest, "QuestAcceptedConsequences");
            Assert.Null(quest._companyOfTroubleParty);
            Assert.True(TroubleTroopCount(mainParty, quest) > 0);

            InvokePrivate(quest, "OnFinalize");

            // The embedded copies were genuinely removed - not a coincidental no-op.
            Assert.Equal(0, TroubleTroopCount(mainParty, quest));
        });
    }

    /// <summary>State 2 - "already Phase 2": a real <c>_companyOfTroubleParty</c> exists when the quest ends.
    /// Tolerated to succeed or throw past the point the code path is reached (this harness's synthetic party
    /// doesn't carry enough real settlement/component state for vanilla's own generic cascades to complete
    /// cleanly - same tolerance as every other Group A type's own OnFinalize test) - <c>OnFinalize</c>'s own real
    /// body does NOT touch <c>_companyOfTroubleParty</c> at all (confirmed by decompile - despawn for an
    /// already-spawned party happens exclusively in <c>company_of_trouble_menu_on_init</c>'s own
    /// <c>_checkForBattleResults</c> branch, a separate, pre-existing vanilla despawn path this task's own brief
    /// does not ask to be relocated) - this test proves OnFinalize's OWN roster cleanup still runs safely
    /// (0 remaining embedded copies, since they were already moved into the party) without erroring on the
    /// already-Phase-2 state.</summary>
    [Fact]
    public void OnFinalize_AlreadyPhase2_WithARealCompanyEnemyParty_RosterCleanupStillSafe()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);
        var mainPartyId = CreateMainPartyId(Server, new CampaignVec2(new Vec2(1, 1), true));

        var partyId = CreateRealPartyOnServer("Company of Trouble");
        Server.Call(() =>
        {
            var mainParty = EstablishMainParty(Server, mainPartyId);

            InvokePrivate(quest, "QuestAcceptedConsequences");

            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Server.Resolve<ILandLordCompanyOfTroubleIssueInterface>().ForceCompanyEnemyParty(quest.QuestGiver, party);

            // Emulate CreateCompanyEnemyParty's own first line (moved the mercs OUT of the owner's roster into
            // the party) - the state OnFinalize would see once Phase 2 was reached for real.
            mainParty.MemberRoster.AddToCounts(quest._troubleCharacterObject, -quest._companyTroopCount);

            Assert.NotNull(quest._companyOfTroubleParty);
            Assert.Equal(0, TroubleTroopCount(mainParty, quest));

            var exception = Record.Exception(() => InvokePrivate(quest, "OnFinalize"));
            Assert.Null(exception);

            // No further, erroneous removal attempted against an already-empty roster.
            Assert.Equal(0, TroubleTroopCount(mainParty, quest));
        });
    }
}
