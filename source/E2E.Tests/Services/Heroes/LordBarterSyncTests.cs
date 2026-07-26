using Common.Network;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using GameInterface.Services.Barters;
using GameInterface.Services.Barters.Handlers;
using GameInterface.Services.Barters.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MobilePartyAIs.Patches;
using GameInterface.Services.Players;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Heroes;

public class LordBarterSyncTests : MapEventTestBase
{
    private static int observedBarterAcceptedDispatches;
    private static List<MobileParty>? safePassageOpponentParties;

    public LordBarterSyncTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void GenericLordBarter_DuplicateRequest_AppliesPaymentOnceAndReplaysAcceptance()
    {
        const int initialPlayerGold = 1_000_000;
        const int initialTargetGold = 50;
        const int offeredGold = 500_000;
        var client = Clients.First();
        var player = CreatePartyWithRegisteredLeader();
        var target = CreatePartyWithRegisteredLeader();
        var requestId = Guid.NewGuid().ToString("N");
        var harmony = new Harmony($"e2e.lord-barter-accepted.{Guid.NewGuid():N}");
        observedBarterAcceptedDispatches = 0;

        RegisterPlayer(client, player.HeroId, player.MobilePartyId);
        SetMainHero(player.HeroId);
        Server.Call(() =>
        {
            new GoldBarterBehavior().RegisterEvents();
            harmony.Patch(
                AccessTools.Method(
                    typeof(CampaignEventDispatcher),
                    nameof(CampaignEventDispatcher.OnBarterAccepted)),
                prefix: new HarmonyMethod(
                    typeof(LordBarterSyncTests),
                    nameof(ObserveBarterAcceptedDispatch)));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(target.HeroId, out var targetHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(target.MobilePartyId, out var targetParty));
            playerHero.Gold = initialPlayerGold;
            targetHero.Gold = initialTargetGold;
            Assert.True(ConversationPartyHold.TryEngage(
                Server.Resolve<ConversationPartyTracker>(),
                client.NetPeer,
                player.PartyId,
                targetParty,
                target.PartyId,
                engagerIsDefender: true));
        });
        Server.NetworkSentMessages.Clear();

        try
        {
            client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkAuthorizeLordBarter(
                requestId,
                target.HeroId,
                PeaceConversationContext.MapParty,
                target.PartyId,
                LordBarterKind.Generic)));
            var request = new NetworkRequestLordBarter(
                target.HeroId,
                PeaceConversationContext.MapParty,
                target.PartyId,
                LordBarterKind.Generic,
                new[]
                {
                    new PeaceBarterTerm(
                        PeaceBarterTermType.Gold,
                        player.HeroId,
                        null,
                        null,
                        true,
                        offeredGold),
                },
                requestId);

            client.Call(() => client.Resolve<INetwork>().SendAll(request));
            client.Call(() => client.Resolve<INetwork>().SendAll(request));
            TestEnvironment.FlushCoalescer();

