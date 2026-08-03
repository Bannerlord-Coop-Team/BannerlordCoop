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
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

/// <summary>
/// A minimal stub for the ONE model type this file's genuine <c>Town.GetItemPrice</c> calls need:
/// <c>TownMarketData.GetPrice</c> reads <c>Campaign.Current.Models.TradeItemPriceFactorModel</c> - the REAL
/// <c>DefaultTradeItemPriceFactorModel</c> NREs somewhere in its own supply/demand computation this lightweight
/// harness's bare test Town/ItemCategory never provides real data for (same shape as
/// <see cref="ArtisanCantSellProductsAtAFairPriceIssueTests.StubMapDistanceModel"/>'s own doc comment). This
/// stub just returns the item's own <c>Value</c> directly - deterministic, and exactly what this file's tests
/// need to manufacture genuine, controllable price divergence across peers by giving each peer's own "jewelry"
/// item a different <c>Value</c>.
/// </summary>
internal class StubTradeItemPriceFactorModel : TradeItemPriceFactorModel
{
    public override float GetTradePenalty(ItemObject item, MobileParty clientParty, PartyBase merchant, bool isSelling, float inStore, float supply, float demand) => 0f;

    public override float GetBasePriceFactor(ItemCategory itemCategory, float inStoreValue, float supply, float demand, bool isSelling, int transferValue) => 1f;

    public override int GetPrice(EquipmentElement itemRosterElement, MobileParty clientParty, PartyBase merchant, bool isSelling, float inStoreValue, float supply, float demand) =>
        itemRosterElement.Item?.Value ?? 0;

    public override int GetTheoreticalMaxItemMarketValue(ItemObject item) => item.Value;
}

