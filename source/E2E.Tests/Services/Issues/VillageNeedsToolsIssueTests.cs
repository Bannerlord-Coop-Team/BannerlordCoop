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
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

/// <summary>
/// Real, executed multi-peer coverage for Village Needs Tools (source/GameInterface/Services/Issues/
/// {Interfaces,Messages,Handlers,Patches}/VillageNeedsTools*.cs), previously verified only by static code
/// review. Every test here drives the actual production entry point (a genuine
/// <see cref="IssueManager.CreateNewIssue"/>/<see cref="IssueManager.StartIssueQuest"/> call, or a real
/// <see cref="IMessageBroker"/> publish simulating a specific peer's network request) rather than
/// re-implementing the mod's own logic and asserting it against itself.
/// </summary>
public class VillageNeedsToolsIssueTests : IDisposable
{
    private static readonly PropertyInfo ItemValueProperty =
        AccessTools.Property(typeof(ItemObject), nameof(ItemObject.Value));

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();
    private EnvironmentInstance OtherClient => TestEnvironment.Clients.Last();
    private IEnumerable<EnvironmentInstance> AllInstances => new[] { Server }.Concat(TestEnvironment.Clients);

    public VillageNeedsToolsIssueTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private record VillageFixture(string HeroId, string VillageId, string SettlementId, string ItemId);

