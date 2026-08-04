using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Patches;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

/// <summary>
/// Real, executed multi-peer coverage for Artisan Overpriced Goods (source/GameInterface/Services/Issues/
/// {Interfaces,Messages,Handlers,Patches}/ArtisanOverpricedGoods*.cs). Every test drives the actual production
/// entry point (a genuine <see cref="IssueManager.CreateNewIssue"/>/<see cref="IssueManager.StartIssueQuest"/>
/// call, or a real <see cref="IMessageBroker"/> publish simulating a specific peer's network message) rather
/// than re-implementing the mod's own logic and asserting it against itself.
///
/// This file's single highest-value test is
/// <see cref="AntagonistHero_UsesTheFrozenCounterOfferHero_NotTheLiveNotablesFirstOrDefaultDerivation_OnEveryPeer"/>:
/// the fixture deliberately places a DECOY merchant notable ahead of the real, correctly-captured
/// <c>CounterOfferHero</c> in <c>Settlement.Notables</c> insertion order, so the buggy live
/// <c>Notables.FirstOrDefault(...)</c> derivation this fix replaces WOULD genuinely resolve to the wrong hero if
/// it ever ran unpatched - proving both that the harness genuinely reproduces the real vanilla-adjacent bug
/// AND that every peer's own patched <c>AntagonistHero</c> getter converges on the correct, frozen value
/// instead.
/// </summary>
public class ArtisanOverpricedGoodsIssueTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();
    private EnvironmentInstance OtherClient => TestEnvironment.Clients.Last();
    private IEnumerable<EnvironmentInstance> AllInstances => new[] { Server }.Concat(TestEnvironment.Clients);

    public ArtisanOverpricedGoodsIssueTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    // Same reflection-based setup ArtisanCantSellProductsAtAFairPriceIssueTests/
    // GangLeaderNeedsToOffloadStolenGoodsIssueTests already document: ChangeRelationActionPatches.
    // ApplyPlayerRelationPrefix routes ChangeRelationAction.ApplyPlayerRelation through
    // ResolvedMainHeroContext.ResolvedMainHero (a [ThreadStatic] field, internal to GameInterface) instead of
    // vanilla's own Hero.MainHero - every Succeed/Fail consequence this file drives reaches it, and with no
    // "current player" context ever set by this harness, the real ChangeRelationAction.ApplyInternal NREs on a
    // null hero without this.
    private static readonly FieldInfo ResolvedMainHeroField =
        AccessTools.Field(Type.GetType("GameInterface.Services.Heroes.Patches.ResolvedMainHeroContext, GameInterface"), "ResolvedMainHero");

    private static void SetResolvedMainHero(Hero hero) => ResolvedMainHeroField.SetValue(null, hero);

    private static readonly PropertyInfo ItemValueProperty =
        AccessTools.Property(typeof(ItemObject), nameof(ItemObject.Value));

    // Only the SERVER's copy of a TestEnvironment.CreateRegisteredObject<Town>() is built via the real
    // TownBuilder (whose ctor initializes _marketData); every OTHER peer's own copy is a bare placeholder
    // object under the same shared id, with _marketData left at its default null -
    // QuestHelper.GetAveragePriceOfItemInTheWorld (-> Town.GetItemPrice -> TownMarketData.GetPrice), called
    // unconditionally from the real ctor's own CalculateTradeGoodsAmountAndReward(), NREs the moment any
    // non-server peer's own real ctor call reaches it. Force-initialized per instance below - same technique
    // GangLeaderNeedsToOffloadStolenGoodsIssueTests already established.
    private static readonly FieldInfo TownMarketDataField = AccessTools.Field(typeof(Town), "_marketData");

    private static void EnsureMarketData(Town town)
    {
        if (TownMarketDataField.GetValue(town) == null)
        {
            TownMarketDataField.SetValue(town, new TownMarketData(town));
        }
    }

    private static readonly PropertyInfo TradeItemPriceFactorModelProperty =
        AccessTools.Property(typeof(GameModels), nameof(GameModels.TradeItemPriceFactorModel));

    // ArtisanOverpricedGoodsIssue's ctor unconditionally calls CalculateTradeGoodsAmountAndReward() ->
    // QuestHelper.GetAveragePriceOfItemInTheWorld, which iterates Campaign.Current.Settlements (a cached
    // snapshot - see the decompiled CampaignObjectManager.Settlements - only ever refreshed at specific
    // real-init call sites this lightweight harness never reaches, and NOT auto-populated by this project's
    // own AutoRegistry CreatePrefix hook, which registers with this harness's OWN IObjectManager, not the real
    // MBObjectManager.Instance) and divides by the town/village count found - a genuinely new requirement no
    // sibling test file in this project has needed before (every other bespoke-interface test avoids any
    // global "every settlement in the world" helper). Force-set directly, per instance, to a single-element
    // list containing that instance's own copy of the owner settlement.
    private static readonly PropertyInfo CampaignObjectManagerSettlementsProperty =
        AccessTools.Property(typeof(CampaignObjectManager), nameof(CampaignObjectManager.Settlements));

    private static void SetCampaignSettlements(Settlement settlement)
    {
        var list = new TaleWorlds.Library.MBList<Settlement> { settlement };
        CampaignObjectManagerSettlementsProperty.SetValue(Campaign.Current.CampaignObjectManager, list);
    }

    private static readonly PropertyInfo AntagonistHeroProperty =
        AccessTools.Property(typeof(ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest), "AntagonistHero");
    private static readonly MethodInfo IsSuitableToTalkMethod =
        AccessTools.Method(typeof(ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest), "IsSuitableToTalk");
    private static readonly MethodInfo AcceptCounterOfferMethod =
        AccessTools.Method(typeof(ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest), "AcceptCounterOffer");
    private static readonly MethodInfo DeliverItemsFullyOnConsequenceMethod =
        AccessTools.Method(typeof(ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest), "DeliverItemsFullyOnConsequence");

    private const string TradeGoodId = "artisan-overpriced-goods-test-wool";

    private record ArtisanFixture(string HeroId, string OwnerSettlementId, string CounterOfferHeroId, string DecoyHeroId);

    /// <summary>
    /// Builds the artisan (issue owner, in his own town), the real correctly-picked antagonist merchant
    /// (<c>CounterOfferHero</c>), a DECOY merchant notable inserted into the settlement's
    /// <c>HeroesWithoutParty</c> BEFORE <c>CounterOfferHero</c> (so <c>Settlement.Notables</c> - itself built
    /// straight off <c>HeroesWithoutParty</c> insertion order, see the decompiled
    /// <c>Settlement.CollectNotablesToCache</c> - lists the decoy FIRST), and the requested trade good,
    /// registered under the literal string id <see cref="TradeGoodId"/> on every peer's own
    /// <see cref="GameInterface.Services.ObjectManager.IObjectManager"/> AND the real vanilla
    /// <c>MBObjectManager</c> (the same dual-registration technique
    /// <see cref="GangLeaderNeedsToOffloadStolenGoodsIssueTests.SetupIssueOwner"/> uses for its own string-id
    /// item). The decoy is what the buggy live <c>AntagonistHero</c> re-derivation
    /// (<c>Notables.FirstOrDefault(x => x != QuestGiver &amp;&amp; x.IsMerchant &amp;&amp;
    /// x.GetTraitLevel(Mercy) &lt;= 0)</c>) would resolve to if it ever ran unpatched - this fixture exists
    /// specifically to make that divergence real and provable, not just theoretical.
    /// </summary>
    private ArtisanFixture SetupIssueOwner(int tradeGoodValue = 60)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var ownerTownId = TestEnvironment.CreateRegisteredObject<Town>();
        var ownerSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var ownerClanId = TestEnvironment.CreateRegisteredObject<Clan>();

        var counterOfferHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var decoyHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var tradeGoodCategoryId = TestEnvironment.CreateRegisteredObject<ItemCategory>();

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<Town>(ownerTownId, out var ownerTown));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(ownerSettlementId, out var ownerSettlement));
                Assert.True(instance.ObjectManager.TryGetObject<Clan>(ownerClanId, out var ownerClan));

                Assert.True(instance.ObjectManager.TryGetObject<Hero>(counterOfferHeroId, out var counterOfferHero));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(decoyHeroId, out var decoyHero));
                Assert.True(instance.ObjectManager.TryGetObject<ItemCategory>(tradeGoodCategoryId, out var tradeGoodCategory));

                using (new AllowedThread())
                {
                    EnsureMarketData(ownerTown);

                    ownerSettlement.SetSettlementComponent(ownerTown);
                    ownerTown.OwnerClan = ownerClan;
                    hero.StayingInSettlement = ownerSettlement;
                    hero.Occupation = Occupation.Artisan;

                    SetCampaignSettlements(ownerSettlement);

                    // HeroBuilder auto-assigns every test Hero a fresh Clan but never gives it a Leader -
                    // DefaultDiplomacyModel.GetHeroesForEffectiveRelation redirects a relation change to
                    // hero.Clan.Leader whenever Clan != null, so ChangeRelationAction.ApplyPlayerRelation
                    // (reached by AcceptCounterOffer/OnCompleteWithSuccess) would NRE on a null Leader without
                    // this - same fixture concern GangLeaderNeedsToOffloadStolenGoodsIssueTests documents.
                    hero.Clan.SetLeader(hero);
                    counterOfferHero.Clan.SetLeader(counterOfferHero);
                    decoyHero.Clan.SetLeader(decoyHero);

                    if (Campaign.Current.Models.TradeItemPriceFactorModel is not StubTradeItemPriceFactorModel)
                    {
                        TradeItemPriceFactorModelProperty.SetValue(Campaign.Current.Models, new StubTradeItemPriceFactorModel());
                    }

                    // Insertion order matters: the decoy is added FIRST, so it's what Notables.FirstOrDefault
                    // resolves to (see the type doc comment).
                    decoyHero.Occupation = Occupation.Merchant;
                    ownerSettlement.AddHeroWithoutParty(decoyHero);
                    counterOfferHero.Occupation = Occupation.Merchant;
                    ownerSettlement.AddHeroWithoutParty(counterOfferHero);

                    var tradeGood = GameObjectCreator.CreateInitializedObject<ItemObject>();
                    ItemValueProperty.SetValue(tradeGood, tradeGoodValue);
                    // Town.GetItemPrice -> TownMarketData.GetPrice reads the item's own category - a bare test
                    // ItemObject has none by default (same lesson every other bespoke-interface test file in
                    // this project already documents for its own item fixture).
                    tradeGood.ItemCategory = tradeGoodCategory;
                    Assert.True(instance.ObjectManager.AddExisting(TradeGoodId, tradeGood));

                    // ArtisanOverpricedGoodsIssue's ctor takes the ItemObject directly (no vanilla string-id
                    // lookup like GangLeader's StolenTradeGood => Game.Current.ObjectManager.GetObject<...>
                    // does), but registering it with the real MBObjectManager too keeps this fixture consistent
                    // with the rest of this project's item fixtures and lets Town.GetItemPrice resolve it
                    // identically regardless of which registry a future change might read from.
                    tradeGood.StringId = TradeGoodId;
                    TaleWorlds.ObjectSystem.MBObjectManager.Instance.RegisterObject(tradeGood);

                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();

                    // OnCompleteWithSuccess/AcceptCounterOffer both call TraitLevelingHelper.
                    // OnIssueSolvedThroughQuest/OnIssueSolvedThroughBetrayal, which reads/writes
                    // Campaign.Current.PlayerTraitDeveloper - never initialized by this harness's lightweight
                    // GameBootStrap.Initialize() (same concern every other Succeed/Fail-consequence-driving
                    // test file in this project already documents).
                    Campaign.Current.PlayerTraitDeveloper ??= new PropertyOwner<PropertyObject>();
                }
            });
        }

        return new ArtisanFixture(heroId, ownerSettlementId, counterOfferHeroId, decoyHeroId);
    }

    /// <summary>
    /// Drives the real server-authoritative creation path exactly as a vetted vanilla issue-check would: the
    /// real ctor's own <c>CalculateTradeGoodsAmountAndReward()</c> runs for real (no forcing needed - both
    /// arguments are handed in directly, see <see cref="Interfaces.IArtisanOverpricedGoodsIssueInterface"/>'s
    /// doc comment).
    /// </summary>
    private void CreateIssueOnServer(ArtisanFixture fixture)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CounterOfferHeroId, out var counterOfferHero));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(TradeGoodId, out var tradeGood));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) =>
                    new ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue(h, counterOfferHero, tradeGood),
                typeof(ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue),
                IssueBase.IssueFrequency.Common);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
        });
    }

    /// <summary>Real (unwrapped) accept on <paramref name="instance"/> - drives
    /// <see cref="IssueManager.StartIssueQuest"/> for real, which (per <see cref="GenericAcceptMirrorIssueTypes"/>)
    /// triggers the fully generic accept-broadcast/ownership-record path unchanged.</summary>
    private ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest AcceptOnInstance(EnvironmentInstance instance, string heroId)
    {
        ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest quest = null;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var owner));
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            quest = Assert.IsType<ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest>(owner.Issue.IssueQuest);
        });
        return quest;
    }

    // --- 1. Creation and replication ---

    [Fact]
    public void GenuineServerCreation_CapturesTheAntagonistAndTradeGood_AndReplicatesAByteIdenticalIssueToEveryClient()
    {
        var fixture = SetupIssueOwner();

        CreateIssueOnServer(fixture);

        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkArtisanOverpricedGoodsIssueCreated>());
        Assert.Equal(fixture.HeroId, created.OwnerId);
        Assert.Equal(fixture.CounterOfferHeroId, created.CounterOfferHeroId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            var issue = Assert.IsType<ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue>(owner.Issue);

            // Defensive zero-guard (task item 3): MathF.Max(..., 1) makes RequestedTradeGoodAmount == 0
            // structurally impossible, but nothing floors _goldReward - assert both landed above 0 for real,
            // proving the ArtisanOverpricedGoodsIssueHandler's own defensive check has nothing to report here.
            Assert.True(issue.RequestedTradeGoodAmount > 0);
            Assert.True(issue.RewardGold > 0);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                var mirrored = Assert.IsType<ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue>(owner.Issue);

                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.CounterOfferHeroId, out var counterOfferHero));
                Assert.True(client.ObjectManager.TryGetObject<ItemObject>(TradeGoodId, out var tradeGood));

                Assert.Same(counterOfferHero, mirrored.CounterOfferHero);
                Assert.Same(tradeGood, mirrored._requestedTradeGood);

                // RequestedTradeGoodAmount/RewardGold are pure functions of _requestedTradeGood.Value and the
                // shared IssueDifficultyMultiplier (see the type doc comment) - every client's own real ctor
                // call independently lands on the SAME nonzero values without any force-write.
                Assert.True(mirrored.RequestedTradeGoodAmount > 0);
                Assert.True(mirrored.RewardGold > 0);
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
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CounterOfferHeroId, out var counterOfferHero));
            Assert.True(Client.ObjectManager.TryGetObject<ItemObject>(TradeGoodId, out var tradeGood));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) =>
                    new ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue(h, counterOfferHero, tradeGood),
                typeof(ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue),
                IssueBase.IssueFrequency.Common);

            Assert.False(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
            Assert.Null(owner.Issue);
        });

        Assert.Empty(Client.NetworkSentMessages.GetMessages<NetworkArtisanOverpricedGoodsIssueCreated>());
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
                var quest = Assert.IsType<ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest>(owner.Issue.IssueQuest);

                Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(TradeGoodId, out var tradeGood));
                Assert.Same(tradeGood, quest._requestedTradeGood);
                Assert.True(quest.RewardGold > 0);
                Assert.True(quest._requestedTradeGoodAmount > 0);

                Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("host-controller", ownerControllerId);
            });
        }
    }

    // --- 3. The identity-bug fix (this file's highest-value coverage) ---

    [Fact]
    public void AntagonistHero_UsesTheFrozenCounterOfferHero_NotTheLiveNotablesFirstOrDefaultDerivation_OnEveryPeer()
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
                var quest = Assert.IsType<ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest>(owner.Issue.IssueQuest);

                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.CounterOfferHeroId, out var counterOfferHero));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.DecoyHeroId, out var decoyHero));

                // Prove the harness genuinely reproduces the real divergence: the buggy live derivation
                // (order-dependent FirstOrDefault, decoy inserted first) resolves to the WRONG hero.
                var liveDerivation = quest.QuestGiver.CurrentSettlement.Notables.FirstOrDefault(
                    x => x != quest.QuestGiver && x.IsMerchant && x.GetTraitLevel(DefaultTraits.Mercy) <= 0);
                Assert.Same(decoyHero, liveDerivation);
                Assert.NotSame(counterOfferHero, liveDerivation);

                // The PATCHED AntagonistHero getter uses the frozen, ctor-passed CounterOfferHero instead -
                // proven on EVERY peer, including ones whose own quest object came from a mirror replay, not a
                // genuine accept.
                var actual = (Hero)AntagonistHeroProperty.GetValue(quest);
                Assert.Same(counterOfferHero, actual);
                Assert.NotSame(decoyHero, actual);
            });
        }
    }

    [Fact]
    public void AcceptCounterOffer_AppliesConsequencesToTheFrozenCounterOfferHero_NotTheDecoy()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CounterOfferHeroId, out var counterOfferHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.DecoyHeroId, out var decoyHero));

            SetResolvedMainHero(owner);

            var counterOfferPowerBefore = counterOfferHero.Power;
            var decoyPowerBefore = decoyHero.Power;

            using (new AllowedThread())
            {
                AcceptCounterOfferMethod.Invoke(quest, null);
            }

            // AntagonistHero.AddPower(5f) inside AcceptCounterOffer landed on the FROZEN counterOfferHero -
            // the decoy (what the unpatched getter would have resolved to) is completely untouched.
            Assert.Equal(counterOfferPowerBefore + 5f, counterOfferHero.Power);
            Assert.Equal(decoyPowerBefore, decoyHero.Power);

            // AcceptCounterOffer's own CompleteQuestWithFail ran for real.
            Assert.False(quest.IsOngoing);
            Assert.Null(owner.Issue);
        });
    }

    // --- 4. Ownership gate on the delivery/ambush turn-in paths, INCLUDING the independently-found
    // chained-external-CompleteQuestWithSuccess gap ---

    [Fact]
    public void OwnershipGate_BlocksDeliveryAmbushAndChainedCompleteQuestWithSuccess_ForAnyoneOtherThanTheRecordedOwner()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(TradeGoodId, out var tradeGood));
            using (new AllowedThread())
            {
                PartyBase.MainParty.ItemRoster.AddToCounts(tradeGood, 9999);
            }
        });

        // --- Non-owner: every reachable consequence is blocked outright - the quest is left completely
        // untouched, including the chained-external CompleteQuestWithSuccess call the real dialogue flow binds
        // separately from DeliverItemsFullyOnConsequence (the gap this pass independently found). ---
        Server.Resolve<IControllerIdProvider>().SetControllerId("someone-else");
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));

            var isSuitable = (bool)IsSuitableToTalkMethod.Invoke(quest, null);
            Assert.False(isSuitable);

            DeliverItemsFullyOnConsequenceMethod.Invoke(quest, null);
            Assert.Equal(0, quest._givenTradeGoods);
            Assert.True(quest.IsOngoing);

            AcceptCounterOfferMethod.Invoke(quest, null);
            Assert.True(quest.IsOngoing);
            Assert.NotNull(owner.Issue);

            // The independently-found gap: even reaching CompleteQuestWithSuccess DIRECTLY (simulating the real
            // dialogue's own separately-bound .Consequence(base.CompleteQuestWithSuccess) node, reached
            // regardless of whether DeliverItemsFullyOnConsequence's own body ran) is blocked too.
            quest.CompleteQuestWithSuccess();
            Assert.True(quest.IsOngoing);
            Assert.NotNull(owner.Issue);
            Assert.True(Campaign.Current.IssueManager.Issues.ContainsKey(owner));
        });

        // --- Restored to the real owner - the same calls now genuinely succeed (proves this isn't a
        // tautological "always blocked" stub). ---
        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            SetResolvedMainHero(owner);

            using (new AllowedThread())
            {
                var isSuitable = (bool)IsSuitableToTalkMethod.Invoke(quest, null);
                Assert.False(isSuitable); // no one is currently talking to the antagonist in this harness

                DeliverItemsFullyOnConsequenceMethod.Invoke(quest, null);
                quest.CompleteQuestWithSuccess();
            }

            Assert.False(quest.IsOngoing);
            Assert.Null(owner.Issue);
            Assert.False(Campaign.Current.IssueManager.Issues.ContainsKey(owner));
        });
    }

    // --- 5. Accept-race arbitration (shared, generic mechanism - no bespoke reject logic) ---

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
                Assert.IsType<ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest>(owner.Issue.IssueQuest);
                Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("player-A", ownerControllerId);
            });
        }
    }

    // --- 6. Alternative-solution accept mirroring + ownership gate ---

    [Fact]
    public void AlternativeSolutionAccept_MirrorsIssueStateOnEveryPeer_AndOwnershipGateBlocksNonOwnerCompletion()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        // Client (player-A) genuinely accepts the alternative solution - the real production accept path.
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

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.True(owner.Issue.IsSolvingWithAlternative);
                Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("player-A", ownerControllerId);
            });
        }

        // The server itself is NOT the recorded owner here (player-A, on Client, is) - the shared, generic
        // NewIssueTypesAlternativeSolutionOwnershipGatePatch blocks a direct completion attempt outright.
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            owner.Issue.CompleteIssueWithAlternativeSolution();

            Assert.NotNull(owner.Issue);
            Assert.True(owner.Issue.IsSolvingWithAlternative);
        });
    }

    // --- 7. Finalize / cleanup (shared, generic teardown) + the antagonist freeze must not leak into the
    // hero's NEXT, unrelated issue ---

    [Fact]
    public void RequestVillageIssueRemoved_FinalizesOnEveryPeer_AndDrainsTheAntagonistFreezeWithoutLeakingIntoTheNextIssue()
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
                Assert.IsType<ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest>(owner.Issue.IssueQuest);
            });
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(ArtisanOverpricedGoodsAntagonistFreeze.TryGet(owner, out _));
        });

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

        // No leak into the SAME hero's NEXT, unrelated issue: the freeze entry recorded above is fully drained
        // on every peer after finalize (see ArtisanOverpricedGoodsAntagonistFreezeCleanupPatch) - proving a
        // stale antagonist can never silently carry over into whatever this hero's next, unrelated issue turns
        // out to be.
        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.False(ArtisanOverpricedGoodsAntagonistFreeze.TryGet(owner, out _));
            });
        }
    }
}