/// <summary>
/// Real, executed multi-peer coverage for Gang Leader Needs to Offload Stolen Goods (source/GameInterface/
/// Services/Issues/{Interfaces,Messages,Handlers,Patches}/GangLeaderNeedsToOffloadStolenGoods*.cs), following the
/// exact same harness/conventions established by <see cref="VillageNeedsCraftingMaterialsIssueTests"/> (accept-
/// time price-capture pattern) and <see cref="HeadmanNeedsToDeliverAHerdIssueTests"/> (second-location/hideout
/// ownership-gate pattern). Every test drives the actual production entry point rather than re-implementing the
/// mod's own logic and asserting it against itself.
///
/// This file's two highest-value tests are <see cref="RemoteClientAccept_ForceCorrectsPriceAmountRewardAndCounterOfferGold_OnEveryPeer_IncludingTheAccepterItself_WhenPriceAndDifficultyDiverge"/>
/// (proving the task's flagged <c>_counterOfferGold</c> double-read bug fix, the same way Crafting Materials'
/// own divergent-multiplier test proved its force-write mechanism) and
/// <see cref="AlternativeSolutionPayoutFreeze_CapturesTheAuthoritativeAmountAndRewardOnceAndForceWritesThemOntoEveryPeer"/>
/// (proving the second flagged gap - the Issue-level alternative-solution payout had ZERO capture-once
/// protection at all before this pass).
/// </summary>
public class GangLeaderNeedsToOffloadStolenGoodsIssueTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();
    private EnvironmentInstance OtherClient => TestEnvironment.Clients.Last();
    private IEnumerable<EnvironmentInstance> AllInstances => new[] { Server }.Concat(TestEnvironment.Clients);

    public GangLeaderNeedsToOffloadStolenGoodsIssueTests(ITestOutputHelper output)
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
    // Hero.MainHero - every Succeed/Fail consequence here reaches it (directly or via
    // TraitLevelingHelper.OnIssueSolvedThroughQuest's own downstream calls), and with no "current player"
    // context ever set (this harness never runs any of the handlers that normally set it), the real
    // ChangeRelationAction.ApplyInternal NREs on a null hero. Resolved via reflection on the current thread,
    // immediately before any consequence that reaches ChangeRelationAction.ApplyPlayerRelation runs - same
    // technique/reasoning as ArtisanCantSellProductsAtAFairPriceIssueTests.
    private static readonly FieldInfo ResolvedMainHeroField =
        AccessTools.Field(Type.GetType("GameInterface.Services.Heroes.Patches.ResolvedMainHeroContext, GameInterface"), "ResolvedMainHero");

    private static void SetResolvedMainHero(Hero hero) => ResolvedMainHeroField.SetValue(null, hero);

    private static readonly PropertyInfo ItemValueProperty =
        AccessTools.Property(typeof(ItemObject), nameof(ItemObject.Value));
    private static readonly PropertyInfo PlayerProgressProperty =
        AccessTools.Property(typeof(Campaign), nameof(Campaign.PlayerProgress));
    private static readonly FieldInfo RandomForStolenTradeGoodField =
        AccessTools.Field(typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue), "_randomForStolenTradeGood");
    private static readonly PropertyInfo TradeItemPriceFactorModelProperty =
        AccessTools.Property(typeof(GameModels), nameof(GameModels.TradeItemPriceFactorModel));

    // Only the SERVER's copy of a TestEnvironment.CreateRegisteredObject<Town>() is built via the real
    // TownBuilder (new Town(), whose ctor initializes _marketData); every OTHER peer's own copy is a bare
    // placeholder object under the same shared id, with _marketData left at its default null - Town.GetItemPrice
    // (-> MarketData.GetPrice) NREs the moment any non-server peer's own real accept reaches it. Force-
    // initialized per instance below, immediately after fetching each peer's own Town object.
    private static readonly FieldInfo TownMarketDataField =
        AccessTools.Field(typeof(Town), "_marketData");

    private static void EnsureMarketData(Town town)
    {
        if (TownMarketDataField.GetValue(town) == null)
        {
            TownMarketDataField.SetValue(town, new TownMarketData(town));
        }
    }

    /// <summary>The real production <c>Game.Current.ObjectManager.GetObject&lt;ItemObject&gt;(id)</c> lookup
    /// key <c>StolenTradeGood</c> resolves - index 0 of the real <c>PossibleStolenItems</c> array
    /// (<see cref="CreateIssueOnServer"/> always force-sets <c>_randomForStolenTradeGood</c> to 0).</summary>
    private const string StolenGoodId = "jewelry";

    private record GangLeaderFixture(
        string HeroId,
        string OwnerSettlementId,
        string IssueHideoutSettlementId,
        string CounterOfferHeroId);

    /// <summary>
    /// Builds the gang leader (issue owner, in his own town), the hideout settlement (the SECOND location this
    /// quest type's own hideout-battle wiring/dialogue is anchored at), the counter-offer merchant (the ONLY
    /// notable in the owner's settlement, so <c>AfterIssueCreation</c>'s real <c>FirstOrDefault</c> pick
    /// deterministically resolves to it on every peer without needing to force it - unlike Artisan, whose
    /// analogous field is set directly in the ctor instead of via <c>AfterIssueCreation</c>), and the stolen-
    /// good item, registered under the literal string id <see cref="StolenGoodId"/> on every peer's own
    /// <see cref="GameInterface.Services.ObjectManager.IObjectManager"/> - the same technique
    /// <see cref="VillageNeedsCraftingMaterialsIssueTests.RegisterDefaultCraftingMaterialItemsOnClients"/> uses
    /// for its own string/id-resolved item. Deliberately built directly via <c>GameObjectCreator</c> rather than
    /// <see cref="E2ETestEnvironment.CreateRegisteredObject{T}"/> - that helper already registers the object
    /// under its OWN auto-generated id on the server, so a second <c>AddExisting("jewelry", ...)</c> call on
    /// the SAME object would be a genuine double-registration (this harness correctly rejects it) - the
    /// production string-id lookup needs "jewelry" to be this item's ONE AND ONLY id, on every peer
    /// independently, exactly like <c>DefaultItems.IronIngot1/2</c> already are for Crafting Materials.
    /// </summary>
    private GangLeaderFixture SetupIssueOwner(int stolenGoodValue = 100)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var ownerTownId = TestEnvironment.CreateRegisteredObject<Town>();
        var ownerSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var ownerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var ownerPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        var issueHideoutId = TestEnvironment.CreateRegisteredObject<Hideout>();
        var issueHideoutSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        var counterOfferHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var stolenGoodCategoryId = TestEnvironment.CreateRegisteredObject<ItemCategory>();

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<Town>(ownerTownId, out var ownerTown));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(ownerSettlementId, out var ownerSettlement));
                Assert.True(instance.ObjectManager.TryGetObject<Clan>(ownerClanId, out var ownerClan));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));

                Assert.True(instance.ObjectManager.TryGetObject<Hideout>(issueHideoutId, out var issueHideout));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(issueHideoutSettlementId, out var issueHideoutSettlement));

                Assert.True(instance.ObjectManager.TryGetObject<Hero>(counterOfferHeroId, out var counterOfferHero));
                Assert.True(instance.ObjectManager.TryGetObject<ItemCategory>(stolenGoodCategoryId, out var stolenGoodCategory));

                using (new AllowedThread())
                {
                    EnsureMarketData(ownerTown);

                    ownerSettlement.SetSettlementComponent(ownerTown);
                    ownerTown.OwnerClan = ownerClan;
                    hero.StayingInSettlement = ownerSettlement;
                    hero.Occupation = Occupation.GangLeader;
                    Campaign.Current.MainParty = ownerParty;

                    // HeroBuilder auto-assigns every test Hero a fresh Clan (needed elsewhere), but never gives
                    // it a Leader - DefaultDiplomacyModel.GetHeroesForEffectiveRelation redirects a relation
                    // change to hero.Clan.Leader whenever Clan != null, so ChangeRelationAction.ApplyPlayerRelation
                    // (reached by every Succeed/Fail consequence here) would NRE on a null Leader without this.
                    hero.Clan.SetLeader(hero);
                    counterOfferHero.Clan.SetLeader(counterOfferHero);

                    if (Campaign.Current.Models.TradeItemPriceFactorModel is not StubTradeItemPriceFactorModel)
                    {
                        TradeItemPriceFactorModelProperty.SetValue(Campaign.Current.Models, new StubTradeItemPriceFactorModel());
                    }

                    // The Quest ctor unconditionally calls AddGameMenuOptions() -> AddGameMenuOption("hideout_place", ...),
                    // which looks up an ALREADY-REGISTERED "hideout_place" GameMenu - normally registered by
                    // SandBox's own real menu-registration code, which this lightweight harness never runs.
                    // Registers a minimal stand-in so the real ctor doesn't KeyNotFoundException immediately.
                    if (Campaign.Current.GameMenuManager.GetGameMenu("hideout_place") == null)
                    {
                        var hideoutMenu = new GameMenu("hideout_place");
                        hideoutMenu.Initialize(new TextObject("Hideout"), null, GameMenu.MenuOverlayType.None);
                        Campaign.Current.GameMenuManager.AddGameMenu(hideoutMenu);
                    }

                    issueHideoutSettlement.SetSettlementComponent(issueHideout);

                    // The ONLY notable in the owner's settlement, and a merchant - AfterIssueCreation's real
                    // FirstOrDefault(x => x != IssueOwner && x.IsMerchant) deterministically resolves to this
                    // hero on every peer.
                    counterOfferHero.Occupation = Occupation.Merchant;
                    ownerSettlement.AddHeroWithoutParty(counterOfferHero);

                    var stolenGood = GameObjectCreator.CreateInitializedObject<ItemObject>();
                    ItemValueProperty.SetValue(stolenGood, stolenGoodValue);
                    // Town.GetItemPrice -> TownMarketData.GetPrice reads the item's own category (e.g. via a
                    // Dictionary<ItemCategory, ItemData> lookup) - a bare test ItemObject has none by default,
                    // same lesson HeadmanNeedsToDeliverAHerdIssueTests's own OnCompleteWithSuccess fixture note
                    // already documents for its own herd item.
                    stolenGood.ItemCategory = stolenGoodCategory;
                    Assert.True(instance.ObjectManager.AddExisting(StolenGoodId, stolenGood));

                    // StolenTradeGood => Game.Current.ObjectManager.GetObject<ItemObject>(id) - unlike this
                    // project's own IObjectManager (used by OUR handlers to resolve a broadcast id, see
                    // VillageNeedsCraftingMaterialsIssueTests's own doc comment for that DIFFERENT registry),
                    // this is the REAL vanilla lookup GangLeaderNeedsToOffloadStolenGoodsIssue's own production
                    // code genuinely calls, backed by MBObjectManager - a completely separate registry that
                    // needs its own direct registration.
                    stolenGood.StringId = StolenGoodId;
                    MBObjectManager.Instance.RegisterObject(stolenGood);

                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();

                    // OnCompleteWithSuccess-equivalent Succeed/Fail consequences all call TraitLevelingHelper.
                    // OnIssueSolvedThroughQuest, which reads/writes Campaign.Current.PlayerTraitDeveloper -
                    // never initialized by this harness's lightweight GameBootStrap.Initialize() - same concern
                    // HeadmanNeedsToDeliverAHerdIssueTests/ArtisanCantSellProductsAtAFairPriceIssueTests already
                    // document for their own types' equivalent consequences.
                    Campaign.Current.PlayerTraitDeveloper ??= new PropertyOwner<PropertyObject>();
                }
            });
        }

        return new GangLeaderFixture(heroId, ownerSettlementId, issueHideoutSettlementId, counterOfferHeroId);
    }

    /// <summary>
    /// Drives the real server-authoritative creation path exactly as a vetted vanilla issue-check would: the
    /// real ctor's own <c>_issueHideout</c> assignment (a plain ctor parameter, no roll) runs for real, and
    /// <c>_randomForStolenTradeGood</c> (the one genuine <c>MBRandom</c> roll in this type) is force-set to 0
    /// directly inside the <see cref="PotentialIssueData"/> factory delegate - same technique
    /// <see cref="HeadmanNeedsToDeliverAHerdIssueTests.CreateIssueOnServer"/> uses for its own chained rolls.
    /// <c>CounterOfferHero</c> is deliberately NOT forced here - <see cref="IGangLeaderNeedsToOffloadStolenGoodsIssueInterface"/>'s
    /// doc comment explains why forcing it before <c>CreateNewIssue</c>'s own bookkeeping (which calls
    /// <c>AfterIssueCreation()</c> AFTER the factory returns) would be silently overwritten - <see cref="SetupIssueOwner"/>'s
    /// single-merchant-notable fixture makes the real <c>AfterIssueCreation()</c> resolve to the correct hero
    /// deterministically on every peer without needing to force it at all here.
    /// </summary>
    private void CreateIssueOnServer(GangLeaderFixture fixture)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.IssueHideoutSettlementId, out var issueHideoutSettlement));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) =>
                {
                    var issue = new GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue(h, issueHideoutSettlement);
                    RandomForStolenTradeGoodField.SetValue(issue, 0);
                    return issue;
                },
                typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue),
                IssueBase.IssueFrequency.Common);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
        });
    }

    // --- 1. Creation and replication ---

    [Fact]
    public void GenuineServerCreation_CapturesTheRolledFieldsAndReplicatesAByteIdenticalIssueToEveryClient()
    {
        var fixture = SetupIssueOwner();

        CreateIssueOnServer(fixture);

        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkGangLeaderStolenGoodsIssueCreated>());
        Assert.Equal(fixture.HeroId, created.OwnerId);
        Assert.Equal(fixture.IssueHideoutSettlementId, created.IssueHideoutId);
        Assert.Equal(0, created.RandomForStolenTradeGood);
        Assert.Equal(fixture.CounterOfferHeroId, created.CounterOfferHeroId);

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                var mirrored = Assert.IsType<GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue>(owner.Issue);

                Assert.True(client.ObjectManager.TryGetObject<Settlement>(fixture.IssueHideoutSettlementId, out var issueHideoutSettlement));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.CounterOfferHeroId, out var counterOfferHero));

                Assert.Same(issueHideoutSettlement, mirrored._issueHideout);
                Assert.Equal(0, mirrored._randomForStolenTradeGood);
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
            Assert.True(Client.ObjectManager.TryGetObject<Settlement>(fixture.IssueHideoutSettlementId, out var issueHideoutSettlement));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) => new GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue(h, issueHideoutSettlement),
                typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue),
                IssueBase.IssueFrequency.Common);

            Assert.False(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
            Assert.Null(owner.Issue);
        });

        Assert.Empty(Client.NetworkSentMessages.GetMessages<NetworkGangLeaderStolenGoodsIssueCreated>());
    }

    // --- 2. The accept-time price/reward-forcing mechanism, INCLUDING the _counterOfferGold double-read fix
    // (this issue type's genuinely novel, task-flagged mechanic) ---

    /// <summary>
    /// This is this file's single highest-value test (mirroring how Crafting Materials' own accept-race test
    /// was its highest-value one): genuinely diverges BOTH real per-peer inputs
    /// <see cref="Interfaces.IGangLeaderNeedsToOffloadStolenGoodsIssueInterface"/>'s type doc comment identifies
    /// as the root cause - live <c>Town.GetItemPrice</c> (via each peer's own distinct <c>ItemObject.Value</c>
    /// for the SAME "jewelry" item) AND <c>IssueDifficultyMultiplier</c> (via each peer's own distinct
    /// <c>Campaign.PlayerProgress</c>, captured the instant a machine's own accept genuinely runs). A remote
    /// client (not the server) is the one who genuinely accepts, so this also proves the correction gets forced
    /// back onto the ACCEPTER'S OWN client too - and, critically, that <c>_counterOfferGold</c> (the
    /// confirmed-real double-read bug: the Quest ctor independently re-reads <c>Town.GetItemPrice</c> a SECOND
    /// time instead of deriving it from the already-frozen price/amount) converges to the SAME authoritative
    /// value everywhere too, not just <c>_stolenTradeGoodAmount</c>/<c>_stolenTradeGoodPrice</c>/<c>RewardGold</c>.
    /// </summary>
    [Fact]
    public void RemoteClientAccept_ForceCorrectsPriceAmountRewardAndCounterOfferGold_OnEveryPeer_IncludingTheAccepterItself_WhenPriceAndDifficultyDiverge()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        // Diverge the real drivers: three genuinely different IssueDifficultyMultiplier values (server high,
        // accepting client low, bystander mid) AND three genuinely different "jewelry" prices (a distinct
        // ItemObject.Value per peer, feeding the same real Town.GetItemPrice -> StolenTradeGoodAmount/
        // StolenTradeGoodPrice/RewardGold production code on three independent Campaign instances).
        Server.Call(() => PlayerProgressProperty.SetValue(Campaign.Current, 1.0f));
        Client.Call(() => PlayerProgressProperty.SetValue(Campaign.Current, 0.1f));
        OtherClient.Call(() => PlayerProgressProperty.SetValue(Campaign.Current, 0.55f));

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(StolenGoodId, out var item));
            using (new AllowedThread()) { ItemValueProperty.SetValue(item, 400); }
        });
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<ItemObject>(StolenGoodId, out var item));
            using (new AllowedThread()) { ItemValueProperty.SetValue(item, 50); }
        });
        OtherClient.Call(() =>
        {
            Assert.True(OtherClient.ObjectManager.TryGetObject<ItemObject>(StolenGoodId, out var item));
            using (new AllowedThread()) { ItemValueProperty.SetValue(item, 150); }
        });

        // The accepting client (player-A)'s own live conversation genuinely accepts - runs its OWN real
        // StartIssueQuest, baking its own (about-to-be-superseded) price/amount/reward/counterOfferGold from
        // ITS OWN multiplier/price. Captured here BEFORE the round trip below can correct it.
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
        });

        var clientTriggered = Assert.Single(Client.InternalMessages.GetMessages<GangLeaderStolenGoodsQuestAcceptTriggered>());
        Assert.True(clientTriggered.StolenTradeGoodAmount > 0);

        // The server's own replay (ReplayQuestAccepted) used ITS OWN multiplier (1.0) and price (500) - a
        // genuinely different multiplier/price must move every one of the four captured fields, not just some.
        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkGangLeaderStolenGoodsQuestAccepted>());
        Assert.Equal(fixture.HeroId, accepted.OwnerId);
        Assert.Equal("player-A", accepted.OwnerControllerId);
        Assert.NotEqual(clientTriggered.StolenTradeGoodAmount, accepted.StolenTradeGoodAmount);
        Assert.NotEqual(clientTriggered.StolenTradeGoodPrice, accepted.StolenTradeGoodPrice);
        Assert.NotEqual(clientTriggered.RewardGold, accepted.RewardGold);
        Assert.NotEqual(clientTriggered.CounterOfferGold, accepted.CounterOfferGold);

        // Every peer - the server, the never-accepted OtherClient, AND the accepter Client's OWN already-
        // existing quest - must now carry the SAME server-authoritative values, not their own locally-diverged
        // ones. Critically, _counterOfferGold is ALSO force-corrected here, not independently re-derived by any
        // peer's own real ctor call from ITS OWN live Town.GetItemPrice a second time (the confirmed-real bug).
        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                var quest = Assert.IsType<GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest>(owner.Issue.IssueQuest);
                Assert.Equal(accepted.StolenTradeGoodAmount, quest._stolenTradeGoodAmount);
                Assert.Equal(accepted.StolenTradeGoodPrice, quest._stolenTradeGoodPrice);
                Assert.Equal(accepted.RewardGold, quest.RewardGold);
                Assert.Equal(accepted.CounterOfferGold, quest._counterOfferGold);
            });
        }

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("player-A", ownerControllerId);
            });
        }
    }

    // --- 3. Accept-race arbitration (bespoke messages, since this type isn't generically mirror-eligible - see
    // the interface's doc comment) ---

    [Fact]
    public void RequestGangLeaderStolenGoodsAcceptQuest_FirstRequestWins_SecondIsRejectedAndOwnershipConvergesOnEveryPeer()
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
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, new RequestGangLeaderStolenGoodsAcceptQuest(fixture.HeroId));
        });

        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkGangLeaderStolenGoodsQuestAccepted>());
        Assert.Equal(fixture.HeroId, accepted.OwnerId);
        Assert.Equal("player-A", accepted.OwnerControllerId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkVillageIssueAcceptRejected>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.False(owner.Issue.IsOngoingWithoutQuest);
            Assert.IsType<GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest>(owner.Issue.IssueQuest);
            Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal("player-A", ownerControllerId);
        });

        // OtherClient (player-B) requests second, for the SAME issue - reuses the SHARED, generic
        // NetworkVillageIssueAcceptRejected message/handler (see Handlers.GangLeaderNeedsToOffloadStolenGoodsIssueHandler's
        // doc comment for why no bespoke reject type is needed).
        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(OtherClient.NetPeer, new RequestGangLeaderStolenGoodsAcceptQuest(fixture.HeroId));
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkGangLeaderStolenGoodsQuestAccepted>());
        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageIssueAcceptRejected>());
        Assert.Equal(fixture.HeroId, rejected.OwnerId);

        Assert.Single(OtherClient.InternalMessages.GetMessages<NetworkVillageIssueAcceptRejected>());
        Assert.Empty(Client.InternalMessages.GetMessages<NetworkVillageIssueAcceptRejected>());

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.IsType<GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest>(owner.Issue.IssueQuest);
                Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("player-A", ownerControllerId);
            });
        }
    }

    [Fact]
    public void RequestGangLeaderStolenGoodsAcceptQuest_FromUnregisteredRequester_IsRejectedWithoutMutatingTheIssue()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, new RequestGangLeaderStolenGoodsAcceptQuest(fixture.HeroId));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkGangLeaderStolenGoodsQuestAccepted>());
        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageIssueAcceptRejected>());
        Assert.Equal(fixture.HeroId, rejected.OwnerId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(owner.Issue.IsOngoingWithoutQuest);
            Assert.False(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out _));
        });
    }

    // --- 4. Ownership gate on the SECOND-LOCATION (hideout) consequences ---

    [Fact]
    public void HideoutOwnershipGate_BlocksSuccessAndFailConsequencesForAnyoneOtherThanTheRecordedOwner()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");

        GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest quest = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            quest = Assert.IsType<GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest>(owner.Issue.IssueQuest);
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal("host-controller", ownerControllerId);
        });

        // --- Non-owner: every hideout-completion method blocked outright - the quest state is untouched, and
        // (for the success paths) nothing gets granted to this non-owner's own local party. ---
        Server.Resolve<IControllerIdProvider>().SetControllerId("someone-else");
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));

            quest.FailQuestByLosingHideoutBattle();
            Assert.NotNull(owner.Issue);
            Assert.Same(quest, owner.Issue.IssueQuest);

            quest.SucceedQuestByPayingAndGivingTheGoodsBack();
            Assert.NotNull(owner.Issue);
            Assert.Same(quest, owner.Issue.IssueQuest);

            quest.FailQuestByGivingBackTheGoods();
            Assert.NotNull(owner.Issue);
            Assert.Same(quest, owner.Issue.IssueQuest);

            quest.FailQuestByKeepingTheGoods();
            Assert.NotNull(owner.Issue);
            Assert.Same(quest, owner.Issue.IssueQuest);

            quest.OnSettlementLeft(PartyBase.MainParty.MobileParty, quest.QuestGiver.CurrentSettlement);

            quest.SucceedQuestByPayingAndKeepingTheGoods();
            Assert.NotNull(owner.Issue);
            Assert.Same(quest, owner.Issue.IssueQuest);
            Assert.True(Campaign.Current.IssueManager.Issues.ContainsKey(owner));
            // The stolen goods were never granted to this non-owner's own party.
            Assert.Equal(0, PartyBase.MainParty.ItemRoster.GetItemNumber(quest._stolenTradeGood));
        });

        // --- Restore ownership match - the real owner's own success genuinely completes the quest (proves this
        // isn't a tautological "always blocked" stub). ---
        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            SetResolvedMainHero(owner);

            using (new AllowedThread())
            {
                quest.SucceedQuestByPayingAndKeepingTheGoods();
            }

            Assert.Null(owner.Issue);
            Assert.False(Campaign.Current.IssueManager.Issues.ContainsKey(owner));
            Assert.True(PartyBase.MainParty.ItemRoster.GetItemNumber(quest._stolenTradeGood) > 0);
        });
    }

    // --- 5. The alternative-solution payout freeze (the task's second flagged gap) ---

    /// <summary>
    /// Genuinely diverges the SAME two real drivers as the quest-solution test above (live
    /// <c>Town.GetItemPrice</c> via a distinct <c>ItemObject.Value</c>, and <c>IssueDifficultyMultiplier</c> via
    /// a distinct <c>Campaign.PlayerProgress</c>), but for the ALTERNATIVE-solution path: a remote client
    /// genuinely accepts the alternative solution, capturing what ITS OWN live
    /// <c>StolenTradeGoodAmount</c>/<c>RewardGold</c> Issue-level properties computed AT THAT INSTANT. Proves
    /// <see cref="GangLeaderStolenGoodsAlternativeSolutionFreeze"/> converges the SAME frozen values onto every
    /// peer (including the accepter), and that the frozen values remain stable even after the item's price is
    /// changed AGAIN afterward (simulating market drift between accept and completion) - the "capture once, not
    /// re-derived later" guarantee the task asked for.
    /// </summary>
    [Fact]
    public void AlternativeSolutionPayoutFreeze_CapturesTheAuthoritativeAmountAndRewardOnceAndForceWritesThemOntoEveryPeer()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        Server.Call(() => PlayerProgressProperty.SetValue(Campaign.Current, 1.0f));
        Client.Call(() => PlayerProgressProperty.SetValue(Campaign.Current, 0.1f));
        OtherClient.Call(() => PlayerProgressProperty.SetValue(Campaign.Current, 0.55f));

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(StolenGoodId, out var item));
            using (new AllowedThread()) { ItemValueProperty.SetValue(item, 400); }
        });
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<ItemObject>(StolenGoodId, out var item));
            using (new AllowedThread()) { ItemValueProperty.SetValue(item, 50); }
        });
        OtherClient.Call(() =>
        {
            Assert.True(OtherClient.ObjectManager.TryGetObject<ItemObject>(StolenGoodId, out var item));
            using (new AllowedThread()) { ItemValueProperty.SetValue(item, 150); }
        });

        // Client (player-A) genuinely accepts the alternative solution - the real production accept path.
        // StartIssueWithAlternativeSolution reads AlternativeSolutionHero (the companion a real player picks
        // via the party-screen troop-selection flow this harness never runs) - derived from
        // AlternativeSolutionSentTroops (an empty dummy roster by default), so a hero character must be added
        // to it first or GetAlternativeSolutionSkill's own hero.GetSkillValue delegate creation NREs on a null
        // target.
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CounterOfferHeroId, out var companion));
            using (new AllowedThread())
            {
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
            }
            owner.Issue.StartIssueWithAlternativeSolution();
        });

        var clientTriggered = Assert.Single(Client.InternalMessages.GetMessages<GangLeaderStolenGoodsAlternativeAcceptTriggered>());
        Assert.True(clientTriggered.StolenTradeGoodAmount > 0);

        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkGangLeaderStolenGoodsAlternativeAccepted>());
        Assert.Equal(fixture.HeroId, accepted.OwnerId);
        Assert.Equal("player-A", accepted.OwnerControllerId);
        Assert.Equal(clientTriggered.StolenTradeGoodAmount, accepted.StolenTradeGoodAmount);
        Assert.Equal(clientTriggered.RewardGold, accepted.RewardGold);

        // Every peer's own Issue-level StolenTradeGoodAmount/RewardGold properties (patched to consult the
        // freeze) now report the SAME frozen values - not a live re-derivation from that peer's own
        // Town.GetItemPrice/IssueDifficultyMultiplier.
        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                var issue = Assert.IsType<GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue>(owner.Issue);
                Assert.Equal(accepted.StolenTradeGoodAmount, issue.StolenTradeGoodAmount);
                Assert.Equal(accepted.RewardGold, issue.RewardGold);
                Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("player-A", ownerControllerId);
            });
        }

        // Market drift AFTER the freeze: changing the item's price again on every peer must NOT move the
        // frozen values - this is the actual "capture once, not re-derived later" guarantee under test.
        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(StolenGoodId, out var item));
                using (new AllowedThread()) { ItemValueProperty.SetValue(item, 9999); }

                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                var issue = Assert.IsType<GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue>(owner.Issue);
                Assert.Equal(accepted.StolenTradeGoodAmount, issue.StolenTradeGoodAmount);
                Assert.Equal(accepted.RewardGold, issue.RewardGold);
            });
        }
    }

    // --- 6. Finalize / cleanup (shared, generic teardown - see Handlers.GangLeaderNeedsToOffloadStolenGoodsIssueHandler's
    // doc comment) ---

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
                Assert.IsType<GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest>(owner.Issue.IssueQuest);
            });
        }

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer,
                new RequestVillageIssueRemoved(fixture.HeroId, VillageIssueFinalizeReason.QuestCancel));
        });

        var removed = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageIssueRemoved>());
        Assert.Equal(fixture.HeroId, removed.OwnerId);
        Assert.Equal(VillageIssueFinalizeReason.QuestCancel, removed.Reason);

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