    /// <summary>
    /// Builds a village-owning Hero + its Village/Settlement + a requested ItemObject, independently wired
    /// up on the server AND every client (same string ids everywhere - CreateRegisteredObject already
    /// replicates the object itself; this wires the relationships a fresh Village/Settlement/Hero
    /// don't have by default). Hearth is pinned to 650 (>= the real constructor's 300 threshold) so
    /// VillageNeedsToolsIssue's constructor always takes the gold-payment branch and never needs a
    /// populated VillageType - kept out of scope of these tests deliberately.
    /// </summary>
    private VillageFixture SetupVillageOwner(int itemValue = 40)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var villageId = TestEnvironment.CreateRegisteredObject<Village>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var itemId = TestEnvironment.CreateRegisteredObject<ItemObject>();

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<Village>(villageId, out var village));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));

                using (new AllowedThread())
                {
                    settlement.SetSettlementComponent(village);
                    village.Bound = settlement;
                    village.Hearth = 650f;
                    hero.StayingInSettlement = settlement;
                    ItemValueProperty.SetValue(item, itemValue);
                }
            });
        }

        return new VillageFixture(heroId, villageId, settlementId, itemId);
    }

    /// <summary>
    /// Drives the real server-authoritative creation path exactly as a vetted vanilla issue-check would:
    /// <see cref="IssueManagerCreateNewIssuePatches"/>'s prefix lets a genuine
    /// <see cref="IssueManager.CreateNewIssue"/> call through on the server, and its postfix captures the
    /// real rolled fields and broadcasts them.
    /// </summary>
    private void CreateIssueOnServer(VillageFixture fixture)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(fixture.ItemId, out var requestedItem));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) => new VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue(h, requestedItem),
                typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue),
                IssueBase.IssueFrequency.VeryCommon);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
        });
    }

    // --- 1. Creation and replication ---

    [Fact]
    public void GenuineServerCreation_CapturesRolledFieldsAndReplicatesAByteIdenticalIssueToEveryClient()
    {
        var fixture = SetupVillageOwner(itemValue: 40);

        CreateIssueOnServer(fixture);

        // The server captured the real rolled terms off its own live issue and broadcast them.
        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageIssueCreated>());
        Assert.Equal(fixture.HeroId, created.OwnerId);
        Assert.Equal(fixture.ItemId, created.RequestedItemId);
        // Hearth was pinned to 650 (>= 300) - the exchange-goods branch never applies.
        Assert.Null(created.ExchangeItemId);
        Assert.Equal(0, created.NumberOfExchangeItem);
        Assert.True(created.Payment > 0, "Expected a resolved gold payment for a Hearth >= 300 village");
        Assert.True(created.NumberOfRequestedItem > 0);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            var serverIssue = Assert.IsType<VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue>(owner.Issue);
            Assert.Equal(created.NumberOfRequestedItem, serverIssue._numberOfRequestedItem);
            Assert.Equal(created.Payment, serverIssue._payment);
        });

        // Every client independently constructed its OWN VillageNeedsToolsIssue instance (never the same
        // object as the server's) with the exact same captured terms - the actual replication contract.
        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                var mirrored = Assert.IsType<VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue>(owner.Issue);

                Assert.True(client.ObjectManager.TryGetObject<ItemObject>(fixture.ItemId, out var requestedItem));
                Assert.Same(requestedItem, mirrored._requestedItem);
                Assert.Null(mirrored._exchangeItem);
                Assert.Equal(created.NumberOfRequestedItem, mirrored._numberOfRequestedItem);
                Assert.Equal(created.NumberOfExchangeItem, mirrored._numberOfExchangeItem);
                Assert.Equal(created.Payment, mirrored._payment);
            });
        }
    }

    [Fact]
    public void ClientOriginatedCreation_IsBlocked_IssueManagerNeverCreatesIt()
    {
        var fixture = SetupVillageOwner();

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<ItemObject>(fixture.ItemId, out var requestedItem));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) => new VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue(h, requestedItem),
                typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue),
                IssueBase.IssueFrequency.VeryCommon);

            // IssueManagerCreateNewIssuePatches' prefix blocks a client from originating a new issue.
            Assert.False(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
            Assert.Null(owner.Issue);
        });

        Assert.Empty(Client.NetworkSentMessages.GetMessages<NetworkVillageIssueCreated>());
    }

    // --- 2. Ownership-gate mechanism ---

    [Fact]
    public void QuestOwnershipGate_BlocksTurnInForAnyoneOtherThanTheRecordedOwner_EvenWithTheToolsInHand()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnServer(fixture);
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");

        VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest quest = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(fixture.ItemId, out var requestedItem));

            Campaign.Current.MainParty = party;

            // A genuine (unwrapped) accept: IssueQuestAcceptancePatch's real postfix on
            // IssueManager.StartIssueQuest fires and records ownership through the actual production path -
            // not set directly by this test.
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            quest = Assert.IsType<VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest>(owner.Issue.IssueQuest);

            party.ItemRoster.AddToCounts(requestedItem, quest._numberOfRequestedGood);
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal("host-controller", ownerControllerId);
        });

        // The recorded owner, with enough tools: the gate lets the real dialogue condition run, and it's
        // satisfied.
        Server.Call(() => Assert.True(quest.PlayerHasTools()));

        // The exact same machine/roster, but no longer the recorded owner (e.g. after someone else's
        // accept won a race, or a dedicated server with no local player) - the gate now blocks the option
        // outright, despite the roster still genuinely holding enough tools.
        Server.Resolve<IControllerIdProvider>().SetControllerId("someone-else");
        Server.Call(() => Assert.False(quest.PlayerHasTools()));

        // Restore ownership match, then prove this isn't a tautological "always true for the owner" stub:
        // with the tools removed, the owner's real underlying inventory check now correctly fails too.
        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(fixture.ItemId, out var requestedItem));
            party.ItemRoster.AddToCounts(requestedItem, -quest._numberOfRequestedGood);
        });
        Server.Call(() => Assert.False(quest.PlayerHasTools()));
    }

    // --- 3. Accept-race arbitration ---

    [Fact]
    public void RequestVillageIssueAcceptQuest_FirstRequestWins_SecondIsRejectedAndOwnershipConvergesOnEveryPeer()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnServer(fixture);

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
            Assert.IsType<VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest>(owner.Issue.IssueQuest);
            Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal("player-A", ownerControllerId);
        });

        // OtherClient (player-B) requests second, for the SAME issue - loses the race, since the server's
        // own copy is no longer IsOngoingWithoutQuest.
        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(OtherClient.NetPeer, new RequestVillageIssueAcceptQuest(fixture.HeroId));
        });

        // Still only the one accept ever went out - no second broadcast for the same issue.
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageIssueQuestAccepted>());
        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageIssueAcceptRejected>());
        Assert.Equal(fixture.HeroId, rejected.OwnerId);

        // The rejection was addressed ONLY to the losing peer - never broadcast, never delivered to the
        // winner.
        Assert.Single(OtherClient.InternalMessages.GetMessages<NetworkVillageIssueAcceptRejected>());
        Assert.Empty(Client.InternalMessages.GetMessages<NetworkVillageIssueAcceptRejected>());

        // Both the winner AND the loser mirrored the SAME winning accept (the first broadcast reached every
        // client), and both record the SAME ownership - the losing peer's own request never overwrote it.
        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.IsType<VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest>(owner.Issue.IssueQuest);
                Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("player-A", ownerControllerId);
            });
        }
    }

    [Fact]
    public void RequestVillageIssueAcceptQuest_FromUnregisteredRequester_IsRejectedWithoutMutatingTheIssue()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnServer(fixture);

        // Client's peer was never registered/connected to any player - the server cannot resolve who is
        // asking, so IPlayerManager.TryGetPlayer(NetPeer, ...) fails.
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

    // --- 4. Finalize / cleanup ---

    [Fact]
    public void RequestVillageIssueRemoved_FinalizesTheRealQuestAndBroadcastsRemovalToEveryPeer()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnServer(fixture);

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");

        // A genuine (unwrapped) accept on the server itself - IssueQuestAcceptancePatch's real postfix
        // records ownership and broadcasts the accept through the actual production path.
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
                Assert.IsType<VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest>(owner.Issue.IssueQuest);
            });
        }

        // The accepting client's own turn-in conversation genuinely finalized its local copy with success -
        // it tells the server (a client's SendAll only ever reaches the server).
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
        var fixture = SetupVillageOwner();
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
