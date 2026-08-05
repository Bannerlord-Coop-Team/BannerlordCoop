using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

/// <summary>
/// A minimal stub for the ONE model type this file's real-ctor-driven creation test genuinely needs:
/// <c>ArtisanCantSellProductsAtAFairPriceIssueBehavior.SelectTargetSettlement</c> calls
/// <c>Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType</c> (worked around separately,
/// see <see cref="ArtisanCantSellProductsAtAFairPriceIssueTests.AverageDistanceBetweenClosestTwoTownsField"/>)
/// AND <c>SettlementHelper.FindNearestSettlementToMobileParty</c>'s own condition delegate, which calls
/// <c>Campaign.Current.Models.MapDistanceModel.GetDistance(...)</c> - the REAL <c>DefaultMapDistanceModel</c>
/// needs actual loaded navmesh/pathing data (via <c>PathFaceRecord</c>s) this lightweight harness's bare
/// <c>MapScene()</c> never provides, and would NRE. This stub is swapped in ONLY for the duration of these
/// tests (restored in <see cref="ArtisanCantSellProductsAtAFairPriceIssueTests.Dispose"/>) and only implements
/// the overload actually reached (a small constant distance, always "reachable") - every other abstract member
/// is unreachable from this file's tests and throws if ever hit, so a real gap here fails loudly rather than
/// silently.
/// </summary>
internal class StubMapDistanceModel : MapDistanceModel
{
    public override int RegionSwitchCostFromLandToSea => 0;
    public override int RegionSwitchCostFromSeaToLand => 0;
    public override float MaximumSpawnDistanceForCompanionsAfterDisband => 100f;

    public override float GetMaximumDistanceBetweenTwoConnectedSettlements(MobileParty.NavigationType navigationType) => 100f;

    public override float GetLandRatioOfPathBetweenSettlements(Settlement fromSettlement, Settlement toSettlement, bool isFromPort, bool isTargetingPort) => 1f;

    public override float GetDistance(MobileParty fromMobileParty, Settlement toSettlement, bool isTargetingPort, MobileParty.NavigationType customCapability, out float estimatedLandRatio)
    {
        estimatedLandRatio = 1f;
        return 10f;
    }

    public override float GetDistance(MobileParty fromMobileParty, MobileParty toMobileParty, MobileParty.NavigationType customCapability, out float landRatio)
    {
        landRatio = 1f;
        return 10f;
    }

    public override bool GetDistance(MobileParty fromMobileParty, MobileParty toMobileParty, MobileParty.NavigationType customCapability, float maxDistance, out float distance, out float landRatio)
    {
        distance = 10f;
        landRatio = 1f;
        return true;
    }

    public override float GetDistance(Settlement fromSettlement, Settlement toSettlement, bool isFromPort, bool isTargetingPort, MobileParty.NavigationType navigationCapability) => 10f;

    public override float GetDistance(Settlement fromSettlement, Settlement toSettlement, bool isFromPort, bool isTargetingPort, MobileParty.NavigationType navigationCapability, out float landRatio)
    {
        landRatio = 1f;
        return 10f;
    }

    public override float GetDistance(MobileParty fromMobileParty, in CampaignVec2 toPoint, MobileParty.NavigationType navigationType, out float landRatio)
    {
        landRatio = 1f;
        return 10f;
    }

    public override float GetDistance(Settlement fromSettlement, in CampaignVec2 toPoint, bool isFromPort, MobileParty.NavigationType navigationType) => 10f;

    public override float GetPortToGateDistanceForSettlement(Settlement settlement) => 0f;

    public override bool PathExistBetweenPoints(in CampaignVec2 fromPoint, in CampaignVec2 toPoint, MobileParty.NavigationType navigationType) => true;

    public override void RegisterDistanceCache(MobileParty.NavigationType navigationCapability, INavigationCache cacheToRegister)
    {
    }

