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
using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

/// <summary>
/// Real, executed multi-peer coverage for Revenue Farming (source/GameInterface/Services/Issues/
/// {Interfaces,Messages,Handlers,Patches}/RevenueFarming*.cs), following the exact same harness/conventions
/// established by <see cref="VillageNeedsCraftingMaterialsIssueTests"/> (accept-time capture-and-force-write
/// pattern) and <see cref="GangLeaderNeedsToOffloadStolenGoodsIssueTests"/> (multi-method ownership-gate
/// coverage). Every test drives the actual production entry point (a genuine
/// <see cref="IssueManager.CreateNewIssue"/>/<see cref="IssueManager.StartIssueQuest"/> call, a direct call to a
/// real (Harmony-patched) private quest method, or a real <see cref="IMessageBroker"/> publish simulating a
/// specific peer's network request) rather than re-implementing the mod's own logic and asserting it against
/// itself.
///
/// This file's highest-value test is <see cref="RemoteClientAccept_ForceCorrectsTotalDenarsAndVillagesOnEveryPeer_IncludingTheAccepterItself_WhenLiveBoundVillagesStateDiverges"/>
/// (mirroring how Crafting Materials' own divergent-multiplier test was its highest-value one) - it genuinely
/// diverges each peer's own live <c>Village.Hearth</c> (the real driver <c>GenerateIssueQuest</c> reads at
/// accept time), not a synthetic field poke.
/// </summary>
public class RevenueFarmingIssueTests : IDisposable
{
    private static readonly FieldInfo VillageEventsField =
        AccessTools.Field(typeof(RevenueFarmingIssueBehavior), "_villageEvents");
    private static readonly PropertyInfo IssueTotalRequestedDenarsProperty =
        AccessTools.Property(typeof(RevenueFarmingIssueBehavior.RevenueFarmingIssue), "TotalRequestedDenars");

    // ChangeRelationActionPatches (GameInterface's own coop-specific patch) routes ChangeRelationAction.
    // ApplyPlayerRelation through ResolvedMainHeroContext.ResolvedMainHero instead of vanilla's own
    // Hero.MainHero - every real completion consequence this file drives reaches it - same technique/reasoning
    // as GangLeaderNeedsToOffloadStolenGoodsIssueTests/ArtisanOverpricedGoodsIssueTests.
    private static readonly FieldInfo ResolvedMainHeroField =
        AccessTools.Field(Type.GetType("GameInterface.Services.Heroes.Patches.ResolvedMainHeroContext, GameInterface"), "ResolvedMainHero");

    private static void SetResolvedMainHero(Hero hero) => ResolvedMainHeroField.SetValue(null, hero);

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();
    private EnvironmentInstance OtherClient => TestEnvironment.Clients.Last();
    private IEnumerable<EnvironmentInstance> AllInstances => new[] { Server }.Concat(TestEnvironment.Clients);

    public RevenueFarmingIssueTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private record RevenueVillageSpec(string VillageId, string VillageSettlementId);
    private record RevenueFixture(string HeroId, string TargetSettlementId, List<RevenueVillageSpec> Villages);

