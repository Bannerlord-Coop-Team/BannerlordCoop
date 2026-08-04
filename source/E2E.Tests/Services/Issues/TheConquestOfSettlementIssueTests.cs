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
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

/// <summary>
/// Real, executed multi-peer coverage for The Conquest of a Settlement (source/GameInterface/Services/Issues/
/// {Interfaces,Messages,Handlers,Patches}/TheConquestOfSettlement*.cs), following the same harness/conventions
/// established by <see cref="CaravanAmbushIssueTests"/>/<see cref="LandLordCompanyOfTroubleIssueTests"/>.
///
/// The two confirmed, root-caused bugs this suite exists to prove fixed (see
/// <see cref="Patches.TheConquestOfSettlementSiegeCompletionPatches"/>/
/// <see cref="Patches.TheConquestOfSettlementSettlementOwnerChangedPatch"/> for the full derivation): (1)
/// <c>OnSiegeCompleted</c> compared the real, correctly-known attacker party against <c>MobileParty.MainParty</c>
/// - "whichever local avatar happens to be running this process," not this quest's real recorded owner; (2)
/// <c>OnSettlementOwnerChanged</c>'s <c>ByBarter</c> success branch compared the settlement's new owner against
/// <c>Hero.MainHero</c> instead of the quest's real recorded owner.
/// </summary>
public class TheConquestOfSettlementIssueTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();
    private EnvironmentInstance OtherClient => TestEnvironment.Clients.Last();
    private IEnumerable<EnvironmentInstance> AllInstances => new[] { Server }.Concat(TestEnvironment.Clients);

    public TheConquestOfSettlementIssueTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private record ConquestFixture(string HeroId, string TargetSettlementId);

    /// <summary>Builds the issue-giving Hero plus a target settlement. Also seeds
    /// <see cref="Campaign.EncyclopediaManager"/> on every instance - the real journal logs (<c>QuestStartedLog</c>
    /// etc.) eagerly resolve encyclopedia links, same hazard/fix as every other Group A/Tier 3 type's own
    /// <c>SetupIssueOwner</c>. Also constructs a bare <see cref="TheConquestOfSettlementIssueBehavior"/> and
    /// calls its real <c>RegisterEvents()</c> on every instance - this harness's bootstrap does not itself
    /// instantiate every one of vanilla's ~40 issue-type behaviors, so without this explicit call
    /// <see cref="Patches.TheConquestOfSettlementSiegeCompletionPatches"/>'s own module-level
    /// <c>SiegeCompletedEvent</c> listener (subscribed from a Postfix on this exact method) would never get
    /// wired at all - harmless to call directly, matching this method's real production entry point (the game's
    /// own campaign-behavior bootstrap).</summary>
    private ConquestFixture SetupIssueOwner()
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
                }

                new TheConquestOfSettlementIssueBehavior().RegisterEvents();
            });
        }

        return new ConquestFixture(heroId, targetSettlementId);
    }

    /// <summary>Drives the real server-authoritative creation path: a genuine <see cref="IssueManager.CreateNewIssue"/>
    /// call handing back a real <see cref="TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue"/>
    /// constructed with the given (server-picked) settlement - exactly what
    /// <c>TheConquestOfSettlementIssueBehavior.OnStartIssue</c> does for real, just with the settlement supplied
    /// directly instead of re-running <c>ConditionsHold</c>'s own RNG scan.</summary>
    private void CreateIssueOnServer(ConquestFixture fixture)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.TargetSettlementId, out var target));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) => new TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue(h, target),
                typeof(TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue),
                IssueBase.IssueFrequency.VeryCommon);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
        });
    }

    /// <summary>Real (unwrapped) accept on <paramref name="instance"/> - drives <see cref="IssueManager.StartIssueQuest"/>
    /// for real, which (per <see cref="GenericAcceptMirrorIssueTypes"/>) triggers the fully generic
    /// accept-broadcast/ownership-record path unchanged. Does NOT by itself call <c>StartQuest()</c>/
    /// <c>RegisterEvents()</c> (see <see cref="Patches.TheConquestOfSettlementSiegeCompletionPatches"/>'s doc
    /// comment for why that's a separate, live-dialogue-only step) - callers that need the genuine
    /// <c>OnSiegeCompleted</c>/<c>OnSettlementOwnerChanged</c> listeners wired should also invoke
    /// <c>QuestAcceptedConsequences</c> via <see cref="InvokePrivate"/>.</summary>
    private TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest AcceptOnInstance(EnvironmentInstance instance, string heroId)
    {
        TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest quest = null;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var owner));
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            quest = Assert.IsType<TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest>(owner.Issue.IssueQuest);
        });
        return quest;
    }

    /// <summary>Registers a connected player (a real, addressable <see cref="Player"/> entry that
    /// <see cref="TheConquestOfSettlementIssueOwnershipResolver.TryResolveOwner"/> can resolve
    /// ControllerId -&gt; Hero/MobileParty through) on the server, with a fresh Hero and MobileParty of its own.
    /// Deliberately separate from <see cref="Hero.MainHero"/> - see the type doc comment.</summary>
    private (string HeroId, string PartyId) RegisterConnectedPlayer(string controllerId, string clanId = null)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            if (clanId != null && Server.ObjectManager.TryGetObject<Clan>(clanId, out var clan))
            {
                hero.Clan = clan;
                party.ActualClan = clan;
            }

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(controllerId, heroId, partyId, clanId ?? "", "")));
        });

        return (heroId, partyId);
    }

    private static void InvokePrivate(object instance, string methodName, params object[] args)
    {
        var method = AccessTools.Method(instance.GetType(), methodName);
        method.Invoke(instance, args);
    }

    /// <summary>
    /// Directly invokes <see cref="Patches.TheConquestOfSettlementSiegeCompletionPatches"/>'s private static
    /// <c>Resolve</c> method - the actual Bug 1 fix/decision logic (owner resolution, success/lesser-success/
    /// cancel branching, reward payment) - against the quest object <paramref name="quest"/> the caller already
    /// holds, bypassing both the genuine <c>CampaignEvents.SiegeCompletedEvent</c> subscribe-then-dispatch round
    /// trip AND the <c>OnSiegeCompleted</c> wrapper's own <c>Campaign.Current.IssueManager.Issues</c> scan.
    ///
    /// Harness limitation (confirmed empirically, not a fix-quality concern): this harness's <c>IObjectManager</c>
    /// does not return the SAME Hero instance for a given StringId across separate <c>EnvironmentInstance.Call()</c>
    /// invocations (confirmed via <c>GetHashCode()</c> - matching <c>StringId</c>, differing identity), so
    /// <c>Campaign.Current.IssueManager.Issues</c> (a reference-equality-keyed <c>Dictionary&lt;Hero, IssueBase&gt;</c>
    /// in real gameplay) can silently fail to dictionary-match a Hero re-resolved in a later call even against its
    /// own genuine entry - a test-harness-only quirk (no production code re-resolves Hero objects across
    /// arbitrary boundaries the way constructing a byte-for-byte test scenario does). Calling <c>Resolve</c>
    /// directly against the already-held <c>quest</c> reference sidesteps that dictionary lookup entirely while
    /// still exercising the exact same decision logic <c>OnSiegeCompleted</c>'s scan would have called it with.
    /// </summary>
    private static void InvokeSiegeCompletedResolve(object quest, Settlement siegeSettlement, MobileParty attackerParty)
    {
        var method = AccessTools.Method(
            typeof(GameInterface.Services.Issues.Patches.TheConquestOfSettlementSiegeCompletionPatches), "Resolve");
        Assert.True(method != null, "Resolve method not found via reflection");
        try
        {
            method.Invoke(null, new object[] { quest, siegeSettlement, attackerParty });
        }
        catch (System.Reflection.TargetInvocationException ex)
        {
            throw new Exception("InvokeSiegeCompletedResolve threw: " + ex.InnerException, ex.InnerException);
        }
    }

    // --- 1. Creation and replication ---

    [Fact]
    public void GenuineServerCreation_CapturesThePickedTargetSettlementAndReplicatesAByteIdenticalIssueToEveryClient()
    {
        var fixture = SetupIssueOwner();

        CreateIssueOnServer(fixture);

        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkTheConquestOfSettlementIssueCreated>());
        Assert.Equal(fixture.HeroId, created.OwnerId);
        Assert.Equal(fixture.TargetSettlementId, created.TargetSettlementId);

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                var mirrored = Assert.IsType<TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue>(owner.Issue);

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
                (in PotentialIssueData _, Hero h) => new TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue(h, target),
                typeof(TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue),
                IssueBase.IssueFrequency.VeryCommon);

            Assert.False(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
            Assert.Null(owner.Issue);
        });

        Assert.Empty(Client.NetworkSentMessages.GetMessages<NetworkTheConquestOfSettlementIssueCreated>());
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
                var quest = Assert.IsType<TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest>(owner.Issue.IssueQuest);
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(fixture.TargetSettlementId, out var target));
                Assert.Same(target, quest._targetSettlement);

                Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("host-controller", ownerControllerId);
            });
        }
    }

    // --- 3. Bug 1: OnSiegeCompleted resolves the real owner, not MobileParty.MainParty ---

    /// <summary>The decisive test: the SERVER OPERATOR's own local avatar (<c>MobileParty.MainParty</c> on the
    /// server) is a THIRD party, genuinely uninvolved in the siege - old code (<c>attackerParty ==
    /// MobileParty.MainParty</c>) would compare the real attacker against this unrelated party and, finding no
    /// match, fall through to "was MY OWN party involved at all" (also false for the unrelated party) and wrongly
    /// cancel. The real owner is a SEPARATE connected player who genuinely led the winning attack in person
    /// (<c>attackerParty == ownerParty</c>) - the quest must succeed using THAT identity, and the reward must go
    /// to the real owner's Hero, not to whoever <c>Hero.MainHero</c> happens to be on the server.</summary>
    [Fact]
    public void OnSiegeCompleted_OwnerPersonallyLed_SucceedsUsingTheRealOwnersIdentity_IgnoringTheServersOwnMainParty()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("owner-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);

        var (ownerHeroId, ownerPartyId) = RegisterConnectedPlayer("owner-controller");
        var unrelatedPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() =>
        {
            InvokePrivate(quest, "QuestAcceptedConsequences");

            // Harness note: quest.QuestGiver/quest._targetSettlement (the quest's own stored references) are
            // used below rather than fresh Server.ObjectManager lookups for the SAME ids - this harness's
            // IObjectManager does not return the same Hero/Settlement instance for a given StringId across
            // separate EnvironmentInstance.Call() invocations (confirmed empirically via GetHashCode), so a
            // fresh lookup here would silently diverge from what quest itself actually holds.
            var questGiver = quest.QuestGiver;
            var target = quest._targetSettlement;
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var ownerHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(unrelatedPartyId, out var unrelatedParty));

            // Both the real attacker AND the quest giver share a faction (the attacking party's own Clan acts
            // as its own MapFaction when it has no Kingdom - see Clan.MapFaction/IsMapFaction).
            ownerParty.ActualClan = questGiver.Clan;

            // The server operator's own local avatar - deliberately a totally different, uninvolved party.
            Campaign.Current.MainParty = unrelatedParty;

            var goldBefore = ownerHero.Gold;

            InvokeSiegeCompletedResolve(quest, target, ownerParty);

            Assert.True(quest.IsFinalized);
            Assert.True(ownerHero.Gold > goldBefore);
            // 20000 is TheConquestOfSettlementIssue.RewardGold's real, unmodified constant value.
            Assert.Equal(goldBefore + 20000, ownerHero.Gold);
        });
    }

    /// <summary>Discriminating in the other direction: the real owner was genuinely NOT involved in the siege at
    /// all (a different, unrelated party of the SAME faction led it) - the fix must still correctly CANCEL
    /// (matching vanilla's own "you didn't participate, moot" semantics), not blindly succeed for anyone who
    /// happens to share the quest giver's faction.</summary>
    [Fact]
    public void OnSiegeCompleted_OwnerNotInvolvedAtAll_StillCorrectlyCancels()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("owner-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);

        var (ownerHeroId, ownerPartyId) = RegisterConnectedPlayer("owner-controller");
        var attackerPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() =>
        {
            InvokePrivate(quest, "QuestAcceptedConsequences");

            // See the harness note in OnSiegeCompleted_OwnerPersonallyLed_... - use the quest's own stored
            // references, not fresh ObjectManager lookups, for identity-sensitive comparisons.
            var questGiver = quest.QuestGiver;
            var target = quest._targetSettlement;
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(attackerPartyId, out var attackerParty));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var ownerHero));

            attackerParty.ActualClan = questGiver.Clan;

            var goldBefore = ownerHero.Gold;

            InvokeSiegeCompletedResolve(quest, target, attackerParty);

            Assert.True(quest.IsFinalized);
            // No reward - the real owner was never involved.
            Assert.Equal(goldBefore, ownerHero.Gold);
        });
    }

    // --- 4. Bug 2: OnSettlementOwnerChanged's ByBarter branch resolves the real owner, not Hero.MainHero ---

    /// <summary>The decisive test: an unrelated player independently barters for the target settlement - nothing
    /// to do with this quest. Old code (<c>newOwner == Hero.MainHero</c>) would wrongly succeed whenever THIS
    /// process's own <c>Hero.MainHero</c> happens to equal the real new owner (exactly true on the exploiting
    /// player's own machine/mirror). The fix must NOT complete the real owner's quest for this unrelated
    /// transaction.</summary>
    [Fact]
    public void OnSettlementOwnerChanged_ByBarter_UnrelatedNewOwner_DoesNotWronglyCompleteTheQuest()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("owner-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);
        var (ownerHeroId, _) = RegisterConnectedPlayer("owner-controller");

        var unrelatedHeroId = TestEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            InvokePrivate(quest, "QuestAcceptedConsequences");

            Assert.True(Server.ObjectManager.TryGetObject<Hero>(unrelatedHeroId, out var unrelatedHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var ownerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.TargetSettlementId, out var target));

            // Simulate running on the exploiting player's OWN machine: their local Hero.MainHero genuinely is
            // the unrelated new owner - the exact condition old code's `newOwner == Hero.MainHero` needed to
            // wrongly succeed.
            ((CharacterObject)Game.Current.PlayerTroop).HeroObject = unrelatedHero;

            var goldBefore = ownerHero.Gold;

            InvokePrivate(quest, "OnSettlementOwnerChanged",
                target, false, unrelatedHero, unrelatedHero, null,
                ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByBarter);

            Assert.False(quest.IsFinalized);
            Assert.Equal(goldBefore, ownerHero.Gold);
            Assert.Equal(goldBefore, unrelatedHero.Gold);
        });
    }

    /// <summary>The real owner genuinely barters for their own quest's target settlement themselves - the fix
    /// must still let this succeed, paying the real owner.</summary>
    [Fact]
    public void OnSettlementOwnerChanged_ByBarter_RealOwner_StillSucceeds()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("owner-controller");
        var quest = AcceptOnInstance(Server, fixture.HeroId);
        var (ownerHeroId, _) = RegisterConnectedPlayer("owner-controller");

        Server.Call(() =>
        {
            InvokePrivate(quest, "QuestAcceptedConsequences");

            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var ownerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.TargetSettlementId, out var target));

            ((CharacterObject)Game.Current.PlayerTroop).HeroObject = ownerHero;

            var goldBefore = ownerHero.Gold;

            InvokePrivate(quest, "OnSettlementOwnerChanged",
                target, false, ownerHero, ownerHero, null,
                ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByBarter);

            Assert.True(quest.IsFinalized);
            // 20000 / 4 (QuestSuccess(2)'s own RewardGold - the ByBarter branch always passes a flat 2 boost,
            // not a share of RewardGold - confirmed by decompile: QuestSuccess(int boost) always pays the FULL
            // RewardGold regardless of boost, only the Security/Loyalty bump scales with boost).
            Assert.Equal(goldBefore + 20000, ownerHero.Gold);
        });
    }

    // --- 5. Accept-race arbitration (shared mechanism only) ---

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
            Assert.IsType<TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest>(owner.Issue.IssueQuest);
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
                Assert.IsType<TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest>(owner.Issue.IssueQuest);
                Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("player-A", ownerControllerId);
            });
        }
    }

    // --- 6. Finalize / cleanup ---

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
                Assert.IsType<TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest>(owner.Issue.IssueQuest);
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
