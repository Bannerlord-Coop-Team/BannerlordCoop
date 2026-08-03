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
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

/// <summary>
/// Real, executed multi-peer coverage for Village Needs Crafting Materials (source/GameInterface/Services/
/// Issues/{Interfaces,Messages,Handlers,Patches}/VillageNeedsCraftingMaterials*.cs), following the exact same
/// harness/conventions established by <see cref="VillageNeedsToolsIssueTests"/>. Every test drives the actual
/// production entry point (a genuine <see cref="IssueManager.CreateNewIssue"/>/<see cref="IssueManager.StartIssueQuest"/>
/// call, or a real <see cref="IMessageBroker"/> publish simulating a specific peer's network request) rather
/// than re-implementing the mod's own logic and asserting it against itself.
///
/// Unlike Tools, this issue type re-derives its required-item-count/reward at ACCEPT time from
/// <c>IssueDifficultyMultiplier</c> (see <see cref="IVillageNeedsCraftingMaterialsIssueInterface"/>'s type doc
/// comment) - <see cref="RemoteClientAccept_ForceCorrectsQuantityAndRewardOnEveryPeer_IncludingTheAccepterItself_WhenIssueDifficultyMultiplierDiverges"/>
/// is this file's highest-value test, mirroring how Tools' accept-race test was its highest-value one.
/// </summary>
public class VillageNeedsCraftingMaterialsIssueTests : IDisposable
{
    private static readonly PropertyInfo PlayerProgressProperty =
        AccessTools.Property(typeof(Campaign), nameof(Campaign.PlayerProgress));

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();
    private EnvironmentInstance OtherClient => TestEnvironment.Clients.Last();
    private IEnumerable<EnvironmentInstance> AllInstances => new[] { Server }.Concat(TestEnvironment.Clients);

    public VillageNeedsCraftingMaterialsIssueTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private record CraftingFixture(string HeroId, string SettlementId);