    /// <summary>
    /// Builds an issue-owning Hero + a target (town) Settlement + two bound Village/Settlement pairs,
    /// independently wired up on the server AND every client (same string ids everywhere -
    /// <c>CreateRegisteredObject</c> already replicates the object itself; this wires the relationships a fresh
    /// Settlement/Village/Hero don't have by default - same technique as <c>VillageNeedsToolsIssueTests.SetupVillageOwner</c>,
    /// generalized to two villages bound to one town via <c>Village.Bound</c>'s real setter, which itself
    /// populates the town's own <c>_boundVillages</c> via <c>AddBoundVillageInternal</c> - the real production
    /// mechanism <c>Settlement.BoundVillages</c>/<c>RevenueFarmingIssue.GenerateIssueQuest</c> reads).
    ///
    /// Also registers a bare <see cref="RevenueFarmingIssueBehavior"/> instance with an empty (reflectively
    /// force-written) <c>_villageEvents</c> list on each instance's own <see cref="Campaign"/> - this harness
    /// never runs the real campaign bootstrap that would otherwise populate it via
    /// <c>OnSessionLaunched</c>/<c>AddVillageEvents</c>, but <c>RevenueFarmingIssueQuest</c>'s own ctor
    /// unconditionally reads <c>Campaign.Current.GetCampaignBehavior&lt;RevenueFarmingIssueBehavior&gt;()._villageEvents</c>
    /// to seed <c>_currentVillageEvents</c>' keys - an empty list is enough for that (none of these tests drive
    /// the village-event minigame itself, only creation/accept/ownership-gate/raid behavior).
    /// </summary>
    private RevenueFixture SetupIssueOwner(float village1Hearth = 300f, float village2Hearth = 300f)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var targetSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var village1Id = TestEnvironment.CreateRegisteredObject<Village>();
        var village1SettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var village2Id = TestEnvironment.CreateRegisteredObject<Village>();
        var village2SettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(targetSettlementId, out var targetSettlement));
                Assert.True(instance.ObjectManager.TryGetObject<Village>(village1Id, out var village1));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(village1SettlementId, out var village1Settlement));
                Assert.True(instance.ObjectManager.TryGetObject<Village>(village2Id, out var village2));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(village2SettlementId, out var village2Settlement));

                using (new AllowedThread())
                {
                    // See VillageNeedsCraftingMaterialsIssueTests.SetupIssueOwner's doc comment - SetDialogs()
                    // eagerly builds lines referencing Hero.EncyclopediaLink/Settlement.EncyclopediaLinkWithName
                    // right in the Quest ctor, which NREs against this harness's never-populated
                    // EncyclopediaManager without this.
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();

                    // This harness's GameObjectCreator.CreateInitializedObject<T>() never assigns a real
                    // MBGUID (both villages otherwise default to the same Id) - OnVillageRaid's own real
                    // implementation looks up its target via _revenueVillages.FirstOrDefault(x =>
                    // x.Village.Id == village.Id), so each village needs a genuinely distinct Id for that
                    // production lookup to disambiguate them at all.
                    village1.Id = new MBGUID(1001);
                    village2.Id = new MBGUID(1002);

                    village1Settlement.SetSettlementComponent(village1);
                    village1.Bound = targetSettlement;
                    village1.Hearth = village1Hearth;

                    village2Settlement.SetSettlementComponent(village2);
                    village2.Bound = targetSettlement;
                    village2.Hearth = village2Hearth;

                    hero.StayingInSettlement = targetSettlement;

                    // QuestCompletedWithSuccess/QuestCompletedWithBetray both reach TraitLevelingHelper.
                    // OnIssueSolvedThroughQuest, which reads/writes Campaign.Current.PlayerTraitDeveloper -
                    // never initialized by this harness's lightweight bootstrap (same concern every other
                    // Succeed/Fail-consequence-driving test file in this project already documents, e.g.
                    // ArtisanOverpricedGoodsIssueTests).
                    Campaign.Current.PlayerTraitDeveloper ??= new PropertyOwner<PropertyObject>();

                    var behavior = new RevenueFarmingIssueBehavior();
                    VillageEventsField.SetValue(behavior, new List<RevenueFarmingIssueBehavior.VillageEvent>());
                    Campaign.Current.AddCampaignBehaviorManager(
                        new CampaignBehaviorManager(new List<CampaignBehaviorBase> { behavior }));
                }
            });
        }

        return new RevenueFixture(heroId, targetSettlementId, new List<RevenueVillageSpec>
        {
            new(village1Id, village1SettlementId),
            new(village2Id, village2SettlementId),
        });
    }

    /// <summary>Overwrites each village's <c>Hearth</c> on <paramref name="instance"/>'s own copy only - used to
    /// genuinely diverge the real per-client input <c>GenerateIssueQuest</c> reads at accept time (see
    /// <see cref="RemoteClientAccept_ForceCorrectsTotalDenarsAndVillagesOnEveryPeer_IncludingTheAccepterItself_WhenLiveBoundVillagesStateDiverges"/>).</summary>
    private void SetVillageHearth(EnvironmentInstance instance, RevenueFixture fixture, float village1Hearth, float village2Hearth)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Village>(fixture.Villages[0].VillageId, out var village1));
            Assert.True(instance.ObjectManager.TryGetObject<Village>(fixture.Villages[1].VillageId, out var village2));

            using (new AllowedThread())
            {
                village1.Hearth = village1Hearth;
                village2.Hearth = village2Hearth;
            }
        });
    }

    /// <summary>
    /// Drives the real server-authoritative creation path exactly as a vetted vanilla issue-check would:
    /// <see cref="Patches.RevenueFarmingIssueCreationPatch"/>'s postfix lets a genuine
    /// <see cref="IssueManager.CreateNewIssue"/> call through on the server, captures the real
    /// <c>_targetSettlement</c>, and broadcasts it. Unlike Village Needs Crafting Materials, the ctor takes the
    /// settlement directly (no roll to speculatively pre-register) - see
    /// <see cref="IRevenueFarmingIssueInterface"/>'s type doc comment.
    /// </summary>
    private void CreateIssueOnServer(RevenueFixture fixture)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.TargetSettlementId, out var targetSettlement));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) => new RevenueFarmingIssueBehavior.RevenueFarmingIssue(h, targetSettlement),
                typeof(RevenueFarmingIssueBehavior.RevenueFarmingIssue),
                IssueBase.IssueFrequency.VeryCommon);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
        });
    }

    // --- 1. Creation and replication ---

    [Fact]
    public void GenuineServerCreation_CapturesTargetSettlementAndReplicatesAByteIdenticalIssueToEveryClient()
    {
        var fixture = SetupIssueOwner();

        CreateIssueOnServer(fixture);

        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkRevenueFarmingIssueCreated>());
        Assert.Equal(fixture.HeroId, created.OwnerId);
        Assert.Equal(fixture.TargetSettlementId, created.TargetSettlementId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            var serverIssue = Assert.IsType<RevenueFarmingIssueBehavior.RevenueFarmingIssue>(owner.Issue);
            Assert.True(Server.ObjectManager.TryGetId(serverIssue._targetSettlement, out var serverSettlementId));
            Assert.Equal(created.TargetSettlementId, serverSettlementId);
        });

        // Every client independently constructed its OWN RevenueFarmingIssue instance (never the same object as
        // the server's) targeting the exact same settlement.
        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                var mirrored = Assert.IsType<RevenueFarmingIssueBehavior.RevenueFarmingIssue>(owner.Issue);

                Assert.True(client.ObjectManager.TryGetObject<Settlement>(created.TargetSettlementId, out var targetSettlement));
                Assert.Same(targetSettlement, mirrored._targetSettlement);
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
            Assert.True(Client.ObjectManager.TryGetObject<Settlement>(fixture.TargetSettlementId, out var targetSettlement));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) => new RevenueFarmingIssueBehavior.RevenueFarmingIssue(h, targetSettlement),
                typeof(RevenueFarmingIssueBehavior.RevenueFarmingIssue),
                IssueBase.IssueFrequency.VeryCommon);

            // IssueManagerCreateNewIssuePatches' prefix (already fully generic) blocks a client from
            // originating a new issue - see RevenueFarmingIssueCreationPatch's doc comment.
            Assert.False(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
            Assert.Null(owner.Issue);
        });

        Assert.Empty(Client.NetworkSentMessages.GetMessages<NetworkRevenueFarmingIssueCreated>());
    }

    // --- 2. Accept-time village-list re-derivation + the genuinely independent _totalRequestedDenars bug ---

    [Fact]
    public void AcceptTime_ReDerivesTheVillageListLiveFromBoundVillages_AndTotalRequestedDenarsDivergesFromTheIssueLevelPreview()
    {
        // Hearth 25/50 -> TargetAmount (int)(Hearth*4) = 100/200 - deliberately NOT a multiple of 3, so the
        // Quest ctor's per-village-then-sum division (100/3 + 200/3 = 33 + 66 = 99) genuinely differs from the
        // Issue-level preview's sum-then-divide-once (100 + 200 = 300, /3 = 100) - the exact bug task item 5
        // describes, verified here against the real decompiled formulas rather than trusted.
        var fixture = SetupIssueOwner(village1Hearth: 25f, village2Hearth: 50f);
        CreateIssueOnServer(fixture);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            var issue = Assert.IsType<RevenueFarmingIssueBehavior.RevenueFarmingIssue>(owner.Issue);
            var issuePreview = (int)IssueTotalRequestedDenarsProperty.GetValue(issue);
            Assert.Equal(100, issuePreview);

            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            var quest = Assert.IsType<RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest>(owner.Issue.IssueQuest);

            // Both villages present, re-derived live from BoundVillages at this exact moment.
            Assert.Equal(2, quest.RevenueVillages.Count);
            Assert.Contains(quest.RevenueVillages, v => v.TargetAmount == 100);
            Assert.Contains(quest.RevenueVillages, v => v.TargetAmount == 200);

            Assert.Equal(99, quest._totalRequestedDenars);
            Assert.NotEqual(issuePreview, quest._totalRequestedDenars);
        });
    }

    [Fact]
    public void AcceptTime_ExcludesARaidedVillageFromTheGeneratedList()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Village>(fixture.Villages[0].VillageId, out var village1));

            using (new AllowedThread())
            {
                // Settlement.IsRaided is a read-only derived property (IsVillage && VillageState == Looted) -
                // the real production signal GenerateIssueQuest's own filter reads.
                village1.VillageState = Village.VillageStates.Looted;
            }

            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            var quest = Assert.IsType<RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest>(owner.Issue.IssueQuest);

            // Only the non-raided village made it into the live-derived list.
            var onlyVillage = Assert.Single(quest.RevenueVillages);
            Assert.True(Server.ObjectManager.TryGetObject<Village>(fixture.Villages[1].VillageId, out var village2));
            Assert.Equal(village2.Id, onlyVillage.Village.Id);
        });
    }

    // --- 3. The accept-time force-write mechanism (this issue type's genuinely novel mechanic) ---

    /// <summary>
    /// This is this file's single highest-value test: genuinely diverges each peer's own live
    /// <c>Village.Hearth</c> (the real driver <c>GenerateIssueQuest</c> reads at accept time via
    /// <c>(int)(boundVillage.Hearth * 4f)</c>) rather than poking <c>_totalRequestedDenars</c> directly. A
    /// remote client (not the server) is the one who genuinely accepts, so this also exercises the doc
    /// comment's most surprising claim: the correction gets forced back onto the ACCEPTER'S OWN client too, not
    /// just the bystander who never accepted.
    /// </summary>
    [Fact]
    public void RemoteClientAccept_ForceCorrectsTotalDenarsAndVillagesOnEveryPeer_IncludingTheAccepterItself_WhenLiveBoundVillagesStateDiverges()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        // Server: TargetAmount 100/200 -> total 99 (this is what ends up authoritative).
        SetVillageHearth(Server, fixture, 25f, 50f);
        // Accepting client: TargetAmount 40/80 -> total 39 (its own, about-to-be-superseded, value).
        SetVillageHearth(Client, fixture, 10f, 20f);
        // Bystander: yet another distinct value, to prove it also converges rather than keeping its own.
        SetVillageHearth(OtherClient, fixture, 15f, 45f);

        // The accepting client's own live conversation genuinely accepts - runs its OWN real StartIssueQuest,
        // baking its own (about-to-be-superseded) total/villages from ITS OWN Hearth values.
        // RevenueFarmingQuestAcceptTriggered is published locally with exactly what this machine baked -
        // recorded here BEFORE the round trip below can correct it.
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
        });

        var clientTriggered = Assert.Single(Client.InternalMessages.GetMessages<RevenueFarmingQuestAcceptTriggered>());
        Assert.Equal(39, clientTriggered.TotalRequestedDenars);

        // The server's own replay (ReplayQuestAccepted) used ITS OWN live BoundVillages/Hearth state (25/50) -
        // genuinely different from what the accepting client baked.
        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkRevenueFarmingIssueQuestAccepted>());
        Assert.Equal(fixture.HeroId, accepted.OwnerId);
        Assert.Equal("player-A", accepted.OwnerControllerId);
        Assert.Equal(99, accepted.TotalRequestedDenars);
        Assert.NotEqual(clientTriggered.TotalRequestedDenars, accepted.TotalRequestedDenars);
        Assert.Equal(2, accepted.Villages.Length);
        Assert.Contains(accepted.Villages, v => v.TargetAmount == 100);
        Assert.Contains(accepted.Villages, v => v.TargetAmount == 200);

        // Every peer - the server, the never-accepted OtherClient (who must replay to even get a quest object),
        // AND the accepter Client's OWN already-existing quest - must now carry the SAME server-authoritative
        // values, not their own locally-diverged ones.
        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                var quest = Assert.IsType<RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest>(owner.Issue.IssueQuest);
                Assert.Equal(99, quest._totalRequestedDenars);
                Assert.Equal(2, quest.RevenueVillages.Count);
                Assert.Contains(quest.RevenueVillages, v => v.TargetAmount == 100);
                Assert.Contains(quest.RevenueVillages, v => v.TargetAmount == 200);
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

    // --- 4. Accept-race arbitration (delegates to the shared, already-fixed RejectAcceptance mechanism) ---

    [Fact]
    public void RequestRevenueFarmingIssueAcceptQuest_FirstRequestWins_SecondIsRejectedAndOwnershipConvergesOnEveryPeer()
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
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, new RequestRevenueFarmingIssueAcceptQuest(fixture.HeroId));
        });

        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkRevenueFarmingIssueQuestAccepted>());
        Assert.Equal(fixture.HeroId, accepted.OwnerId);
        Assert.Equal("player-A", accepted.OwnerControllerId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkRevenueFarmingIssueAcceptRejected>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.False(owner.Issue.IsOngoingWithoutQuest);
            Assert.IsType<RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest>(owner.Issue.IssueQuest);
            Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal("player-A", ownerControllerId);
        });

        // OtherClient (player-B) requests second, for the SAME issue - loses the race, since the server's own
        // copy is no longer IsOngoingWithoutQuest.
        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(OtherClient.NetPeer, new RequestRevenueFarmingIssueAcceptQuest(fixture.HeroId));
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkRevenueFarmingIssueQuestAccepted>());
        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkRevenueFarmingIssueAcceptRejected>());
        Assert.Equal(fixture.HeroId, rejected.OwnerId);

        Assert.Single(OtherClient.InternalMessages.GetMessages<NetworkRevenueFarmingIssueAcceptRejected>());
        Assert.Empty(Client.InternalMessages.GetMessages<NetworkRevenueFarmingIssueAcceptRejected>());

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.IsType<RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest>(owner.Issue.IssueQuest);
                Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("player-A", ownerControllerId);
            });
        }
    }

    [Fact]
    public void RequestRevenueFarmingIssueAcceptQuest_FromUnregisteredRequester_IsRejectedWithoutMutatingTheIssue()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, new RequestRevenueFarmingIssueAcceptQuest(fixture.HeroId));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkRevenueFarmingIssueQuestAccepted>());
        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkRevenueFarmingIssueAcceptRejected>());
        Assert.Equal(fixture.HeroId, rejected.OwnerId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(owner.Issue.IsOngoingWithoutQuest);
            Assert.False(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out _));
        });
    }

    // --- 5. Ownership gate coverage for the load-bearing methods ---

    [Fact]
    public void OwnershipGate_BlocksHourlyTickAndSettlementOwnerChangedAndQuestCompletionForAnyoneOtherThanTheRecordedOwner()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);
        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");

        RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest quest = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            quest = Assert.IsType<RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest>(owner.Issue.IssueQuest);

            // Real public API (RevenueVillage.SetDone), not reflection - makes HourlyTick's first branch
            // (OnAllRevenuesAreCollected) deterministically reachable without needing the full gold-collection
            // minigame (Settlement.CurrentSettlement/MobileParty.MainParty), matching how
            // GangLeaderNeedsToOffloadStolenGoodsIssueTests keeps its own gate test focused.
            using (new AllowedThread())
            {
                foreach (var revenueVillage in quest.RevenueVillages) revenueVillage.SetDone();
            }
        });

        // --- Non-owner: every gated method is a complete no-op. ---
        Server.Resolve<IControllerIdProvider>().SetControllerId("someone-else");
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.TargetSettlementId, out var targetSettlement));

            quest.HourlyTick();
            Assert.False(quest._allRevenuesAreCollected);

            quest.OnSettlementOwnerChanged(targetSettlement, false, null, quest.QuestGiver, null, default);
            Assert.NotNull(owner.Issue);
            Assert.Same(quest, owner.Issue.IssueQuest);

            quest.QuestCompletedWithSuccess();
            Assert.NotNull(owner.Issue);
            Assert.Same(quest, owner.Issue.IssueQuest);

            quest.QuestCompletedWithBetray();
            Assert.NotNull(owner.Issue);
            Assert.Same(quest, owner.Issue.IssueQuest);
        });

        // --- Restore ownership match - the real owner's own HourlyTick genuinely completes collection, proving
        // this isn't a tautological "always blocked" stub. ---
        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        Server.Call(() =>
        {
            quest.HourlyTick();
            Assert.True(quest._allRevenuesAreCollected);
        });
    }

    /// <summary>
    /// Drives the real, unblocked owner path for the turn-in leaf method itself (not just proving the gate
    /// blocks a non-owner - the "not a tautological stub" half of the same coverage): the real gold deduction
    /// from <c>Hero.MainHero</c> and the quest genuinely ending. The full teardown cascade all the way to
    /// <c>owner.Issue</c> being nulled is exercised separately by
    /// <see cref="RequestVillageIssueRemoved_FinalizesTheRealQuestAndBroadcastsRemovalToEveryPeer"/> (which
    /// reaches the same underlying <c>QuestBase.CompleteQuestWithSuccess</c> via the real
    /// <c>CampaignEventDispatcher</c>-driven finalize broadcast rather than a bare direct call, and is this
    /// harness's proven way to observe that cascade complete).
    /// </summary>
    [Fact]
    public void QuestCompletedWithSuccess_GenuinelyCompletesForTheRecordedOwner_AndDeductsTheAuthoritativeTotalFromMainHero()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);
        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            var quest = Assert.IsType<RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest>(owner.Issue.IssueQuest);

            using (new AllowedThread())
            {
                Game.Current.PlayerTroop = owner.CharacterObject;
                owner.ChangeHeroGold(5000);
            }
            SetResolvedMainHero(owner);

            Assert.Same(owner, Hero.MainHero);
            Assert.True(VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(owner));

            var expectedTotal = quest._totalRequestedDenars;
            var goldBefore = owner.Gold;

            quest.QuestCompletedWithSuccess();

            Assert.Equal(goldBefore - expectedTotal, owner.Gold);
            Assert.False(quest.IsOngoing);
        });
    }

    // --- 6. A village being removed from consideration mid-quest via OnVillageRaid ---

    [Fact]
    public void OnVillageRaid_RemovesTheRaidedVillageFromConsiderationAndReducesTheTotal_ForTheOwnerOnly()
    {
        // Hearth 300 both -> TargetAmount 1200 each, /3 = 400 each, total 800.
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);
        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");

        RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest quest = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            quest = Assert.IsType<RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest>(owner.Issue.IssueQuest);

            // The real dialogue Consequence a live "accept the quest" conversation would invoke - populates
            // _questProgressLog (OnVillageRaid's own UpdateCurrentProgress reads it), rather than faking that
            // state up directly.
            quest.QuestAcceptedConsequences();
        });

        Assert.Equal(800, quest._totalRequestedDenars);

        // --- Non-owner: a locally-fired VillageBeingRaided listener must be a complete no-op. ---
        Server.Resolve<IControllerIdProvider>().SetControllerId("someone-else");
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Village>(fixture.Villages[0].VillageId, out var village1));
            quest.OnVillageRaid(village1);
        });
        Assert.Equal(800, quest._totalRequestedDenars);
        Assert.Equal(2, quest.RevenueVillages.Count(v => !v.IsRaided));

        // --- The real owner: the raided village is marked done/raided and removed from the requested total;
        // the quest itself survives since the other village is still available. ---
        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Village>(fixture.Villages[0].VillageId, out var village1));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));

            quest.OnVillageRaid(village1);

            Assert.Equal(400, quest._totalRequestedDenars);
            var raidedEntry = quest.RevenueVillages.Single(v => v.Village.Id == village1.Id);
            Assert.True(raidedEntry.IsRaided);
            Assert.True(raidedEntry.GetIsCompleted());
            Assert.NotNull(owner.Issue);
            Assert.Same(quest, owner.Issue.IssueQuest);
        });

        // --- Raiding the LAST remaining village cancels the whole quest for the real owner. ---
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Village>(fixture.Villages[1].VillageId, out var village2));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));

            quest.OnVillageRaid(village2);

            Assert.Null(owner.Issue);
        });
    }

    // --- 7. Finalize / cleanup (shared, generic teardown) ---

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
                Assert.IsType<RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest>(owner.Issue.IssueQuest);
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

    [Fact]
    public void RequestVillageIssueRemoved_WithNoQuestYet_FallsBackToBareIssueFinalizedWithoutOrphaning()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

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