            var results = Server.NetworkSentMessages.GetMessages<NetworkLordBarterResult>().ToList();
            Assert.Equal(2, results.Count);
            Assert.All(results, result =>
            {
                Assert.True(result.Accepted, result.Reason);
                Assert.Equal(requestId, result.RequestId);
                Assert.Equal(initialPlayerGold - offeredGold, result.PlayerGold);
            });
            Assert.Equal(1, observedBarterAcceptedDispatches);
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(target.HeroId, out var targetHero));
                Assert.Equal(initialPlayerGold - offeredGold, playerHero.Gold);
                Assert.Equal(initialTargetGold + offeredGold, targetHero.Gold);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            observedBarterAcceptedDispatches = 0;
        }
    }

    [Fact]
    public void OverpayRelationBonus_UsesNativeBarterModel()
    {
        const int overpayAmount = 500_000;
        var player = CreatePartyWithRegisteredLeader();
        var target = CreatePartyWithRegisteredLeader();
        var commonKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        SetMainHero(player.HeroId);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(target.HeroId, out var targetHero));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(commonKingdomId, out var commonKingdom));
            playerHero.Clan.Kingdom = commonKingdom;
            targetHero.Clan.Kingdom = commonKingdom;
            var initialRelation = CharacterRelationManager.GetHeroRelation(targetHero, playerHero);
            var expectedBonus = Campaign.Current.Models.BarterModel
                .CalculateOverpayRelationIncreaseCosts(targetHero, overpayAmount);

            Assert.Equal(3, expectedBonus);
            LordBarterHandler.ApplyOverpayRelationBonus(playerHero, targetHero, overpayAmount);

            Assert.Equal(
                initialRelation + expectedBonus,
                CharacterRelationManager.GetHeroRelation(targetHero, playerHero));
        });
    }

    [Fact]
    public void SafePassage_ProtectsEveryEncounterOpponent()
    {
        var player = CreatePartyWithRegisteredLeader();
        var target = CreatePartyWithRegisteredLeader();
        var nearbyOpponent = CreatePartyWithRegisteredLeader();
        var harmony = new Harmony($"e2e.lord-safe-passage.{Guid.NewGuid():N}");

        SetMainHero(player.HeroId);
        SetMockPlayerEncounter(Server, target.MobilePartyId);
        try
        {
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty));
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(target.MobilePartyId, out var targetParty));
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                    nearbyOpponent.MobilePartyId,
                    out var nearbyOpponentParty));
                safePassageOpponentParties = new List<MobileParty>
                {
                    targetParty,
                    nearbyOpponentParty,
                };
                harmony.Patch(
                    AccessTools.Method(
                        Campaign.Current.Models.EncounterModel.GetType(),
                        "FindNonAttachedNpcPartiesWhoWillJoinPlayerEncounter"),
                    prefix: new HarmonyMethod(
                        typeof(LordBarterSyncTests),
                        nameof(SupplySafePassageOpponentParties)));

                using (new BarterPlayerContext(playerHero, playerParty))
                    LordBarterHandler.ApplySafePassage(targetParty, playerParty);

                var protectedAttackers = DefaultMobilePartyAIModelPatches
                    .GetPersistedAttackProtections()
                    .Where(protection => protection.TargetParty == playerParty)
                    .Select(protection => protection.AttackerParty)
                    .ToList();
                Assert.Contains(targetParty, protectedAttackers);
                Assert.Contains(nearbyOpponentParty, protectedAttackers);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            safePassageOpponentParties = null;
            Server.Call(DefaultMobilePartyAIModelPatches.ResetPersistedAttackProtections);
        }
    }

    [Fact]
    public void JoinKingdomBarter_PlayerChangesKingdomAfterAuthorization_IsRejected()
    {
        var client = Clients.First();
        var player = CreatePartyWithRegisteredLeader();
        var target = CreatePartyWithRegisteredLeader();
        var authorizedKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var changedKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetOriginalKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var requestId = Guid.NewGuid().ToString("N");

        RegisterPlayer(client, player.HeroId, player.MobilePartyId);
        SetMainHero(player.HeroId);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(target.HeroId, out var targetHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(target.MobilePartyId, out var targetParty));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(authorizedKingdomId, out var authorizedKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetOriginalKingdomId, out var targetOriginalKingdom));
            playerHero.Clan.Kingdom = authorizedKingdom;
            targetHero.Clan.Kingdom = targetOriginalKingdom;
            Assert.True(ConversationPartyHold.TryEngage(
                Server.Resolve<ConversationPartyTracker>(),
                client.NetPeer,
                player.PartyId,
                targetParty,
                target.PartyId,
                engagerIsDefender: true));
        });

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkAuthorizeLordBarter(
            requestId,
            target.HeroId,
            PeaceConversationContext.MapParty,
            target.PartyId,
            LordBarterKind.JoinKingdomAsClan,
            authorizedKingdomId)));
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(changedKingdomId, out var changedKingdom));
            playerHero.Clan.Kingdom = changedKingdom;
        });
        Server.NetworkSentMessages.Clear();

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestLordBarter(
            target.HeroId,
            PeaceConversationContext.MapParty,
            target.PartyId,
            LordBarterKind.JoinKingdomAsClan,
            Array.Empty<PeaceBarterTerm>(),
            requestId)));

        var result = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkLordBarterResult>());
        Assert.False(result.Accepted);
        Assert.Contains("not eligible", result.Reason);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(target.HeroId, out var targetHero));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(changedKingdomId, out var changedKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetOriginalKingdomId, out var targetOriginalKingdom));
            Assert.Same(changedKingdom, playerHero.Clan.Kingdom);
            Assert.Same(targetOriginalKingdom, targetHero.Clan.Kingdom);
        });
    }

    private (string HeroId, string MobilePartyId, string PartyId) CreatePartyWithRegisteredLeader()
    {
        var mobilePartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        string heroId = null;
        string partyId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var party));
            Assert.NotNull(party.LeaderHero);
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out heroId));
            Assert.True(Server.ObjectManager.TryGetId(party.Party, out partyId));
        });
        return (heroId, mobilePartyId, partyId);
    }

    private void RegisterPlayer(EnvironmentInstance client, string heroId, string mobilePartyId)
    {
        const string controllerId = "PlayerOne";
        client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);
        RegisterAsPlayerParty(controllerId, heroId, mobilePartyId);
        Server.Resolve<IPlayerManager>().SetPeer(controllerId, client.NetPeer);
    }

    private void SetMainHero(string heroId)
    {
        void Set(EnvironmentInstance instance)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Game.Current.PlayerTroop = hero.CharacterObject;
            });
        }

        Set(Server);
        foreach (var client in Clients)
            Set(client);
    }

    private static void ObserveBarterAcceptedDispatch()
        => observedBarterAcceptedDispatches++;

    private static bool SupplySafePassageOpponentParties(
        List<MobileParty> partiesToJoinPlayerSide,
        List<MobileParty> partiesToJoinEnemySide)
    {
        if (safePassageOpponentParties != null)
            partiesToJoinEnemySide.AddRange(safePassageOpponentParties);
        return false;
    }
}
