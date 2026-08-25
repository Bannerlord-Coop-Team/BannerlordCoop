using Autofac;
using Common.Util;
using Coop.Core.Server.Services.Kingdoms.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Patches;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Library;

namespace Coop.IntegrationTests.Kingdoms;

/// <summary>
/// Guards the invariant the decision index messages depend on: every instance's
/// <c>Kingdom._unresolvedDecisions</c> holds the same decisions in the same order as the server's.
/// Asserts the lists themselves, not the messages that carry them.
/// </summary>
/// <remarks>
/// Two collaborators need live campaign state the headless environment does not have, so they are
/// stubbed: <see cref="IKingdomDecisionVoteManager"/> (its RegisterDecision builds a vanilla election
/// that reads stances and heroes) and the campaign event dispatcher (given no receivers). Everything
/// between the vanilla AddDecision patch and each instance's list is the real path.
/// </remarks>
[Collection(KingdomSyncGameThreadCollection.Name)]
public class KingdomDecisionQueueAlignmentTest : IDisposable
{
    private const string KingdomId = "kingdom1";
    private const string ProposerClanId = "clan1";
    private const string ControllerId = "player1";

    private readonly Campaign previousCampaign;

    internal TestEnvironment TestEnvironment { get; }

    public KingdomDecisionQueueAlignmentTest()
    {
        previousCampaign = Campaign.Current;
        Campaign.Current = ObjectHelper.SkipConstructor<Campaign>();
        Campaign.Current.CampaignEventDispatcher = new CampaignEventDispatcher(Array.Empty<CampaignEventReceiver>());
        TestEnvironment = new TestEnvironment(configureInstance: StubVoteManager);
    }

    public void Dispose()
    {
        Campaign.Current = previousCampaign;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// The proposing side is the server here, which is the only side that decides queue-vs-resolve.
    /// One client's clan sits in another kingdom, the case the removed membership filter used to drop.
    /// </summary>
    [Fact]
    public void ServerQueue_IsMirrored_OnEveryClient_AfterMixedAddAndResolve()
    {
        var server = TestEnvironment.Server;
        var outsideClient = TestEnvironment.Clients.First();
        var insideClient = TestEnvironment.Clients.Skip(1).First();
        RegisterKingdomEverywhere();
        PlaceControllerClanOutsideKingdom(outsideClient);

        AddDecisionOnServer("kingdom2");
        AddDecisionOnServer("kingdom3");
        ResolveDecisionOnServer(index: 0);
        AddDecisionOnServer("kingdom4");

        AssertQueue(server, "kingdom3", "kingdom4");
        AssertQueue(outsideClient, "kingdom3", "kingdom4");
        AssertQueue(insideClient, "kingdom3", "kingdom4");
    }

    /// <summary>
    /// A client applying the server's answer must not re-announce it, otherwise the server treats the
    /// mirrored add as a fresh proposal and the two sides trade the same decision forever.
    /// </summary>
    [Fact]
    public void MirroredAdd_IsNotProposedBackToTheServer()
    {
        RegisterKingdomEverywhere();

        AddDecisionOnServer("kingdom2");

        foreach (var client in TestEnvironment.Clients)
        {
            Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkAddDecision>());
        }
    }

    /// <summary>
    /// A decision the server resolved in place never entered its queue, so no client may queue it either.
    /// The client must also not re-run the election: that second evaluation would re-apply outcome actions
    /// the server already replicated, and here it throws because the vanilla election reads campaign state.
    /// </summary>
    [Fact]
    public void ClientQueue_SkipsDecision_TheServerResolvedInPlace()
    {
        RegisterKingdomEverywhere();
        var client = TestEnvironment.Clients.First();

        client.SimulateMessage(this, new AddDecision(
            KingdomId,
            CreateDecisionData("kingdom2"),
            ignoreInfluenceCost: true,
            randomNumber: 0.5f,
            wasQueued: false));

        Assert.Empty(client.GetRegisteredObject<Kingdom>(KingdomId)._unresolvedDecisions);
    }

    private void AddDecisionOnServer(string targetKingdomId)
    {
        var server = TestEnvironment.Server;
        server.Call(() =>
        {
            var kingdom = server.GetRegisteredObject<Kingdom>(KingdomId);
            Assert.True(CreateDecisionData(targetKingdomId)
                .TryGetKingdomDecision(server.ObjectManager, out var decision));
            KingdomPatches.AddDecisionPrefix(kingdom, decision, ignoreInfluenceCost: true);
        });
    }

    /// <summary>
    /// Drives the server side of a resolve. The patch prefix returns true so vanilla removes the entry,
    /// and Harmony is not installed in this environment, so the original call is made here instead.
    /// </summary>
    private void ResolveDecisionOnServer(int index)
    {
        var server = TestEnvironment.Server;
        server.Call(() =>
        {
            var kingdom = server.GetRegisteredObject<Kingdom>(KingdomId);
            var decision = kingdom._unresolvedDecisions[index];
            Assert.True(KingdomPatches.RemoveDecisionPrefix(kingdom, decision));
            kingdom._unresolvedDecisions.Remove(decision);
        });
    }

    private static void AssertQueue(EnvironmentInstance instance, params string[] expectedTargetKingdomIds)
    {
        var decisions = instance.GetRegisteredObject<Kingdom>(KingdomId)._unresolvedDecisions;
        Assert.Equal(expectedTargetKingdomIds.Length, decisions.Count);
        for (int index = 0; index < expectedTargetKingdomIds.Length; index++)
        {
            var decision = Assert.IsType<DeclareWarDecision>(decisions[index]);
            Assert.Same(
                instance.GetRegisteredObject<Kingdom>(expectedTargetKingdomIds[index]),
                decision.FactionToDeclareWarOn);
        }
    }

    private void RegisterKingdomEverywhere()
    {
        foreach (var instance in TestEnvironment.Clients.Prepend(TestEnvironment.Server))
        {
            var kingdom = instance.CreateRegisteredObject<Kingdom>(KingdomId);
            kingdom._unresolvedDecisions = new MBList<KingdomDecision>();
            instance.CreateRegisteredObject<Clan>(ProposerClanId)._kingdom = kingdom;
            instance.CreateRegisteredObject<Kingdom>("kingdom2");
            instance.CreateRegisteredObject<Kingdom>("kingdom3");
            instance.CreateRegisteredObject<Kingdom>("kingdom4");
        }
    }

    private static void PlaceControllerClanOutsideKingdom(EnvironmentInstance client)
    {
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client.Resolve<IPlayerManager>().AddPlayer(
            new Player(ControllerId, "hero1", "party1", ProposerClanId, "character1"));
        client.GetRegisteredObject<Clan>(ProposerClanId)._kingdom =
            client.CreateRegisteredObject<Kingdom>("kingdom_other");
    }

    private static KingdomDecisionData CreateDecisionData(string targetKingdomId)
    {
        return new DeclareWarDecisionData(ProposerClanId, KingdomId, 0, false, false, false, targetKingdomId);
    }

    private static void StubVoteManager(ContainerBuilder builder)
    {
        var voteManager = new Mock<IKingdomDecisionVoteManager>();
        voteManager.Setup(manager => manager.HasEligiblePlayerClan(It.IsAny<KingdomDecision>())).Returns(true);
        builder.RegisterInstance(voteManager.Object).As<IKingdomDecisionVoteManager>().SingleInstance();
    }
}
