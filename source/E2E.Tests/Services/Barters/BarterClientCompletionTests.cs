using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using GameInterface.Services.Bandits.Messages;
using GameInterface.Services.Bandits.Patches;
using GameInterface.Services.Barters;
using GameInterface.Services.Barters.Messages;
using GameInterface.Services.Barters.Patches;
using GameInterface.Services.Entity;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Barters;

public class BarterClientCompletionTests : MapEventTestBase
{
    private static bool barterCloseObserved;

    public BarterClientCompletionTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void PeaceBarter_AcceptedResult_ClosesUiBeforePresentationFailure()
    {
        const string contextId = "stale-settlement";
        const string requestId = "peace-client-completion";
        var client = Clients.First();
        var (playerHeroId, playerPartyId, targetHeroId, targetPartyId) = CreateBarterParties(client);

        client.Call(() =>
        {
            var barter = CreateBarter(client, playerHeroId, playerPartyId, targetHeroId, targetPartyId);
            SetStaticField(typeof(PeaceBarterPatch), "pendingBarter", barter);
            SetStaticField(typeof(PeaceBarterPatch), "pendingUiActive", true);
            SetStaticField(typeof(PeaceBarterPatch), "pendingRequestId", requestId);
            SetStaticField(typeof(PeaceBarterPatch), "pendingContext", PeaceConversationContext.Settlement);
            SetStaticField(typeof(PeaceBarterPatch), "pendingContextId", contextId);

            AssertAcceptedResultClosesUi(() => PeaceBarterPatch.CompleteRequest(
                new NetworkPeaceBarterResult(
                    contextId,
                    accepted: true,
                    playerGold: 500,
                    requestId: requestId),
                new ThrowingBarterClientPresentation()));
        });
    }

    [Fact]
    public void BanditBarter_AcceptedResult_ClosesUiBeforePresentationFailure()
    {
        const string banditPartyId = "stale-bandit-party";
        const string requestId = "bandit-client-completion";
        var client = Clients.First();
        var (playerHeroId, playerPartyId, targetHeroId, targetPartyId) = CreateBarterParties(client);

        client.Call(() =>
        {
            var barter = CreateBarter(client, playerHeroId, playerPartyId, targetHeroId, targetPartyId);
            SetStaticField(typeof(BanditBarterPatch), "pendingBarter", barter);
            SetStaticField(typeof(BanditBarterPatch), "pendingBanditPartyId", banditPartyId);
            SetStaticField(typeof(BanditBarterPatch), "pendingRequestId", requestId);
            SetStaticField(typeof(BanditBarterPatch), "pendingUiActive", true);

            AssertAcceptedResultClosesUi(() => BanditBarterPatch.CompleteRequest(
                new NetworkBanditBarterResult(
                    banditPartyId,
                    accepted: true,
                    playerGold: 500,
                    requestId: requestId),
                new ThrowingBarterClientPresentation()));
        });
    }

    private (string playerHeroId, string playerPartyId, string targetHeroId, string targetPartyId)
        CreateBarterParties(EnvironmentInstance client)
    {
        const string controllerId = "PlayerOne";
        var playerPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var targetPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        string? playerHeroId = null;
        string? targetHeroId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(targetPartyId, out var targetParty));
            Assert.True(Server.ObjectManager.TryGetId(playerParty.LeaderHero, out playerHeroId));
            Assert.True(Server.ObjectManager.TryGetId(targetParty.LeaderHero, out targetHeroId));
        });

        Assert.NotNull(playerHeroId);
        Assert.NotNull(targetHeroId);
        client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
        RegisterAsPlayerParty(controllerId, playerHeroId!, playerPartyId);
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(playerHeroId!, out var playerHero));
            Game.Current.PlayerTroop = playerHero.CharacterObject;
            Assert.Same(playerHero, Hero.MainHero);
        });

        return (playerHeroId!, playerPartyId, targetHeroId!, targetPartyId);
    }

    private static BarterData CreateBarter(
        EnvironmentInstance client,
        string playerHeroId,
        string playerPartyId,
        string targetHeroId,
        string targetPartyId)
    {
        Assert.True(client.ObjectManager.TryGetObject<Hero>(playerHeroId, out var playerHero));
        Assert.True(client.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerParty));
        Assert.True(client.ObjectManager.TryGetObject<Hero>(targetHeroId, out var targetHero));
        Assert.True(client.ObjectManager.TryGetObject<MobileParty>(targetPartyId, out var targetParty));
        return new BarterData(playerHero, targetHero, playerParty.Party, targetParty.Party, null);
    }

    private static void AssertAcceptedResultClosesUi(Action completeRequest)
    {
        var harmony = new Harmony($"e2e.barter-client-completion.{Guid.NewGuid():N}");
        barterCloseObserved = false;
        BarterManager.Instance.LastBarterIsAccepted = false;
        harmony.Patch(
            AccessTools.Method(typeof(BarterManager), nameof(BarterManager.Close)),
            prefix: new HarmonyMethod(
                typeof(BarterClientCompletionTests),
                nameof(CaptureBarterClose)));

        try
        {
            completeRequest();
            Assert.True(barterCloseObserved);
            Assert.True(BarterManager.Instance.LastBarterIsAccepted);
        }
        finally
        {
            PeaceBarterPatch.ClearPendingRequest();
            BanditBarterPatch.ClearPendingRequest();
            BarterManager.Instance.LastBarterIsAccepted = false;
            harmony.UnpatchAll(harmony.Id);
        }
    }

    private static bool CaptureBarterClose()
    {
        barterCloseObserved = true;
        return false;
    }

    private static void SetStaticField(Type type, string fieldName, object? value)
        => AccessTools.Field(type, fieldName).SetValue(null, value);

    private sealed class ThrowingBarterClientPresentation : IBarterClientPresentation
    {
        public void SynchronizeMainHeroGold(int gold)
            => throw new InvalidOperationException("The client presentation failed after acceptance.");
    }
}
