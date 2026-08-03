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
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

/// <summary>
/// Real, executed multi-peer coverage for Deliver the Herd to Town (source/GameInterface/Services/Issues/
/// {Interfaces,Messages,Handlers,Patches}/HeadmanNeedsToDeliverAHerd*.cs), following the exact same
/// harness/conventions established by <see cref="VillageNeedsToolsIssueTests"/>/
/// <see cref="VillageNeedsCraftingMaterialsIssueTests"/>/<see cref="TheSpyPartyIssueTests"/>. Every test drives
/// the actual production entry point (a genuine <see cref="IssueManager.CreateNewIssue"/>/
/// <see cref="IssueManager.StartIssueQuest"/> call, a real <see cref="IMessageBroker"/> publish simulating a
/// specific peer's network request, or a direct call into the real, Harmony-patched
/// <c>DeliverHerdOnConsequence</c>/<c>DeliverHerdRejectOnConsequence</c>/<c>InitializeQuestOnLoadWithQuestManager</c>
/// production methods - all public post-Publicize, same technique
/// <see cref="VillageNeedsCraftingMaterialsIssueTests"/> already uses for <c>QuestAcceptedConsequences</c>)
/// rather than re-implementing the mod's own logic and asserting it against itself.
///
/// This file's highest-value tests are <see cref="DeliveryDialogueOwnershipGate_BlocksSuccessAndRejectConsequencesForAnyoneOtherThanTheRecordedOwner"/>
/// and <see cref="PostLoadReRegistration_StillGatesTheDeliveryDialogueForANonOwningPeer"/>, covering this
/// type's single genuinely novel mechanism: a THIRD <c>DialogFlow</c> registered OUTSIDE the normal
/// <c>SetDialogs</c>/<c>AddDialogs</c> hook, from TWO separate call sites (see
/// <see cref="HeadmanNeedsToDeliverAHerdOwnershipGatePatches"/>'s doc comment) - given the same scrutiny the
/// accept-time-reward-forcing test got for Crafting Materials.
/// </summary>
public class HeadmanNeedsToDeliverAHerdIssueTests : IDisposable
{
    private static readonly PropertyInfo ItemValueProperty =
        AccessTools.Property(typeof(ItemObject), nameof(ItemObject.Value));

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();
    private EnvironmentInstance OtherClient => TestEnvironment.Clients.Last();
    private IEnumerable<EnvironmentInstance> AllInstances => new[] { Server }.Concat(TestEnvironment.Clients);

    public HeadmanNeedsToDeliverAHerdIssueTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private record HerdFixture(
        string HeroId,
        string OwnerVillageSettlementId,
        string TargetSettlementId,
        string TargetHeroId,
        string HerdTypeId);

    /// <summary>
    /// Builds the headman (issue owner, in his own bound village), the SECOND settlement/NPC pair
    /// (<c>_targetSettlement</c>/<c>_targetHero</c>) the delivery dialogue is anchored at, and the herd-type
    /// item - independently wired up on the server AND every client.
    ///
    /// The owner's village is bound to a SEPARATE, real Town (never itself - a self-referential
    /// <c>Village.Bound</c> would make <c>Settlement.MapFaction</c> (<c>Village.MapFaction =&gt; Bound.MapFaction</c>)
    /// recurse into itself the moment anything reads it, which <c>DeliverHerdRejectOnConsequence</c>'s
    /// <c>ChangeCrimeRatingAction.Apply(QuestGiver.CurrentSettlement.MapFaction, ...)</c> genuinely does), owned
    /// by a real Clan so that MapFaction resolves. The target settlement is a real Town too (needed for
    /// <c>OnCompleteWithSuccess</c>'s <c>_targetSettlement.Town.Prosperity</c>), owned by the SAME clan (so
    /// creation's own <c>!x.MapFaction.IsAtWarWith(...)</c> condition would hold too, though these tests
    /// construct the Issue directly rather than exercising that predicate).
    /// </summary>
    private HerdFixture SetupIssueOwner(int herdTypeValue = 40)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var villageId = TestEnvironment.CreateRegisteredObject<Village>();
        var ownerVillageSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var ownerTownId = TestEnvironment.CreateRegisteredObject<Town>();
        var ownerTownSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var ownerClanId = TestEnvironment.CreateRegisteredObject<Clan>();

