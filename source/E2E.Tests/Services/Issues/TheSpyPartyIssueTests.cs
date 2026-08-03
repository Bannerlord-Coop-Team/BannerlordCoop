using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using SandBox.Issues;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

/// <summary>
/// Real, executed multi-peer coverage for The Spy Party (source/GameInterface/Services/Issues/
/// {Interfaces,Messages,Handlers,Patches}/TheSpyParty*.cs), following the exact same harness/conventions
/// established by <see cref="VillageNeedsToolsIssueTests"/>/<see cref="VillageNeedsCraftingMaterialsIssueTests"/>.
/// Every test drives the actual production entry point (a genuine <see cref="IssueManager.CreateNewIssue"/>/
/// <see cref="IssueManager.StartIssueQuest"/> call, or a real <see cref="IMessageBroker"/> publish simulating a
/// specific peer's network request) rather than re-implementing the mod's own logic and asserting it against
/// itself.
///
/// This file's single highest-value test,
/// <see cref="RequestTheSpyPartyIssueAcceptQuest_FirstRequestWins_SecondIsRejectedAndOwnershipConvergesOnEveryPeer"/>,
/// is what found <see cref="ITheSpyPartyIssueInterface.RejectAcceptance"/>'s own copy of the exact same
/// accept-race rollback bug already found and fixed in Village Needs Tools, then Village Needs Crafting
/// Materials (which had copy-pasted Tools' OLD, pre-fix code): this type's own <c>RejectAcceptance</c> doc
/// comment claimed "identical, fully generic logic to VillageNeedsToolsIssueInterface.RejectAcceptance" - true
/// only of Tools' pre-fix version, since the ownership guard was never ported over here either.
/// </summary>
public class TheSpyPartyIssueTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();
    private EnvironmentInstance OtherClient => TestEnvironment.Clients.Last();
    private IEnumerable<EnvironmentInstance> AllInstances => new[] { Server }.Concat(TestEnvironment.Clients);

    public TheSpyPartyIssueTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private record SpyPartyFixture(string HeroId, string SettlementId);

    /// <summary>
    /// Builds the issue-owning Hero plus the tournament-host Settlement. Also seeds
    /// <see cref="Campaign.EncyclopediaManager"/> on every instance - same reason/technique as
    /// <see cref="VillageNeedsCraftingMaterialsIssueTests.SetupIssueOwner"/>: unlike Tools, this quest's own
    /// <c>SetDialogs()</c> (called eagerly from the <c>TheSpyPartyIssueQuest</c> constructor) builds a line
    /// referencing <c>_selectedSettlement.EncyclopediaLinkWithName</c>, which genuinely resolves
    /// <c>Campaign.Current.EncyclopediaManager.GetIdentifier(typeof(Settlement))</c>. This harness never runs
    /// the real campaign bootstrap that populates that manager, so any real accept would NRE/throw there
    /// without this.
    /// </summary>
    private SpyPartyFixture SetupIssueOwner()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out _));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out _));

                using (new AllowedThread())
                {
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                }
            });
        }

        return new SpyPartyFixture(heroId, settlementId);
    }

    /// <summary>
    /// Drives the real server-authoritative creation path exactly as a vetted vanilla issue-check would:
    /// <see cref="Patches.TheSpyPartyIssueCreationPatch"/>'s postfix lets a genuine
    /// <see cref="IssueManager.CreateNewIssue"/> call through on the server and broadcasts the picked
    /// settlement. Constructs directly with <paramref name="fixture"/>'s settlement (the real ctor takes it
    /// directly - see <see cref="ITheSpyPartyIssueInterface.ConstructReplicated"/>'s doc comment for why this
    /// bypasses the creation-time <c>GetRandomElementWithPredicate</c> roll entirely, same technique
    /// <see cref="VillageNeedsToolsIssueTests.CreateIssueOnServer"/> already uses for its own requested item).
    /// </summary>
    private void CreateIssueOnServer(SpyPartyFixture fixture)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var selectedSettlement));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) => new TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue(h, selectedSettlement),
                typeof(TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue),
                IssueBase.IssueFrequency.Rare);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
        });
    }

    // --- 1. Creation and replication ---

    [Fact]
    public void GenuineServerCreation_CapturesTheSelectedSettlementAndReplicatesAByteIdenticalIssueToEveryClient()
    {
        var fixture = SetupIssueOwner();

        CreateIssueOnServer(fixture);

        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkTheSpyPartyIssueCreated>());
        Assert.Equal(fixture.HeroId, created.OwnerId);
        Assert.Equal(fixture.SettlementId, created.SelectedSettlementId);

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                var mirrored = Assert.IsType<TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue>(owner.Issue);

                Assert.True(client.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var selectedSettlement));
                Assert.True(client.ObjectManager.TryGetId(mirrored._selectedSettlement, out var mirroredSettlementId));
                Assert.Equal(fixture.SettlementId, mirroredSettlementId);
                Assert.Same(selectedSettlement, mirrored._selectedSettlement);
            });
        }
    }

    // --- 2. Accept-race arbitration (this file's highest-value test - see the type doc comment) ---

    [Fact]
    public void RequestTheSpyPartyIssueAcceptQuest_FirstRequestWins_SecondIsRejectedAndOwnershipConvergesOnEveryPeer()
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

        // Client (player-A)'s own live conversation accepted first - tells the server.
        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, new RequestTheSpyPartyIssueAcceptQuest(fixture.HeroId));
        });

        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkTheSpyPartyIssueQuestAccepted>());
        Assert.Equal(fixture.HeroId, accepted.OwnerId);
        Assert.Equal("player-A", accepted.OwnerControllerId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkTheSpyPartyIssueAcceptRejected>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.False(owner.Issue.IsOngoingWithoutQuest);
            Assert.IsType<TheSpyPartyIssueQuestBehavior.TheSpyPartyIssueQuest>(owner.Issue.IssueQuest);
            Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal("player-A", ownerControllerId);
        });

        // OtherClient (player-B) requests second, for the SAME issue - loses the race, since the server's own
        // copy is no longer IsOngoingWithoutQuest. By this point OtherClient has ALREADY mirrored player-A's
        // winning accept too (the first broadcast reached every client, including this one, before its own
        // rejection round-trip even started) - this is the exact race window Finding 1's fix targets.
        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(OtherClient.NetPeer, new RequestTheSpyPartyIssueAcceptQuest(fixture.HeroId));
        });

        // Still only the one accept ever went out - no second broadcast for the same issue.
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkTheSpyPartyIssueQuestAccepted>());
        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkTheSpyPartyIssueAcceptRejected>());
        Assert.Equal(fixture.HeroId, rejected.OwnerId);

        // The rejection was addressed ONLY to the losing peer - never broadcast, never delivered to the winner.
        Assert.Single(OtherClient.InternalMessages.GetMessages<NetworkTheSpyPartyIssueAcceptRejected>());
        Assert.Empty(Client.InternalMessages.GetMessages<NetworkTheSpyPartyIssueAcceptRejected>());

        // The core assertion this bug broke: BOTH the winner AND the loser must still carry the SAME
        // correctly-mirrored winning quest (never nulled/cancelled by the loser's own rejection handling), and
        // both record the SAME ownership - the losing peer's own request/rejection never overwrote or
        // cancelled it. Pre-fix, OtherClient's RejectAcceptance ran CompleteIssueWithCancel() on this exact,
        // already-correctly-mirrored quest because the old guard (IsOngoingWithoutQuest alone) couldn't tell
        // it apart from OtherClient's own (nonexistent, in this scenario) optimistic accept.
        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.NotNull(owner.Issue);
                Assert.IsType<TheSpyPartyIssueQuestBehavior.TheSpyPartyIssueQuest>(owner.Issue.IssueQuest);
                Assert.True(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("player-A", ownerControllerId);
            });
        }
    }

    [Fact]
    public void RequestTheSpyPartyIssueAcceptQuest_FromUnregisteredRequester_IsRejectedWithoutMutatingTheIssue()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture);

        // Client's peer was never registered/connected to any player - the server cannot resolve who is
        // asking, so IPlayerManager.TryGetPlayer(NetPeer, ...) fails.
        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, new RequestTheSpyPartyIssueAcceptQuest(fixture.HeroId));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkTheSpyPartyIssueQuestAccepted>());
        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkTheSpyPartyIssueAcceptRejected>());
        Assert.Equal(fixture.HeroId, rejected.OwnerId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(owner.Issue.IsOngoingWithoutQuest);
            Assert.False(VillageNeedsToolsIssueOwnership.TryGetOwnerControllerId(owner, out _));
        });
    }
}