    /// <summary>
    /// This issue type's constructor never takes an arbitrary requested item - it always rolls one of the two
    /// DefaultItems iron ingot variants via <c>SelectCraftingMaterial()</c>. Every CLIENT needs BOTH variants
    /// already resolvable under this GameInterface's own <see cref="GameInterface.Services.ObjectManager.IObjectManager"/>
    /// BEFORE the creation broadcast arrives, so <see cref="Handlers.VillageNeedsCraftingMaterialsIssueHandler"/>'s
    /// <c>Handle_NetworkVillageCraftingIssueCreated</c> can resolve whichever one the server rolled and hand it
    /// to <see cref="Interfaces.IVillageNeedsCraftingMaterialsIssueInterface.ConstructReplicated"/>, which
    /// force-overwrites the client's own independently-rolled (and therefore possibly different) field to
    /// match it - so it does not matter here which concrete <c>DefaultItems.IronIngot1/2</c> reference a given
    /// client resolves for itself; whichever one it registers becomes the client's own canonical replicated
    /// object regardless. The SERVER side is registered separately, reactively, in
    /// <see cref="CreateIssueOnServer"/> - see that method's doc comment for why a matching speculative
    /// pre-registration on the server is NOT safe to rely on.
    /// </summary>
    private void RegisterDefaultCraftingMaterialItemsOnClients()
    {
        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.AddExisting(DefaultItems.IronIngot1.StringId, DefaultItems.IronIngot1));
                Assert.True(client.ObjectManager.AddExisting(DefaultItems.IronIngot2.StringId, DefaultItems.IronIngot2));
            });
        }
    }

    /// <summary>
    /// Builds the issue-owning Hero. Unlike Tools, this issue type never freezes a Village/Hearth-gated
    /// payment at creation time - <c>GetPayment()</c> is only ever invoked live, at ACCEPT time, and (when
    /// <see cref="ForcePromisedPayment"/> hasn't short-circuited it - see that method's doc comment) walks
    /// <c>base.IssueSettlement.Village.Bound.Town.MarketData</c> plus a <c>Settlement.All</c> world-average
    /// scan, none of which this harness needs to stand up for the scenarios below.
    ///
    /// Also seeds <see cref="Campaign.EncyclopediaManager"/> on every instance: unlike Tools'
    /// <c>SetDialogs()</c> (which only ever builds deferred <c>DialogFlow</c> lines), this quest's own
    /// <c>SetDialogs()</c> eagerly builds two lines referencing <c>Hero.MainHero.CharacterObject</c> right in
    /// the constructor, which - genuinely, in vanilla - resolves <c>Hero.EncyclopediaLink</c>, which reads
    /// <c>Campaign.Current.EncyclopediaManager</c>'s <c>_pages</c> dictionary. This harness never runs the
    /// real campaign bootstrap that populates it (<c>Campaign.CreateManagers()</c> + a later
    /// <c>CreateEncyclopediaPages()</c> call), so any real accept would NRE there (a null dictionary, not a
    /// null manager) without both of these.
    ///
    /// Also gives the Hero a bare <c>CurrentSettlement</c> (via <c>StayingInSettlement</c>, same technique as
    /// Tools' fixture, but without a Village/Town component - none of these tests need one): the "player
    /// already has enough" branch of <c>GetRequiredItemCountOnPlayer()</c> genuinely reads
    /// <c>QuestGiver.CurrentSettlement.Name</c> for a quick-information popup, which would otherwise NRE the
    /// moment a test satisfies that exact condition.
    /// </summary>
    private CraftingFixture SetupIssueOwner()
    {
        RegisterDefaultCraftingMaterialItemsOnClients();

        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

                using (new AllowedThread())
                {
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();

                    hero.StayingInSettlement = settlement;
                }
            });
        }

        return new CraftingFixture(heroId, settlementId);
    }

    /// <summary>
    /// Force-writes <c>_promisedPayment</c> (a plain, non-readonly field - see
    /// <see cref="Interfaces.IVillageNeedsCraftingMaterialsIssueInterface"/>'s type doc comment) on
    /// <paramref name="instance"/>'s own copy of the issue, so its <c>GetPayment()</c> short-circuits
    /// (<c>if (_promisedPayment != 0) return _promisedPayment;</c>) instead of walking
    /// <c>IssueSettlement.Village.Bound.Town.MarketData</c>/<c>QuestHelper.GetAveragePriceOfItemInTheWorld</c> -
    /// real vanilla economy plumbing (a bound Town with live MarketData, plus at least one town/village
    /// counted by <c>Settlement.All</c>) this test file deliberately does not stand up, the same way Tools'
    /// own tests pin <c>Village.Hearth</c> to sidestep an unrelated branch rather than building out full
    /// VillageType data. <c>_numberOfRequestedItem</c> is NOT touched by this shortcut - it is still derived
    /// for real from <c>IssueDifficultyMultiplier</c> on every call, so a per-instance divergent
    /// <c>Campaign.PlayerProgress</c> (see <see cref="RemoteClientAccept_ForceCorrectsQuantityAndRewardOnEveryPeer_IncludingTheAccepterItself_WhenIssueDifficultyMultiplierDiverges"/>)
    /// still genuinely diverges that field; giving each instance its own distinct <paramref name="payment"/>
    /// value is this test file's stand-in for the genuinely-divergent-market-data half of the same real
    /// mechanism, so the force-write correction can still be proven for <c>RewardGold</c> too.
    /// </summary>
    private void ForcePromisedPayment(EnvironmentInstance instance, string ownerId, int payment)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(ownerId, out var owner));
            var issue = Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue>(owner.Issue);

            using (new AllowedThread())
            {
                issue._promisedPayment = payment;
            }
        });
    }

    /// <summary>Same <paramref name="payment"/> forced onto every peer's own copy of the issue - used by every
    /// test below that doesn't itself care about reward divergence.</summary>
    private void ForcePromisedPaymentEverywhere(string ownerId, int payment = 500)
    {
        foreach (var instance in AllInstances)
        {
            ForcePromisedPayment(instance, ownerId, payment);
        }
    }

    /// <summary>
    /// Drives the real server-authoritative creation path exactly as a vetted vanilla issue-check would:
    /// <see cref="Patches.VillageNeedsCraftingMaterialsIssueCreationPatch"/>'s postfix lets a genuine
    /// <see cref="IssueManager.CreateNewIssue"/> call through on the server, captures the real rolled
    /// requested-item field, and broadcasts it.
    ///
    /// Registers the server's own rolled <c>_requestedItem</c> with this GameInterface's own object manager
    /// REACTIVELY, INSIDE the <see cref="PotentialIssueData"/> factory delegate itself (immediately after
    /// constructing the issue, before returning it), rather than speculatively pre-registering
    /// <c>DefaultItems.IronIngot1/2</c> beforehand: <c>SelectCraftingMaterial()</c>'s roll always lands on
    /// SOME iron ingot variant, but this harness's static-swap-per-<see cref="EnvironmentInstance.Call"/>
    /// mechanics do not guarantee <c>DefaultItems.IronIngot1/2</c>, read speculatively ahead of time, is the
    /// SAME reference the roll actually produces - reading the item directly off the real, just-constructed
    /// issue instead sidesteps that harness quirk entirely and is unconditionally correct. This MUST happen
    /// inside the factory delegate, not after <see cref="IssueManager.CreateNewIssue"/> returns: its own
    /// Harmony postfix (<see cref="Patches.VillageNeedsCraftingMaterialsIssueCreationPatch"/>) - which
    /// synchronously triggers the broadcast this test asserts on - runs as part of the very same call, before
    /// control returns here.
    /// </summary>
    private void CreateIssueOnServer(string ownerId)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerId, out var owner));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) =>
                {
                    var issue = new VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue(h);
                    if (!Server.ObjectManager.Contains(issue._requestedItem))
                    {
                        Assert.True(Server.ObjectManager.AddExisting(issue._requestedItem.StringId, issue._requestedItem));
                    }
                    return issue;
                },
                typeof(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue),
                IssueBase.IssueFrequency.Rare);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
        });
    }

    // --- 1. Creation and replication ---

    [Fact]
    public void GenuineServerCreation_CapturesTheRolledRequestedItemAndReplicatesAByteIdenticalIssueToEveryClient()
    {
        var fixture = SetupIssueOwner();

        CreateIssueOnServer(fixture.HeroId);

        // The server captured the real rolled requested item off its own live issue and broadcast it.
        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageCraftingIssueCreated>());
        Assert.Equal(fixture.HeroId, created.OwnerId);
        Assert.True(created.RequestedItemId is "ironIngot1" or "ironIngot2",
            $"Expected one of the two real SelectCraftingMaterial() variants, got {created.RequestedItemId}");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            var serverIssue = Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue>(owner.Issue);
            Assert.True(Server.ObjectManager.TryGetId(serverIssue._requestedItem, out var serverItemId));
            Assert.Equal(created.RequestedItemId, serverItemId);
        });

        // Every client independently constructed its OWN VillageNeedsCraftingMaterialsIssue instance (never
        // the same object as the server's) with the exact same captured requested item - the actual
        // replication contract, and specifically NOT each client's own independent SelectCraftingMaterial()
        // coin flip (which would diverge from the server's roughly half the time).
        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                var mirrored = Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue>(owner.Issue);

                Assert.True(client.ObjectManager.TryGetObject<ItemObject>(created.RequestedItemId, out var requestedItem));
                Assert.Same(requestedItem, mirrored._requestedItem);
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
                (in PotentialIssueData _, Hero h) => new VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue(h),
                typeof(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue),
                IssueBase.IssueFrequency.Rare);

            // IssueManagerCreateNewIssuePatches' prefix (already fully generic - see
            // VillageNeedsCraftingMaterialsIssueCreationPatch's doc comment) blocks a client from originating
            // a new issue.
            Assert.False(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
            Assert.Null(owner.Issue);
        });

        Assert.Empty(Client.NetworkSentMessages.GetMessages<NetworkVillageCraftingIssueCreated>());
    }

    // --- 2. Ownership-gate mechanism (CompleteQuestClickableConditions(out TextObject), not Tools' PlayerHasTools()) ---

    [Fact]
    public void QuestOwnershipGate_BlocksTurnInForAnyoneOtherThanTheRecordedOwner_EvenWithTheMaterialsInHand()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture.HeroId);
        ForcePromisedPaymentEverywhere(fixture.HeroId);
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");

        VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest quest = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));

            // A genuine (unwrapped) accept: VillageNeedsCraftingMaterialsQuestAcceptancePatch's real postfix
            // on IssueManager.StartIssueQuest fires and records ownership through the actual production path.
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            quest = Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest>(owner.Issue.IssueQuest);
        });

        // A fresh Server.Call (rather than continuing inline above): StartIssueQuest's own postfix broadcasts
        // the accept to every client synchronously, and this harness's static swap
        // (EnvironmentInstance.Call/StaticScope) does not restore Campaign.Current/MBObjectManager.Instance
        // back to the Server's own when that nested per-client dispatch unwinds - only a fresh Call's own
        // entry does. PartyBase.MainParty (which GetRequiredItemCountOnPlayer reads) resolves through
        // Campaign.Current, so it must be read from a clean Call, not the same one StartIssueQuest ran in.
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Campaign.Current.MainParty = party;

            // The materials are already on the party BEFORE the real dialogue Consequence delegate runs, so
            // QuestAcceptedConsequences' own GetRequiredItemCountOnPlayer() (called while building
            // _playerAcceptedQuestLog) genuinely captures a satisfied progress - exactly like a player who
            // already happened to be carrying enough when they accepted.
            party.ItemRoster.AddToCounts(quest._requestedItem, quest._requestedItemAmount);

            // QuestAcceptedConsequences is the real dialogue Consequence delegate a live "What do you need?"
            // conversation would invoke - genuinely populates _playerAcceptedQuestLog (which
            // CompleteQuestClickableConditions reads), rather than faking that state up directly.
            quest.QuestAcceptedConsequences();
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal("host-controller", ownerControllerId);
        });

        // The recorded owner, with enough materials: the gate lets the real dialogue condition run, and it's
        // satisfied.
        Server.Call(() =>
        {
            Assert.True(quest.CompleteQuestClickableConditions(out var explanation));
            Assert.Null(explanation);
        });

        // The exact same machine/roster, but no longer the recorded owner (e.g. after someone else's accept
        // won a race, or a dedicated server with no local player) - the gate now blocks the option outright,
        // despite the roster still genuinely holding enough materials.
        Server.Resolve<IControllerIdProvider>().SetControllerId("someone-else");
        Server.Call(() =>
        {
            Assert.False(quest.CompleteQuestClickableConditions(out var explanation));
            Assert.NotNull(explanation);
        });

        // Restore ownership match, then prove this isn't a tautological "always true for the owner" stub:
        // with the materials removed, the owner's real underlying progress check now correctly fails too.
        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            party.ItemRoster.AddToCounts(quest._requestedItem, -quest._requestedItemAmount);
            quest.UpdateQuestLog();
        });
        Server.Call(() =>
        {
            Assert.False(quest.CompleteQuestClickableConditions(out var explanation));
            Assert.NotNull(explanation);
        });
    }

    // --- 3. The accept-time reward-forcing mechanism (this issue type's genuinely novel mechanic) ---

    /// <summary>
    /// This is this file's single highest-value test (mirroring how Tools' accept-race test was its highest-
    /// value one): genuinely diverges the real per-client input <see cref="IVillageNeedsCraftingMaterialsIssueInterface"/>'s
    /// type doc comment identifies as the root cause - <c>IssueDifficultyMultiplier</c>, which
    /// <c>IssueBase.StartIssueWithQuest</c> captures from <c>Campaign.Current.Models.IssueModel.GetIssueDifficultyMultiplier()</c>
    /// = <c>Clamp(Campaign.Current.PlayerProgress, 0.1, 1)</c> at the INSTANT a machine's own accept genuinely
    /// runs. Each peer's <c>Campaign.Current</c> is a fully separate instance in this harness (see
    /// <see cref="E2E.Tests.Environment.Instance.GameInstance"/>), so setting a different <c>PlayerProgress</c>
    /// per peer is exactly the kind of drift that can occur for real between a host and a remote client -
    /// not a synthetic field poke.
    ///
    /// A remote client (not the server) is the one who genuinely accepts, so this also exercises the doc
    /// comment's most surprising claim: the correction gets forced back onto the ACCEPTER'S OWN client too,
    /// not just the bystander who never accepted.
    /// </summary>
    [Fact]
    public void RemoteClientAccept_ForceCorrectsQuantityAndRewardOnEveryPeer_IncludingTheAccepterItself_WhenIssueDifficultyMultiplierDiverges()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture.HeroId);

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        // Diverge the real driver: server sees high player progress, the accepting client sees low player
        // progress, the bystander client sees yet another value - three genuinely different
        // IssueDifficultyMultiplier values feeding the same real GenerateIssueQuest()/GetPayment() production
        // code on three independent Campaign instances. This alone genuinely diverges _numberOfRequestedItem.
        Server.Call(() => PlayerProgressProperty.SetValue(Campaign.Current, 1.0f));
        Client.Call(() => PlayerProgressProperty.SetValue(Campaign.Current, 0.1f));
        OtherClient.Call(() => PlayerProgressProperty.SetValue(Campaign.Current, 0.55f));

        // Each peer also gets its own distinct _promisedPayment (see ForcePromisedPayment's doc comment for
        // why this test doesn't stand up real Village/Town market data) - this file's stand-in for the
        // market-data half of the same real divergence, so RewardGold's own force-write can be proven too.
        ForcePromisedPayment(Server, fixture.HeroId, 2000);
        ForcePromisedPayment(Client, fixture.HeroId, 500);
        ForcePromisedPayment(OtherClient, fixture.HeroId, 800);

        // The accepting client (player-A)'s own live conversation genuinely accepts - runs its OWN real
        // StartIssueQuest, baking its own (about-to-be-superseded) _requestedItemAmount/RewardGold from ITS
        // OWN multiplier. VillageCraftingIssueQuestAcceptTriggered is published locally with exactly what this
        // machine baked - recorded here BEFORE the round trip below can correct it, so this captures the
        // genuinely diverged value rather than racing the correction that happens later in the very same
        // synchronous call.
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
        });

        var clientTriggered = Assert.Single(Client.InternalMessages.GetMessages<VillageCraftingIssueQuestAcceptTriggered>());
        Assert.True(clientTriggered.RequestedItemAmount > 0);

        // The server's own replay (ReplayQuestAccepted) used ITS OWN multiplier (1.0, the maximum) - since
        // GetPayment() scales with _numberOfRequestedItem, a genuinely different multiplier must move both
        // fields, not just one.
        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageCraftingIssueQuestAccepted>());
        Assert.Equal(fixture.HeroId, accepted.OwnerId);
        Assert.Equal("player-A", accepted.OwnerControllerId);
        Assert.NotEqual(clientTriggered.RequestedItemAmount, accepted.RequestedItemAmount);
        Assert.NotEqual(clientTriggered.RewardGold, accepted.RewardGold);

        // Every peer - the server, the never-accepted OtherClient (who must replay to even get a quest
        // object), AND the accepter Client's OWN already-existing quest - must now carry the SAME
        // server-authoritative values, not their own locally-diverged ones.
        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                var quest = Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest>(owner.Issue.IssueQuest);
                Assert.Equal(accepted.RequestedItemAmount, quest._requestedItemAmount);
                Assert.Equal(accepted.RewardGold, quest.RewardGold);
            });
        }

        // Ownership converged on the genuine accepter everywhere too.
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

    // --- 4. Accept-race arbitration ---

    [Fact]
    public void RequestVillageCraftingIssueAcceptQuest_FirstRequestWins_SecondIsRejectedAndOwnershipConvergesOnEveryPeer()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture.HeroId);
        ForcePromisedPaymentEverywhere(fixture.HeroId);

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", "", "", "")));
            Assert.True(playerManager.AddPlayer(new Player("player-B", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");
        TestEnvironment.ConnectRegisteredPlayer(OtherClient, "player-B");

        // Client (player-A)'s own live conversation accepted first - tells the server.
        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, new RequestVillageCraftingIssueAcceptQuest(fixture.HeroId));
        });

        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageCraftingIssueQuestAccepted>());
        Assert.Equal(fixture.HeroId, accepted.OwnerId);
        Assert.Equal("player-A", accepted.OwnerControllerId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkVillageCraftingIssueAcceptRejected>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.False(owner.Issue.IsOngoingWithoutQuest);
            Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest>(owner.Issue.IssueQuest);
            Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal("player-A", ownerControllerId);
        });

        // OtherClient (player-B) requests second, for the SAME issue - loses the race, since the server's own
        // copy is no longer IsOngoingWithoutQuest.
        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(OtherClient.NetPeer, new RequestVillageCraftingIssueAcceptQuest(fixture.HeroId));
        });

        // Still only the one accept ever went out - no second broadcast for the same issue.
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageCraftingIssueQuestAccepted>());
        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageCraftingIssueAcceptRejected>());
        Assert.Equal(fixture.HeroId, rejected.OwnerId);

        // The rejection was addressed ONLY to the losing peer - never broadcast, never delivered to the winner.
        Assert.Single(OtherClient.InternalMessages.GetMessages<NetworkVillageCraftingIssueAcceptRejected>());
        Assert.Empty(Client.InternalMessages.GetMessages<NetworkVillageCraftingIssueAcceptRejected>());

        // Both the winner AND the loser mirrored the SAME winning accept (the first broadcast reached every
        // client), and both record the SAME ownership - the losing peer's own request never overwrote it.
        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest>(owner.Issue.IssueQuest);
                Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("player-A", ownerControllerId);
            });
        }
    }

    [Fact]
    public void RequestVillageCraftingIssueAcceptQuest_FromUnregisteredRequester_IsRejectedWithoutMutatingTheIssue()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture.HeroId);

        // Client's peer was never registered/connected to any player - the server cannot resolve who is
        // asking, so IPlayerManager.TryGetPlayer(NetPeer, ...) fails.
        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, new RequestVillageCraftingIssueAcceptQuest(fixture.HeroId));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkVillageCraftingIssueQuestAccepted>());
        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageCraftingIssueAcceptRejected>());
        Assert.Equal(fixture.HeroId, rejected.OwnerId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(owner.Issue.IsOngoingWithoutQuest);
            Assert.False(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out _));
        });
    }

    // --- 5. Finalize / cleanup (shared, generic teardown - see VillageNeedsCraftingMaterialsIssueHandler's doc comment) ---

    [Fact]
    public void RequestVillageIssueRemoved_FinalizesTheRealQuestAndBroadcastsRemovalToEveryPeer()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture.HeroId);
        ForcePromisedPaymentEverywhere(fixture.HeroId);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");

        // A genuine (unwrapped) accept on the server itself - VillageNeedsCraftingMaterialsQuestAcceptancePatch's
        // real postfix records ownership and broadcasts the accept through the actual production path.
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
                Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest>(owner.Issue.IssueQuest);
            });
        }

        // The accepting client's own turn-in conversation genuinely finalized its local copy with success - it
        // tells the server (a client's SendAll only ever reaches the server). This finalize path is fully
        // generic/shared with Tools (see the handler's doc comment) - no Crafting-Materials-specific message
        // needed.
        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer,
                new RequestVillageIssueRemoved(fixture.HeroId, VillageIssueFinalizeReason.QuestSuccess));
        });

        var removed = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageIssueRemoved>());
        Assert.Equal(fixture.HeroId, removed.OwnerId);
        Assert.Equal(VillageIssueFinalizeReason.QuestSuccess, removed.Reason);

        // Genuinely gone everywhere: removed from IssueManager.Issues, hero.Issue nulled out, on the server
        // AND on every client via the mirrored broadcast.
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

    [Fact]
    public void RequestVillageIssueRemoved_WithNoQuestYet_FallsBackToBareIssueFinalizedWithoutOrphaning()
    {
        // IssueOnly: no accepted quest exists at all (e.g. the ambient stay-alive-conditions-failed path) -
        // FinalizeMirror must not try to complete a quest that was never started.
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture.HeroId);

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer,
                new RequestVillageIssueRemoved(fixture.HeroId, VillageIssueFinalizeReason.IssueOnly));
        });

        var removed = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageIssueRemoved>());
        Assert.Equal(VillageIssueFinalizeReason.IssueOnly, removed.Reason);

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.Null(owner.Issue);
            });
        }
    }
}