        var targetTownId = TestEnvironment.CreateRegisteredObject<Town>();
        var targetSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var targetHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var herdTypeId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        var herdCategoryId = TestEnvironment.CreateRegisteredObject<ItemCategory>();

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<Village>(villageId, out var village));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(ownerVillageSettlementId, out var ownerVillageSettlement));
                Assert.True(instance.ObjectManager.TryGetObject<Town>(ownerTownId, out var ownerTown));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(ownerTownSettlementId, out var ownerTownSettlement));
                Assert.True(instance.ObjectManager.TryGetObject<Clan>(ownerClanId, out var ownerClan));

                Assert.True(instance.ObjectManager.TryGetObject<Town>(targetTownId, out var targetTown));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(targetSettlementId, out var targetSettlement));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(targetHeroId, out var targetHero));
                Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(herdTypeId, out var herdTypeToDeliver));
                Assert.True(instance.ObjectManager.TryGetObject<ItemCategory>(herdCategoryId, out var herdCategory));

                using (new AllowedThread())
                {
                    ownerTownSettlement.SetSettlementComponent(ownerTown);
                    ownerTown.OwnerClan = ownerClan;

                    ownerVillageSettlement.SetSettlementComponent(village);
                    village.Bound = ownerTownSettlement;
                    village.Hearth = 650f;

                    hero.Occupation = Occupation.Headman;
                    hero.StayingInSettlement = ownerVillageSettlement;

                    targetSettlement.SetSettlementComponent(targetTown);
                    targetTown.OwnerClan = ownerClan;

                    targetHero.Occupation = Occupation.RuralNotable;
                    targetSettlement.AddHeroWithoutParty(targetHero);

                    ItemValueProperty.SetValue(herdTypeToDeliver, herdTypeValue);
                    // OnCompleteWithSuccess moves the delivered herd into _targetSettlement.ItemRoster, which
                    // cascades into TownMarketData.AddNumberInStore -> GetCategoryData(item.GetItemCategory())
                    // - a Dictionary<ItemCategory, ItemData> lookup that throws ArgumentNullException on a null
                    // key. A bare test ItemObject has no ItemCategory by default (unlike the real "sheep"/"cow"/
                    // "hog" game items), so this must be set explicitly.
                    herdTypeToDeliver.ItemCategory = herdCategory;

                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();

                    // Unlike Tools/Crafting Materials/Spy Party, this quest type's own OnCompleteWithSuccess/
                    // OnTimedOut/OnFailed all call TraitLevelingHelper.OnIssueSolvedThroughQuest, which reads/
                    // writes Campaign.Current.PlayerTraitDeveloper - never initialized by this harness's
                    // lightweight GameBootStrap.Initialize() (only a real Campaign.CreateNewCampaign/LoadCampaign
                    // does that). Without this, ANY genuine success/timeout/fail consequence NREs here, unrelated
                    // to anything this mod's own patches do.
                    Campaign.Current.PlayerTraitDeveloper ??= new PropertyOwner<PropertyObject>();
                }
            });
        }

        return new HerdFixture(heroId, ownerVillageSettlementId, targetSettlementId, targetHeroId, herdTypeId);
    }

    /// <summary>
    /// Drives the real server-authoritative creation path exactly as a vetted vanilla issue-check would:
    /// <see cref="Patches.HeadmanNeedsToDeliverAHerdIssueCreationPatch"/>'s postfix lets a genuine
    /// <see cref="IssueManager.CreateNewIssue"/> call through on the server, and its postfix captures the real
    /// (here: force-set - see below) chained-roll fields and broadcasts them.
    ///
    /// The real ctor's own <c>SettlementHelper.FindRandomSettlement</c>/<c>_possibleHerdTypes.GetRandomElement()</c>/
    /// <c>Notables.GetRandomElementWithPredicate</c> rolls depend on real map-distance data
    /// (<c>Campaign.Current.Models.MapDistanceModel</c>) and real game item content
    /// (<c>Campaign.Current.ObjectManager.GetObject&lt;ItemObject&gt;("sheep"/"cow"/"hog")</c>) this harness
    /// deliberately does not stand up - same reasoning as <c>VillageNeedsCraftingMaterialsIssueTests.ForcePromisedPayment</c>'s
    /// doc comment. The three fields are force-set directly inside the <see cref="PotentialIssueData"/> factory
    /// delegate itself, before returning the issue, so <see cref="Patches.HeadmanNeedsToDeliverAHerdIssueCreationPatch"/>'s
    /// postfix (which runs synchronously as part of the same <see cref="IssueManager.CreateNewIssue"/> call)
    /// captures and broadcasts these controlled values - the actual replication contract under test, exactly
    /// as thoroughly exercised as if the real ctor had rolled them.
    /// </summary>
    private void CreateIssueOnServer(HerdFixture fixture)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.TargetSettlementId, out var targetSettlement));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.TargetHeroId, out var targetHero));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(fixture.HerdTypeId, out var herdTypeToDeliver));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) =>
                {
                    var issue = new HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue(h);
                    issue._targetSettlement = targetSettlement;
                    issue._targetHero = targetHero;
                    issue._herdTypeToDeliver = herdTypeToDeliver;
                    return issue;
                },
                typeof(HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue),
                IssueBase.IssueFrequency.VeryCommon);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
        });
    }

    // --- 1. Creation and replication (three chained rolls) ---

    [Fact]
    public void GenuineServerCreation_CapturesTheThreeChainedRollsAndReplicatesAByteIdenticalIssueToEveryClient()
    {
        var fixture = SetupIssueOwner();

        CreateIssueOnServer(fixture);

        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkHeadmanNeedsToDeliverAHerdIssueCreated>());
        Assert.Equal(fixture.HeroId, created.OwnerId);
        Assert.Equal(fixture.TargetSettlementId, created.TargetSettlementId);
        Assert.Equal(fixture.TargetHeroId, created.TargetHeroId);
        Assert.Equal(fixture.HerdTypeId, created.HerdTypeToDeliverId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            var serverIssue = Assert.IsType<HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue>(owner.Issue);
            Assert.True(Server.ObjectManager.TryGetId(serverIssue._targetSettlement, out var targetSettlementId));
            Assert.Equal(fixture.TargetSettlementId, targetSettlementId);
        });

        // Every client independently constructed its OWN HeadmanNeedsToDeliverAHerdIssue instance (never the
        // same object as the server's) with the exact same captured chained-roll terms - the actual
        // replication contract, not each client's own independent (and, for the settlement/hero pair,
        // dependent-on-each-other) re-roll.
        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                var mirrored = Assert.IsType<HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue>(owner.Issue);

                Assert.True(client.ObjectManager.TryGetObject<Settlement>(fixture.TargetSettlementId, out var targetSettlement));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.TargetHeroId, out var targetHero));
                Assert.True(client.ObjectManager.TryGetObject<ItemObject>(fixture.HerdTypeId, out var herdTypeToDeliver));

                Assert.Same(targetSettlement, mirrored._targetSettlement);
                Assert.Same(targetHero, mirrored._targetHero);
                Assert.Same(herdTypeToDeliver, mirrored._herdTypeToDeliver);
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
                (in PotentialIssueData _, Hero h) => new HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue(h),
                typeof(HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue),
                IssueBase.IssueFrequency.VeryCommon);

            // IssueManagerCreateNewIssuePatches' prefix (already fully generic) blocks a client from
            // originating a new issue.
            Assert.False(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
            Assert.Null(owner.Issue);
        });

        Assert.Empty(Client.NetworkSentMessages.GetMessages<NetworkHeadmanNeedsToDeliverAHerdIssueCreated>());
    }

    // --- 2. Ownership gate on the dynamically-registered SECOND dialogue flow (this file's highest-value
    // coverage - see the type doc comment) ---

    [Fact]
    public void DeliveryDialogueOwnershipGate_BlocksSuccessAndRejectConsequencesForAnyoneOtherThanTheRecordedOwner()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");

        HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest quest = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));

            // A genuine (unwrapped) accept: IssueQuestAcceptancePatch's real postfix on
            // IssueManager.StartIssueQuest fires and records ownership through the actual production path.
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            quest = Assert.IsType<HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest>(owner.Issue.IssueQuest);

            // QuestAcceptedConsequences is the real dialogue Consequence delegate a live "Tell me how I can
            // help." conversation would invoke - genuinely gives the herd/registers the delivery DialogFlow -
            // rather than faking that state up directly (same technique
            // VillageNeedsCraftingMaterialsIssueTests uses for its own quest's accept consequence).
            quest.QuestAcceptedConsequences();
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal("host-controller", ownerControllerId);
        });

        // --- Reject branch first (needs no items at all - the higher-risk of the two per the task): a
        // non-owner (e.g. after someone else's accept won a race, or a dedicated server with no local player)
        // must not be able to force-fail someone else's already-accepted quest for free. ---
        Server.Resolve<IControllerIdProvider>().SetControllerId("someone-else");
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));

            quest.DeliverHerdRejectOnConsequence();

            // Blocked outright: the gate's Prefix returned false, so CompleteQuestWithFail() never ran - the
            // quest is untouched.
            Assert.NotNull(owner.Issue);
            Assert.Same(quest, owner.Issue.IssueQuest);
            Assert.True(Campaign.Current.IssueManager.Issues.ContainsKey(owner));
        });

        // --- Success branch: same non-owner, this time WITH the herd genuinely in hand - proves the gate
        // isn't merely redundant with the (nonexistent, for the reject branch) item-count condition. ---
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            PartyBase.MainParty.ItemRoster.AddToCounts(quest._herdTypeToDeliver, quest._animalCountToDeliver);

            quest.DeliverHerdOnConsequence();

            Assert.NotNull(owner.Issue);
            Assert.Same(quest, owner.Issue.IssueQuest);
            Assert.True(Campaign.Current.IssueManager.Issues.ContainsKey(owner));
            // The herd is still sitting on the party - CompleteQuestWithSuccess() (which would have deducted
            // it) never ran either.
            Assert.True(PartyBase.MainParty.ItemRoster.GetItemNumber(quest._herdTypeToDeliver) >= quest._animalCountToDeliver);
        });

        // --- Restore ownership match, then prove this isn't a tautological "always blocked" stub: the
        // recorded owner's real Consequence genuinely completes the quest. ---
        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));

            // AllowedThread here is a harness concession, not a semantic claim that this is a "replay": in
            // real gameplay this Consequence fires from inside a live dialogue processed by the game's own
            // conversation loop, which this test cannot stand up. Calling it as a bare method invokes the
            // exact same vanilla CompleteQuestWithSuccess()/IssueFinalized() cascade either way - the same
            // AllowedThread scope VillageNeedsToolsIssueInterface.FinalizeMirror already needs for the
            // identical cascade (quest.CompleteQuestWithSuccess()) run outside a live conversation.
            using (new AllowedThread())
            {
                quest.DeliverHerdOnConsequence();
            }

            Assert.Null(owner.Issue);
            Assert.False(Campaign.Current.IssueManager.Issues.ContainsKey(owner));
        });
    }

    /// <summary>
    /// The task's specific highest-risk scenario: <c>InitializeQuestOnGameLoad</c> (called via the real,
    /// public <see cref="QuestBase.InitializeQuestOnLoadWithQuestManager"/> - exactly what
    /// <c>QuestManager</c> genuinely calls for EVERY active quest, owner and non-owners alike, on every
    /// campaign load/resync) re-registers the delivery DialogFlow entirely OUTSIDE of
    /// <c>QuestAcceptedConsequences</c> - so a peer that never ran the real accept consequence at all (a pure
    /// mirror, like <see cref="OtherClient"/> below) still ends up with the SAME delivery dialogue wired up
    /// locally. Proves the ownership gate (patched directly onto the Consequence methods themselves, not the
    /// registration call sites) covers this out-of-band path too, exactly as
    /// <see cref="HeadmanNeedsToDeliverAHerdOwnershipGatePatches"/>'s doc comment claims.
    /// </summary>
    [Fact]
    public void PostLoadReRegistration_StillGatesTheDeliveryDialogueForANonOwningPeer()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        // Client (player-A) genuinely accepts - the real production accept/mirror path.
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
        });

        // OtherClient never accepted at all - it only ever gets a quest object via the accept broadcast's
        // generic mirror (MirrorQuestAccepted -> a bare IssueManager.StartIssueQuest replay, which does NOT
        // run QuestAcceptedConsequences - see IHeadmanNeedsToDeliverAHerdIssueInterface's doc comment), so its
        // own copy of the delivery DialogFlow was NEVER registered by that path.
        HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest otherClientQuest = null;
        OtherClient.Call(() =>
        {
            Assert.True(OtherClient.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            otherClientQuest = Assert.IsType<HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest>(owner.Issue.IssueQuest);

            // Never recorded as owner on this peer.
            Assert.False(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var recordedOwner) &&
                Server.Resolve<IControllerIdProvider>().ControllerId == recordedOwner);

            // Simulates exactly what QuestManager does for every active quest on a real campaign
            // load/resync - the out-of-band registration path this fix must also cover.
            otherClientQuest.InitializeQuestOnLoadWithQuestManager();
        });

        // OtherClient's own local conversation reaches the now-(re)registered delivery dialogue's Consequence
        // methods directly - blocked, because OtherClient never recorded itself as the owner.
        OtherClient.Call(() =>
        {
            Assert.True(OtherClient.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            PartyBase.MainParty.ItemRoster.AddToCounts(otherClientQuest._herdTypeToDeliver, otherClientQuest._animalCountToDeliver);

            otherClientQuest.DeliverHerdOnConsequence();
            Assert.NotNull(owner.Issue);
            Assert.True(Campaign.Current.IssueManager.Issues.ContainsKey(owner));

            otherClientQuest.DeliverHerdRejectOnConsequence();
            Assert.NotNull(owner.Issue);
            Assert.True(Campaign.Current.IssueManager.Issues.ContainsKey(owner));
        });
    }

    // --- 3. Accept-race arbitration: fully generic/shared (see IHeadmanNeedsToDeliverAHerdIssueInterface's
    // doc comment) - no bespoke Deliver-the-Herd message needed, so this proves the delegation is correct
    // rather than reintroducing the accept-race rollback bug already found and fixed three times. ---

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

        // Client (player-A)'s own live conversation accepted first - tells the server. Deliver the Herd has
        // no bespoke accept message of its own - this rides the fully generic VillageNeedsToolsIssueHandler
        // dispatch (GenericAcceptMirrorIssueTypes.IsQuestSolutionMirrorEligible).
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
            Assert.IsType<HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest>(owner.Issue.IssueQuest);
            Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal("player-A", ownerControllerId);
        });

        // OtherClient (player-B) requests second, for the SAME issue - loses the race, since the server's own
        // copy is no longer IsOngoingWithoutQuest. By this point OtherClient has ALREADY mirrored player-A's
        // winning accept too (the first broadcast reached every client) - the exact race window the
        // already-fixed RejectAcceptance guard targets.
        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(OtherClient.NetPeer, new RequestVillageIssueAcceptQuest(fixture.HeroId));
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageIssueQuestAccepted>());
        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageIssueAcceptRejected>());
        Assert.Equal(fixture.HeroId, rejected.OwnerId);

        Assert.Single(OtherClient.InternalMessages.GetMessages<NetworkVillageIssueAcceptRejected>());
        Assert.Empty(Client.InternalMessages.GetMessages<NetworkVillageIssueAcceptRejected>());

        // BOTH the winner AND the loser must still carry the SAME correctly-mirrored winning quest (never
        // nulled/cancelled by the loser's own rejection handling - the bug already found and fixed three
        // times), and both record the SAME ownership.
        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.NotNull(owner.Issue);
                Assert.IsType<HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest>(owner.Issue.IssueQuest);
                Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("player-A", ownerControllerId);
            });
        }
    }

    [Fact]
    public void RequestVillageIssueAcceptQuest_FromUnregisteredRequester_IsRejectedWithoutMutatingTheIssue()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, new RequestVillageIssueAcceptQuest(fixture.HeroId));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkVillageIssueQuestAccepted>());
        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageIssueAcceptRejected>());
        Assert.Equal(fixture.HeroId, rejected.OwnerId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(owner.Issue.IsOngoingWithoutQuest);
            Assert.False(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out _));
        });
    }

    // --- 4. Finalize / cleanup (shared, generic teardown) ---

    [Fact]
    public void RequestVillageIssueRemoved_FinalizesTheRealQuestAndBroadcastsRemovalToEveryPeer()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
        });

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.NotNull(owner.Issue);
                Assert.IsType<HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest>(owner.Issue.IssueQuest);
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