    public override (Settlement, bool) GetClosestEntranceToFace(PathFaceRecord face, MobileParty.NavigationType navigationCapabilities) => (null, false);

    public override MBReadOnlyList<Settlement> GetNeighborsOfFortification(Town town, MobileParty.NavigationType navigationCapabilities) => new MBList<Settlement>();

    public override float GetTransitionCostAdjustment(Settlement settlement1, bool isFromPort, Settlement settlement2, bool isTargetingPort, bool fromIsCurrentlyAtSea, bool toIsCurrentlyAtSea) => 0f;
}

/// <summary>
/// Real, executed multi-peer coverage for Artisan Can't Sell Products At A Fair Price (source/GameInterface/
/// Services/Issues/{Interfaces,Messages,Handlers,Patches}/ArtisanCantSellProductsAtAFairPrice*.cs), following the
/// exact same harness/conventions established by <see cref="HeadmanNeedsToDeliverAHerdIssueTests"/>/
/// <see cref="VillageNeedsCraftingMaterialsIssueTests"/>. Every test drives the actual production entry point
/// (a genuine <see cref="IssueManager.CreateNewIssue"/>/<see cref="IssueManager.StartIssueQuest"/> call, a real
/// <see cref="IMessageBroker"/> publish simulating a specific peer's network request, or a direct call into the
/// real, Harmony-patched Consequence/event-listener production methods - all public post-Publicize) rather than
/// re-implementing the mod's own logic and asserting it against itself.
///
/// This file's highest-value tests are <see cref="AmbushCounterOfferOwnershipGate_BlocksBeforeGameMenuOpenedAndBothCounterOfferConsequencesForANonOwner"/>
/// and <see cref="PostLoadReRegistration_StillGatesBothDialoguesForANonOwningPeer"/>, covering this type's most
/// novel mechanic: a genuinely UNGATED-BY-LOCATION third <c>DialogFlow</c> (<c>GetCounterOfferDialogFlow</c>)
/// PLUS a proactive <c>BeforeGameMenuOpenedEvent</c> listener that force-opens it - the task's "ambush counter-
/// offer" - registered from the same two call sites (<c>QuestAcceptedConsequences</c>/<c>InitializeQuestOnGameLoad</c>)
/// Deliver the Herd's own second dialogue uses, given the identical scrutiny.
/// </summary>
public class ArtisanCantSellProductsAtAFairPriceIssueTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();
    private EnvironmentInstance OtherClient => TestEnvironment.Clients.Last();
    private IEnumerable<EnvironmentInstance> AllInstances => new[] { Server }.Concat(TestEnvironment.Clients);

    public ArtisanCantSellProductsAtAFairPriceIssueTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    // ChangeRelationActionPatches.ApplyPlayerRelationPrefix (GameInterface's own coop-specific patch) routes
    // ChangeRelationAction.ApplyPlayerRelation through ResolvedMainHeroContext.ResolvedMainHero (a [ThreadStatic]
    // field, internal to GameInterface and not InternalsVisibleTo this test project) instead of vanilla's own
    // Hero.MainHero - OnCompleteWithSuccess's own merchant-relation-penalty loop reaches this, and with no
    // "current player" context ever set (this harness never runs any of the handlers that normally set it),
    // the real ChangeRelationAction.ApplyInternal NREs on a null hero. Resolved via reflection on the current
    // thread, immediately before any consequence that reaches ChangeRelationAction.ApplyPlayerRelation runs.
    private static readonly FieldInfo ResolvedMainHeroField =
        AccessTools.Field(Type.GetType("GameInterface.Services.Heroes.Patches.ResolvedMainHeroContext, GameInterface"), "ResolvedMainHero");

    private static void SetResolvedMainHero(Hero hero) => ResolvedMainHeroField.SetValue(null, hero);

    private static readonly FieldInfo AverageDistanceBetweenClosestTwoTownsField =
        AccessTools.Field(typeof(Campaign), "_averageDistanceBetweenClosestTwoTowns");
    private static readonly PropertyInfo MapDistanceModelProperty =
        AccessTools.Property(typeof(GameModels), nameof(GameModels.MapDistanceModel));
    private static readonly PropertyInfo CampaignObjectManagerSettlementsProperty =
        AccessTools.Property(typeof(CampaignObjectManager), nameof(CampaignObjectManager.Settlements));

    private record ArtisanFixture(
        string HeroId,
        string OwnerSettlementId,
        string TargetSettlementId,
        string TargetHeroId,
        string RawMaterialsId,
        string CounterOfferHeroId);

    /// <summary>
    /// Builds the artisan (issue owner, in his own town - bound to a real Town owned by a real Clan so
    /// <c>RefuseCounterOfferConsequences</c>'s <c>ChangeCrimeRatingAction.Apply(QuestGiver.CurrentSettlement.MapFaction,
    /// ...)</c> genuinely resolves, the same fixture shape <see cref="HeadmanNeedsToDeliverAHerdIssueTests"/>'s
    /// own <c>DeliverHerdRejectOnConsequence</c> coverage needed), the SECOND settlement/NPC pair
    /// (<c>_targetSettlement</c>/<c>_targetHero</c>) the delivery dialogue is anchored at, the raw-materials
    /// item, and the counter-offer merchant.
    /// </summary>
    private ArtisanFixture SetupIssueOwner()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var ownerTownId = TestEnvironment.CreateRegisteredObject<Town>();
        var ownerSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var ownerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var ownerPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        var targetTownId = TestEnvironment.CreateRegisteredObject<Town>();
        var targetSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var targetHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var rawMaterialsId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        var counterOfferHeroId = TestEnvironment.CreateRegisteredObject<Hero>();

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<Town>(ownerTownId, out var ownerTown));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(ownerSettlementId, out var ownerSettlement));
                Assert.True(instance.ObjectManager.TryGetObject<Clan>(ownerClanId, out var ownerClan));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));

                Assert.True(instance.ObjectManager.TryGetObject<Town>(targetTownId, out var targetTown));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(targetSettlementId, out var targetSettlement));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(targetHeroId, out var targetHero));
                Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(rawMaterialsId, out var rawMaterials));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(counterOfferHeroId, out var counterOfferHero));

                using (new AllowedThread())
                {
                    // SelectTargetSettlement's own !issueSettlement.OwnerClan.HasNavalNavigationCapability check
                    // reads Clan.DefaultPartyTemplate.ShipHulls.Count (-> Culture.DefaultPartyTemplate) - NREs
                    // twice over on a bare Clan: first with no Culture set at all (see GameObjectCreator.
                    // CreateInitializedObject's own precedent for that, E2E.Tests.Util.ObjectBuilders.
                    // MobilePartyBuilder), then again because CultureObject.DefaultPartyTemplate/PartyTemplateObject.ShipHulls
                    // are both only ever populated by real XML Deserialize, never by any parameterless ctor.
                    ownerClan.Culture = GameObjectCreator.CreateInitializedObject<CultureObject>();
                    ownerClan.Culture.DefaultPartyTemplate = new PartyTemplateObject { ShipHulls = new MBList<ShipTemplateStack>() };

                    ownerSettlement.SetSettlementComponent(ownerTown);
                    ownerTown.OwnerClan = ownerClan;
                    hero.StayingInSettlement = ownerSettlement;
                    Campaign.Current.MainParty = ownerParty;

                    // See StubMapDistanceModel's own doc comment - the real DefaultMapDistanceModel needs
                    // navmesh/pathing data this harness's bare MapScene() never provides.
                    if (Campaign.Current.Models.MapDistanceModel is not StubMapDistanceModel)
                    {
                        MapDistanceModelProperty.SetValue(Campaign.Current.Models, new StubMapDistanceModel());
                    }

                    targetSettlement.SetSettlementComponent(targetTown);
                    targetTown.OwnerClan = ownerClan;
                    targetHero.Occupation = Occupation.RuralNotable;
                    targetSettlement.AddHeroWithoutParty(targetHero);

                    // SelectCounterOfferHero requires a settlement Notable who IsMerchant (Occupation.Merchant),
                    // not just any notable - RuralNotable (used for _targetHero, which doesn't need this)
                    // wouldn't satisfy it.
                    counterOfferHero.Occupation = Occupation.Merchant;
                    ownerSettlement.AddHeroWithoutParty(counterOfferHero);

                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();

                    // OnCompleteWithSuccess/OnFailed/QuestFailedWithRefusal all call TraitLevelingHelper.
                    // OnIssueSolvedThroughQuest/OnIssueFailed, which read/write Campaign.Current.PlayerTraitDeveloper -
                    // never initialized by this harness's lightweight GameBootStrap.Initialize() (only a real
                    // Campaign.CreateNewCampaign/LoadCampaign does that) - same concern
                    // HeadmanNeedsToDeliverAHerdIssueTests.SetupIssueOwner already documents for its own type's
                    // equivalent consequences.
                    Campaign.Current.PlayerTraitDeveloper ??= new PropertyOwner<PropertyObject>();

                    // SelectTargetSettlement's own SelectTargetSettlement -> Campaign.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType
                    // indexes a Dictionary that's only ever populated by a real campaign tick this lightweight
                    // harness never runs - without this, the real ctor's own creation-time roll would NRE
                    // before CreateIssueOnServer's factory delegate ever gets a chance to force-write the
                    // captured fields. Harmless, arbitrary finite values - only their non-null-ness matters,
                    // since the settlement/hero fields are force-set immediately afterward anyway.
                    if (AverageDistanceBetweenClosestTwoTownsField.GetValue(Campaign.Current) == null)
                    {
                        AverageDistanceBetweenClosestTwoTownsField.SetValue(Campaign.Current, new Dictionary<MobileParty.NavigationType, float>
                        {
                            [MobileParty.NavigationType.Default] = 100f,
                            [MobileParty.NavigationType.Naval] = 100f,
                            [MobileParty.NavigationType.All] = 100f,
                        });
                    }

                    // SettlementHelper.FindNearestSettlementToMobileParty only ever considers a candidate whose
                    // real (StubMapDistanceModel-derived) distance is < Campaign.MapDiagonal - a static field
                    // only ever populated by a real campaign map generation this harness never runs, defaulting
                    // to 0f and therefore silently excluding every candidate (no throw - SelectTargetSettlement
                    // just gracefully returns null, which the ctor's very next line then NREs on).
                    if (Campaign.MapDiagonal <= 0f)
                    {
                        Campaign.MapDiagonal = 1000f;
                        Campaign.MapDiagonalSquared = 1000f * 1000f;
                    }

                    // Settlement.All reads Campaign.Current.CampaignObjectManager.Settlements - a cached field
                    // only ever populated by a real campaign map generation this harness never runs (defaults
                    // to null, which SettlementHelper.FindNearestSettlementToMobileParty's own `foreach
                    // (Settlement item in Settlement.All)` would otherwise iterate as empty forever, silently
                    // excluding every candidate this test registers). NOT backed by MBObjectManager.Instance.
                    // GetObjectTypeList<Settlement>() - this project's own TestEnvironment.CreateRegisteredObject
                    // registers with GameInterface's OWN IObjectManager (the coop network-sync id<->object
                    // registry), a completely separate mechanism from vanilla's MBObjectManager, so that query
                    // always comes back empty here. Populated directly instead, with exactly the two
                    // settlements this fixture builds.
                    CampaignObjectManagerSettlementsProperty.SetValue(
                        Campaign.Current.CampaignObjectManager, new MBList<Settlement> { ownerSettlement, targetSettlement });
                }
            });
        }

        return new ArtisanFixture(heroId, ownerSettlementId, targetSettlementId, targetHeroId, rawMaterialsId, counterOfferHeroId);
    }

    /// <summary>
    /// Drives the real server-authoritative creation path exactly as a vetted vanilla issue-check would: the
    /// real ctor's own <c>SelectTargetSettlement</c>/<c>GetRandomElementWithPredicate</c>/<c>GetRandomElement</c>/
    /// <c>SelectCounterOfferHero</c> rolls run for real here (unlike Deliver the Herd's equivalent, whose
    /// underlying search has no distance dependency) - <see cref="SetupIssueOwner"/> stands up just enough fake
    /// map-distance infrastructure (<see cref="StubMapDistanceModel"/>, <c>Campaign.MapDiagonal</c>,
    /// <c>_averageDistanceBetweenClosestTwoTowns</c>, <c>CampaignObjectManager.Settlements</c>) for
    /// <c>SelectTargetSettlement</c>'s real search to genuinely succeed rather than crash or silently return
    /// null. The four rolled/derived fields are then force-set directly inside the
    /// <see cref="PotentialIssueData"/> factory delegate regardless (same technique
    /// <see cref="HeadmanNeedsToDeliverAHerdIssueTests.CreateIssueOnServer"/> uses for its own three chained
    /// rolls) - this test's fixture-built settlement/hero are not necessarily what the real search would have
    /// picked on its own, but forcing them afterward is exactly the real production
    /// <see cref="Interfaces.ArtisanCantSellProductsAtAFairPriceIssueInterface.ConstructReplicated"/> contract
    /// under test, not a shortcut around it.
    /// </summary>
    private void CreateIssueOnServer(ArtisanFixture fixture)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.TargetSettlementId, out var targetSettlement));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.TargetHeroId, out var targetHero));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(fixture.RawMaterialsId, out var rawMaterials));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CounterOfferHeroId, out var counterOfferHero));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) =>
                {
                    var issue = new ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue(h);
                    issue._targetSettlement = targetSettlement;
                    issue._targetHero = targetHero;
                    issue._rawMaterialsToBeDelivered = rawMaterials;
                    issue.CounterOfferHero = counterOfferHero;
                    return issue;
                },
                typeof(ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue),
                IssueBase.IssueFrequency.Common);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
        });
    }

    // --- 1. Creation and replication (four independent rolls) ---

    [Fact]
    public void GenuineServerCreation_CapturesTheFourRolledFieldsAndReplicatesAByteIdenticalIssueToEveryClient()
    {
        var fixture = SetupIssueOwner();

        CreateIssueOnServer(fixture);

        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkArtisanCantSellProductsAtAFairPriceIssueCreated>());
        Assert.Equal(fixture.HeroId, created.OwnerId);
        Assert.Equal(fixture.TargetSettlementId, created.TargetSettlementId);
        Assert.Equal(fixture.TargetHeroId, created.TargetHeroId);
        Assert.Equal(fixture.RawMaterialsId, created.RawMaterialsToBeDeliveredId);
        Assert.Equal(fixture.CounterOfferHeroId, created.CounterOfferHeroId);

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                var mirrored = Assert.IsType<ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue>(owner.Issue);

                Assert.True(client.ObjectManager.TryGetObject<Settlement>(fixture.TargetSettlementId, out var targetSettlement));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.TargetHeroId, out var targetHero));
                Assert.True(client.ObjectManager.TryGetObject<ItemObject>(fixture.RawMaterialsId, out var rawMaterials));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.CounterOfferHeroId, out var counterOfferHero));

                Assert.Same(targetSettlement, mirrored._targetSettlement);
                Assert.Same(targetHero, mirrored._targetHero);
                Assert.Same(rawMaterials, mirrored._rawMaterialsToBeDelivered);
                Assert.Same(counterOfferHero, mirrored.CounterOfferHero);
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
                (in PotentialIssueData _, Hero h) => new ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue(h),
                typeof(ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue),
                IssueBase.IssueFrequency.Common);

            Assert.False(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
            Assert.Null(owner.Issue);
        });

        Assert.Empty(Client.NetworkSentMessages.GetMessages<NetworkArtisanCantSellProductsAtAFairPriceIssueCreated>());
    }

    // --- 2. Lord Solution disabled (unbuilt infrastructure - same gap as Lady's Knight Out) ---

    [Fact]
    public void LordSolutionIsDisabled_IsThereLordSolutionAlwaysReportsFalse()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            var issue = Assert.IsType<ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue>(owner.Issue);

            // The real vanilla type's own field says true - the disable patch overrides the getter regardless.
            Assert.False(issue.IsThereLordSolution);
        });
    }

    // --- 3. Ownership gate on the SECOND-LOCATION delivery dialogue ---

    [Fact]
    public void DeliveryDialogueOwnershipGate_BlocksConsequencesForAnyoneOtherThanTheRecordedOwner()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");

        ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest quest = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            quest = Assert.IsType<ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest>(owner.Issue.IssueQuest);

            // QuestAcceptedConsequences is the real dialogue Consequence delegate a live "How can I help?"
            // conversation would invoke - registers both dynamically-added dialogues and seeds the journal log
            // DeliverItemsFullyOnConsequence needs, rather than faking that state up directly.
            quest.QuestAcceptedConsequences();
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal("host-controller", ownerControllerId);
        });

        // --- Non-owner: all three delivery consequences blocked outright - the quest/party state is untouched. ---
        Server.Resolve<IControllerIdProvider>().SetControllerId("someone-else");
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));

            quest.DeliverItemsRejectOnConsequence();
            Assert.NotNull(owner.Issue);
            Assert.Same(quest, owner.Issue.IssueQuest);

            quest.DeliverItemsPartiallyOnConsequence();
            Assert.Equal(0, quest._deliveredRawGoods);

            PartyBase.MainParty.ItemRoster.AddToCounts(quest._rawMaterialsToBeDelivered, quest._amountOfRawGoodsToBeDelivered);
            quest.DeliverItemsFullyOnConsequence();
            Assert.NotNull(owner.Issue);
            Assert.Same(quest, owner.Issue.IssueQuest);
            Assert.True(Campaign.Current.IssueManager.Issues.ContainsKey(owner));
            // The materials are still sitting on the party - CompleteQuestWithSuccess() never ran.
            Assert.True(PartyBase.MainParty.ItemRoster.GetItemNumber(quest._rawMaterialsToBeDelivered) >= quest._amountOfRawGoodsToBeDelivered);
        });

        // --- Restore ownership match - the real owner's own delivery genuinely completes the quest. ---
        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            SetResolvedMainHero(owner);

            using (new AllowedThread())
            {
                quest.DeliverItemsFullyOnConsequence();
            }

            Assert.Null(owner.Issue);
            Assert.False(Campaign.Current.IssueManager.Issues.ContainsKey(owner));
        });
    }

    // --- 4. Ownership gate on the "ambush counter-offer" (no location gate at all in vanilla - see the type
    // doc comment) ---

    [Fact]
    public void AmbushCounterOfferOwnershipGate_BlocksBeforeGameMenuOpenedAndBothCounterOfferConsequencesForANonOwner()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");

        ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest quest = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            quest = Assert.IsType<ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest>(owner.Issue.IssueQuest);
            quest.QuestAcceptedConsequences();
        });

        // --- Non-owner: BeforeGameMenuOpened never even reaches its own body (which would otherwise force-open
        // a conversation purely from a menu opening, with no ownership concept at all) - _counterOfferGiven
        // stays false, proving the gate's Prefix returned false before anything ran. ---
        Server.Resolve<IControllerIdProvider>().SetControllerId("someone-else");
        Server.Call(() =>
        {
            quest.BeforeGameMenuOpened(null);
            Assert.False(quest._counterOfferGiven);

            quest.QuestFailedWithRefusal();
            Assert.True(Campaign.Current.IssueManager.Issues.ContainsKey(quest.QuestGiver));

            quest.RefuseCounterOfferConsequences();
            Assert.False(quest._counterOfferRefused);
        });

        // --- Restore ownership match - the real owner's own refusal genuinely applies (proves this isn't a
        // tautological "always blocked" stub). ---
        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        Server.Call(() =>
        {
            quest.RefuseCounterOfferConsequences();
            Assert.True(quest._counterOfferRefused);
        });
    }

    /// <summary>
    /// The task's specific highest-risk scenario, mirroring <see cref="HeadmanNeedsToDeliverAHerdIssueTests.PostLoadReRegistration_StillGatesTheDeliveryDialogueForANonOwningPeer"/>:
    /// <c>InitializeQuestOnGameLoad</c> (called via the real, public <see cref="QuestBase.InitializeQuestOnLoadWithQuestManager"/> -
    /// exactly what <c>QuestManager</c> genuinely calls for EVERY active quest, owner and non-owners alike, on
    /// every campaign load/resync) re-registers BOTH dynamically-added DialogFlows entirely OUTSIDE
    /// <c>QuestAcceptedConsequences</c> - so a peer that never ran the real accept consequence at all (a pure
    /// mirror, like <see cref="OtherClient"/> below) still ends up with both flows wired up locally. Proves the
    /// ownership gate (patched directly onto the Consequence methods/event listener themselves, not the
    /// registration call sites) covers this out-of-band path too.
    /// </summary>
    [Fact]
    public void PostLoadReRegistration_StillGatesBothDialoguesForANonOwningPeer()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
        });

        // OtherClient never accepted at all - it only ever gets a quest object via the accept broadcast's
        // generic mirror (a bare IssueManager.StartIssueQuest replay, which does NOT run QuestAcceptedConsequences),
        // so its own copy of both dialogues was NEVER registered by that path.
        ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest otherClientQuest = null;
        OtherClient.Call(() =>
        {
            Assert.True(OtherClient.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            otherClientQuest = Assert.IsType<ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest>(owner.Issue.IssueQuest);

            Assert.False(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var recordedOwner) &&
                Server.Resolve<IControllerIdProvider>().ControllerId == recordedOwner);

            // Simulates exactly what QuestManager does for every active quest on a real campaign load/resync.
            otherClientQuest.InitializeQuestOnLoadWithQuestManager();
        });

        OtherClient.Call(() =>
        {
            otherClientQuest.BeforeGameMenuOpened(null);
            Assert.False(otherClientQuest._counterOfferGiven);

            otherClientQuest.DeliverItemsRejectOnConsequence();
            Assert.NotNull(otherClientQuest.QuestGiver.Issue);
            Assert.True(Campaign.Current.IssueManager.Issues.ContainsKey(otherClientQuest.QuestGiver));

            otherClientQuest.RefuseCounterOfferConsequences();
            Assert.False(otherClientQuest._counterOfferRefused);
        });
    }

    // --- 5. Accept-race arbitration: fully generic/shared - no bespoke Artisan message needed ---

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
            Assert.IsType<ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest>(owner.Issue.IssueQuest);
            Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal("player-A", ownerControllerId);
        });

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(OtherClient.NetPeer, new RequestVillageIssueAcceptQuest(fixture.HeroId));
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageIssueQuestAccepted>());
        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageIssueAcceptRejected>());
        Assert.Equal(fixture.HeroId, rejected.OwnerId);

        Assert.Single(OtherClient.InternalMessages.GetMessages<NetworkVillageIssueAcceptRejected>());
        Assert.Empty(Client.InternalMessages.GetMessages<NetworkVillageIssueAcceptRejected>());

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.IsType<ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest>(owner.Issue.IssueQuest);
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

    // --- 6. Finalize / cleanup (shared, generic teardown) ---

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
                Assert.IsType<ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest>(owner.Issue.IssueQuest);
            });
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            SetResolvedMainHero(owner);

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
