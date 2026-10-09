using Common;
using Common.Network;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using GameInterface.Services.Barters;
using GameInterface.Services.Barters.Handlers;
using GameInterface.Services.Barters.Messages;
using GameInterface.Services.Barters.Patches;
using GameInterface.Services.Entity;
using GameInterface.Services.Locations.Conversations;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MobilePartyAIs.Patches;
using GameInterface.Services.Players;
using GameInterface.Services.UI.Notifications.Messages;
using GameInterface.Services.Villages.Interfaces;
using HarmonyLib;
using Moq;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.SceneInformationPopupTypes;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Heroes;

public class LordBarterSyncTests : MapEventTestBase
{
    private static int observedBarterAcceptedDispatches;
    private static IFaction? safePassagePlayerFaction;
    private static IFaction? safePassageTargetFaction;
    private static IFaction? safePassageNearbyFaction;
    private static SceneNotificationData? shownJoinKingdomScene;
    private static readonly List<string> shownMessages = new();
    private static int serverJoinKingdomPrice;
    private static int clientJoinKingdomPrice;

    public LordBarterSyncTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void GenericLordBarter_RejectedRetryAndDuplicate_AppliesPaymentOnce()
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
            var rejectedRequest = new NetworkRequestLordBarter(
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
                        initialPlayerGold + 1),
                },
                requestId);
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

            client.Call(() => client.Resolve<INetwork>().SendAll(rejectedRequest));
            client.Call(() => client.Resolve<INetwork>().SendAll(request));
            client.Call(() => client.Resolve<INetwork>().SendAll(request));
            TestEnvironment.FlushCoalescer();

            var results = Server.NetworkSentMessages.GetMessages<NetworkLordBarterResult>().ToList();
            Assert.Equal(3, results.Count);
            Assert.False(results[0].Accepted);
            Assert.Equal(requestId, results[0].RequestId);
            Assert.All(results.Skip(1), result =>
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

        Server.PumpGameThread();
    }

    [Fact]
    public void StationarySettlementConversation_ClientResolversUseSettlementContext()
    {
        var client = Clients.First();
        var player = CreatePartyWithRegisteredLeader();
        var targetHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        SetMainHero(player.HeroId);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(targetHeroId, out var targetHero));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            playerParty.CurrentSettlement = settlement;
            targetHero.StayingInSettlement = settlement;
        });

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(targetHeroId, out var targetHero));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty));
            Assert.Null(targetHero.PartyBelongedTo);

            var barter = new BarterData(playerHero, targetHero, playerParty.Party, null, null);
            Assert.True(LordBarterPatch.TryGetConversationContext(
                barter,
                client.ObjectManager,
                out var lordContext,
                out var lordContextId));
            Assert.Equal(PeaceConversationContext.Settlement, lordContext);
            Assert.Equal(settlementId, lordContextId);

            Assert.True(PeaceBarterPatch.TryGetConversationContext(
                barter,
                client.ObjectManager,
                out var peaceContext,
                out var peaceContextId));
            Assert.Equal(PeaceConversationContext.Settlement, peaceContext);
            Assert.Equal(settlementId, peaceContextId);

            Assert.True(MarriageBarterPatch.TryGetConversationContext(
                barter,
                client.ObjectManager,
                out var marriageContext,
                out var marriageContextId));
            Assert.Equal(MarriageConversationContext.Settlement, marriageContext);
            Assert.Equal(settlementId, marriageContextId);
        });
    }

    /// <summary>
    /// A lord standing INSIDE a settlement still leads an active party, so the settlement must be
    /// resolved before the map party. MobileParty.IsActive is only cleared by RemoveParty, player
    /// captivity and load - entering a settlement leaves it true - so resolving the map party first
    /// classifies every settlement-menu barter as MapParty. The server then requires a conversation
    /// hold that a settlement menu never acquires, and refuses the barter after the player has
    /// already played out the conversation.
    /// </summary>
    /// <remarks>
    /// StationarySettlementConversation_ClientResolversUseSettlementContext above does NOT cover this:
    /// it asserts the target has no party at all, so the map-party branch cannot match either way.
    /// </remarks>
    [Fact]
    public void SettlementConversation_WithLordLeadingAnActiveParty_StillResolvesSettlementContext()
    {
        var client = Clients.First();
        var player = CreatePartyWithRegisteredLeader();
        var target = CreatePartyWithRegisteredLeader();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        SetMainHero(player.HeroId);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(target.MobilePartyId, out var targetParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            playerParty.CurrentSettlement = settlement;
            targetParty.CurrentSettlement = settlement;
        });

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(target.HeroId, out var targetHero));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(target.MobilePartyId, out var targetParty));

            // The premise of the bug: the lord's party is inside the settlement AND still active.
            Assert.True(targetParty.IsActive);
            Assert.Same(playerParty.CurrentSettlement, targetParty.CurrentSettlement);

            var barter = new BarterData(playerHero, targetHero, playerParty.Party, targetParty.Party, null);

            Assert.True(LordBarterPatch.TryGetConversationContext(
                barter, client.ObjectManager, out var lordContext, out var lordContextId));
            Assert.Equal(PeaceConversationContext.Settlement, lordContext);
            Assert.Equal(settlementId, lordContextId);

            Assert.True(PeaceBarterPatch.TryGetConversationContext(
                barter, client.ObjectManager, out var peaceContext, out var peaceContextId));
            Assert.Equal(PeaceConversationContext.Settlement, peaceContext);
            Assert.Equal(settlementId, peaceContextId);
        });
    }

    /// <summary>
    /// The reordering above must not steal an ordinary map conversation: on the map neither side has a
    /// CurrentSettlement, so the settlement branch cannot match and the map party still wins.
    /// </summary>
    [Fact]
    public void MapConversation_WithNeitherSideInASettlement_StillResolvesMapPartyContext()
    {
        var client = Clients.First();
        var player = CreatePartyWithRegisteredLeader();
        var target = CreatePartyWithRegisteredLeader();

        SetMainHero(player.HeroId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(target.HeroId, out var targetHero));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(target.MobilePartyId, out var targetParty));

            Assert.Null(playerParty.CurrentSettlement);
            Assert.Null(targetParty.CurrentSettlement);

            var barter = new BarterData(playerHero, targetHero, playerParty.Party, targetParty.Party, null);

            Assert.True(LordBarterPatch.TryGetConversationContext(
                barter, client.ObjectManager, out var lordContext, out _));
            Assert.Equal(PeaceConversationContext.MapParty, lordContext);

            Assert.True(PeaceBarterPatch.TryGetConversationContext(
                barter, client.ObjectManager, out var peaceContext, out _));
            Assert.Equal(PeaceConversationContext.MapParty, peaceContext);
        });
    }

    /// <summary>
    /// A settlement-menu conversation acquires no engagement, so authority comes from co-location - and
    /// co-location is NOT exclusive: every player standing in the settlement satisfies it. Without a
    /// reservation, two kingdom leaders could each authorize, each pay, and each move the same clan in
    /// turn, which is the very duplication the map-party hold exists to prevent.
    /// </summary>
    [Fact]
    public void SettlementConversation_WhenAnotherPlayerHoldsTheLord_RefusesTheSecondAuthorization()
    {
        const int initialPlayerGold = 1_000_000;
        const int offeredGold = 100_000;
        var clientOne = Clients.First();
        var clientTwo = Clients.Skip(1).First();
        var playerOne = CreatePartyWithRegisteredLeader();
        var playerTwo = CreatePartyWithRegisteredLeader();
        var targetHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var requestOne = Guid.NewGuid().ToString("N");
        var requestTwo = Guid.NewGuid().ToString("N");

        RegisterPlayer(clientOne, playerOne.HeroId, playerOne.MobilePartyId, "PlayerOne");
        RegisterPlayer(clientTwo, playerTwo.HeroId, playerTwo.MobilePartyId, "PlayerTwo");
        Server.Call(() =>
        {
            new GoldBarterBehavior().RegisterEvents();
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(playerOne.HeroId, out var heroOne));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(playerTwo.HeroId, out var heroTwo));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(targetHeroId, out var targetHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerOne.MobilePartyId, out var partyOne));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerTwo.MobilePartyId, out var partyTwo));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            heroOne.Gold = initialPlayerGold;
            heroTwo.Gold = initialPlayerGold;
            // Both players are standing in the same settlement as the lord, so both satisfy co-location.
            partyOne.CurrentSettlement = settlement;
            partyTwo.CurrentSettlement = settlement;
            targetHero.StayingInSettlement = settlement;
        });
        Server.NetworkSentMessages.Clear();

        clientOne.Call(() => clientOne.Resolve<INetwork>().SendAll(new NetworkAuthorizeLordBarter(
            requestOne, targetHeroId, PeaceConversationContext.Settlement, settlementId, LordBarterKind.Generic)));
        clientTwo.Call(() => clientTwo.Resolve<INetwork>().SendAll(new NetworkAuthorizeLordBarter(
            requestTwo, targetHeroId, PeaceConversationContext.Settlement, settlementId, LordBarterKind.Generic)));

        // The second player never got an authorization, so their request must be refused outright.
        clientTwo.Call(() => clientTwo.Resolve<INetwork>().SendAll(new NetworkRequestLordBarter(
            targetHeroId, PeaceConversationContext.Settlement, settlementId, LordBarterKind.Generic,
            new[] { new PeaceBarterTerm(PeaceBarterTermType.Gold, playerTwo.HeroId, null, null, true, offeredGold) },
            requestTwo)));
        TestEnvironment.FlushCoalescer();

        var refused = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkLordBarterResult>());
        Assert.False(refused.Accepted);
        Server.NetworkSentMessages.Clear();

        // The holder is unaffected: their barter still goes through.
        clientOne.Call(() => clientOne.Resolve<INetwork>().SendAll(new NetworkRequestLordBarter(
            targetHeroId, PeaceConversationContext.Settlement, settlementId, LordBarterKind.Generic,
            new[] { new PeaceBarterTerm(PeaceBarterTermType.Gold, playerOne.HeroId, null, null, true, offeredGold) },
            requestOne)));
        TestEnvironment.FlushCoalescer();

        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkLordBarterResult>());
        Assert.True(accepted.Accepted);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(playerOne.HeroId, out var heroOne));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(playerTwo.HeroId, out var heroTwo));
            // Paid exactly once, by the holder.
            Assert.Equal(initialPlayerGold - offeredGold, heroOne.Gold);
            Assert.Equal(initialPlayerGold, heroTwo.Gold);
        });

        Server.PumpGameThread();
    }

    /// <summary>
    /// The reservation must not lock a lord for the rest of the session. Cancelling releases it, which is
    /// the path a player takes by backing out of the conversation; expiry uses the same released-if-not-live
    /// check in IsTargetHeldByAnotherPeer.
    /// </summary>
    [Fact]
    public void SettlementConversation_AfterTheHolderCancels_TheLordIsAvailableAgain()
    {
        var clientOne = Clients.First();
        var clientTwo = Clients.Skip(1).First();
        var playerOne = CreatePartyWithRegisteredLeader();
        var playerTwo = CreatePartyWithRegisteredLeader();
        var targetHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var requestOne = Guid.NewGuid().ToString("N");
        var requestTwo = Guid.NewGuid().ToString("N");

        RegisterPlayer(clientOne, playerOne.HeroId, playerOne.MobilePartyId, "PlayerOne");
        RegisterPlayer(clientTwo, playerTwo.HeroId, playerTwo.MobilePartyId, "PlayerTwo");
        Server.Call(() =>
        {
            new GoldBarterBehavior().RegisterEvents();
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(targetHeroId, out var targetHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerOne.MobilePartyId, out var partyOne));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerTwo.MobilePartyId, out var partyTwo));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            partyOne.CurrentSettlement = settlement;
            partyTwo.CurrentSettlement = settlement;
            targetHero.StayingInSettlement = settlement;
        });

        clientOne.Call(() => clientOne.Resolve<INetwork>().SendAll(new NetworkAuthorizeLordBarter(
            requestOne, targetHeroId, PeaceConversationContext.Settlement, settlementId, LordBarterKind.Generic)));
        clientOne.Call(() => clientOne.Resolve<INetwork>().SendAll(
            new NetworkCancelLordBarterAuthorization(requestOne)));

        clientTwo.Call(() => clientTwo.Resolve<INetwork>().SendAll(new NetworkAuthorizeLordBarter(
            requestTwo, targetHeroId, PeaceConversationContext.Settlement, settlementId, LordBarterKind.Generic)));
        Server.NetworkSentMessages.Clear();

        clientTwo.Call(() => clientTwo.Resolve<INetwork>().SendAll(new NetworkRequestLordBarter(
            targetHeroId, PeaceConversationContext.Settlement, settlementId, LordBarterKind.Generic,
            Array.Empty<PeaceBarterTerm>(), requestTwo)));
        TestEnvironment.FlushCoalescer();

        var result = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkLordBarterResult>());
        Assert.True(result.Accepted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GenericLordBarter_StationarySettlementConversation_ValidatesCurrentPresence(bool targetLeaves)
    {
        const int initialPlayerGold = 1_000_000;
        const int initialTargetGold = 50;
        const int offeredGold = 500_000;
        var client = Clients.First();
        var player = CreatePartyWithRegisteredLeader();
        var targetHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var requestId = Guid.NewGuid().ToString("N");

        RegisterPlayer(client, player.HeroId, player.MobilePartyId);
        SetMainHero(player.HeroId);
        Server.Call(() =>
        {
            new GoldBarterBehavior().RegisterEvents();
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(targetHeroId, out var targetHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            playerHero.Gold = initialPlayerGold;
            targetHero.Gold = initialTargetGold;
            playerParty.CurrentSettlement = settlement;
            targetHero.StayingInSettlement = settlement;
            Assert.Null(targetHero.PartyBelongedTo);
        });
        Server.NetworkSentMessages.Clear();

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkAuthorizeLordBarter(
            requestId,
            targetHeroId,
            PeaceConversationContext.Settlement,
            settlementId,
            LordBarterKind.Generic)));
        if (targetLeaves)
        {
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(targetHeroId, out var targetHero));
                targetHero.StayingInSettlement = null;
            });
        }

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestLordBarter(
            targetHeroId,
            PeaceConversationContext.Settlement,
            settlementId,
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
            requestId)));
        TestEnvironment.FlushCoalescer();

        var result = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkLordBarterResult>());
        Assert.Equal(!targetLeaves, result.Accepted);
        if (targetLeaves)
            Assert.Contains("settlement conversation", result.Reason);
        else
            Assert.Equal(initialPlayerGold - offeredGold, result.PlayerGold);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(targetHeroId, out var targetHero));
            Assert.Equal(targetLeaves ? initialPlayerGold : initialPlayerGold - offeredGold, playerHero.Gold);
            Assert.Equal(targetLeaves ? initialTargetGold : initialTargetGold + offeredGold, targetHero.Gold);
        });

        Server.PumpGameThread();
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
    public void SafePassage_WithoutPlayerEncounter_IsAccepted()
    {
        const int initialPlayerGold = 1_000_000;
        const int offeredGold = 900_000;
        var client = Clients.First();
        var player = CreatePartyWithRegisteredLeader();
        var target = CreatePartyWithRegisteredLeader();
        var requestId = Guid.NewGuid().ToString("N");

        RegisterPlayer(client, player.HeroId, player.MobilePartyId);
        SetMainHero(player.HeroId);
        try
        {
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(target.HeroId, out var targetHero));
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(target.MobilePartyId, out var targetParty));

                new GoldBarterBehavior().RegisterEvents();
                playerHero.Gold = initialPlayerGold;
                VillageHostileFactionStanceHelper.ApplyWarStance(
                    playerHero.MapFaction,
                    targetHero.MapFaction);
                Assert.Null(PlayerEncounter.Current);
                Assert.True(FactionManager.IsAtWarAgainstFaction(
                    playerHero.MapFaction,
                    targetHero.MapFaction));
                Assert.True(ConversationPartyHold.TryEngage(
                    Server.Resolve<ConversationPartyTracker>(),
                    client.NetPeer,
                    player.PartyId,
                    targetParty,
                    target.PartyId,
                    engagerIsDefender: true));
                Assert.True(Server.Resolve<ConversationPartyTracker>()
                    .TryGetEngagement(client.NetPeer, out var engagement));
                Assert.True(engagement.EngagerIsDefender);
            });
            Server.NetworkSentMessages.Clear();

            client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkAuthorizeLordBarter(
                requestId,
                target.HeroId,
                PeaceConversationContext.MapParty,
                target.PartyId,
                LordBarterKind.SafePassage)));
            client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestLordBarter(
                target.HeroId,
                PeaceConversationContext.MapParty,
                target.PartyId,
                LordBarterKind.SafePassage,
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
                requestId)));

            var result = Assert.Single(
                Server.NetworkSentMessages.GetMessages<NetworkLordBarterResult>());
            Assert.True(result.Accepted, result.Reason);
            Assert.Equal(initialPlayerGold - offeredGold, result.PlayerGold);
            Server.Call(() =>
            {
                Assert.Null(PlayerEncounter.Current);
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                    player.MobilePartyId,
                    out var playerParty));
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                    target.MobilePartyId,
                    out var targetParty));
                var protectedAttackers = DefaultMobilePartyAIModelPatches
                    .GetPersistedAttackProtections()
                    .Where(protection => protection.TargetParty == playerParty)
                    .Select(protection => protection.AttackerParty)
                    .ToList();
                Assert.Contains(targetParty, protectedAttackers);
            });
        }
        finally
        {
            Server.Call(DefaultMobilePartyAIModelPatches.ResetPersistedAttackProtections);
        }

        Server.PumpGameThread();
    }

    [Fact]
    public void SafePassage_ProtectsEveryResolvedOpponentWithoutPlayerEncounter()
    {
        var player = CreatePartyWithRegisteredLeader();
        var target = CreatePartyWithRegisteredLeader();
        var nearbyOpponent = CreatePartyWithRegisteredLeader();
        var playerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var targetClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var nearbyOpponentClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var harmony = new Harmony($"e2e.lord-safe-passage-parties.{Guid.NewGuid():N}");

        try
        {
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                    player.MobilePartyId,
                    out var playerParty));
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                    target.MobilePartyId,
                    out var targetParty));
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                    nearbyOpponent.MobilePartyId,
                    out var nearbyOpponentParty));
                Assert.True(Server.ObjectManager.TryGetObject<Clan>(
                    playerClanId,
                    out var playerClan));
                Assert.True(Server.ObjectManager.TryGetObject<Clan>(
                    targetClanId,
                    out var targetClan));
                Assert.True(Server.ObjectManager.TryGetObject<Clan>(
                    nearbyOpponentClanId,
                    out var nearbyOpponentClan));

                playerParty._actualClan = playerClan;
                targetParty._actualClan = targetClan;
                nearbyOpponentParty._actualClan = nearbyOpponentClan;
                Assert.NotSame(playerParty.MapFaction, targetParty.MapFaction);
                Assert.NotSame(playerParty.MapFaction, nearbyOpponentParty.MapFaction);
                Assert.NotSame(targetParty.MapFaction, nearbyOpponentParty.MapFaction);
                safePassagePlayerFaction = playerParty.MapFaction;
                safePassageTargetFaction = targetParty.MapFaction;
                safePassageNearbyFaction = nearbyOpponentParty.MapFaction;
                harmony.Patch(
                    AccessTools.Method(
                        typeof(FactionManager),
                        nameof(FactionManager.IsAtWarAgainstFaction)),
                    prefix: new HarmonyMethod(
                        typeof(LordBarterSyncTests),
                        nameof(SupplySafePassageWarState)));
                Assert.Null(PlayerEncounter.Current);
                Assert.Null(nearbyOpponentParty.AttachedTo);
                Assert.DoesNotContain(nearbyOpponentParty, targetParty.AttachedParties);

                var safePassageParties = Server
                    .Resolve<SafePassagePartyResolver>()
                    .ResolveFromCandidates(
                        playerParty,
                        targetParty,
                        new[] { targetParty, nearbyOpponentParty });
                Assert.Contains(nearbyOpponentParty, safePassageParties.OpponentSide);
                Server.Resolve<LordBarterHandler>().ApplySafePassage(
                    targetParty,
                    playerParty,
                    safePassageParties.OpponentSide);

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
            safePassagePlayerFaction = null;
            safePassageTargetFaction = null;
            safePassageNearbyFaction = null;
            Server.Call(DefaultMobilePartyAIModelPatches.ResetPersistedAttackProtections);
        }
    }

    [Fact]
    public void SafePassage_OwnFactionSiege_ChargesInfluenceOnServer()
    {
        const float initialInfluence = 100f;
        var player = CreatePartyWithRegisteredLeader();
        var target = CreatePartyWithRegisteredLeader();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var siegeEventId = CreateSyntheticSiegeEvent();

        try
        {
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(
                    player.HeroId,
                    out var playerHero));
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                    player.MobilePartyId,
                    out var playerParty));
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                    target.MobilePartyId,
                    out var targetParty));
                Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(
                    kingdomId,
                    out var kingdom));
                Assert.True(Server.ObjectManager.TryGetObject<Town>(
                    townId,
                    out var town));
                Assert.True(Server.ObjectManager.TryGetObject<SiegeEvent>(
                    siegeEventId,
                    out var siegeEvent));

                playerHero.Clan.Kingdom = kingdom;
                kingdom.RulingClan = playerHero.Clan;
                playerHero.Clan.Influence = initialInfluence;

                var settlement = siegeEvent.BesiegedSettlement;
                settlement.SetSettlementComponent(town);
                town.OwnerClan = playerHero.Clan;
                town.IsOwnerUnassigned = false;

                var besiegerCamp = siegeEvent.BesiegerCamp;
                besiegerCamp._faction = targetParty.MapFaction;
                besiegerCamp._besiegerParties.Add(targetParty);
                targetParty._besiegerCamp = besiegerCamp;
                VillageHostileFactionStanceHelper.ApplyWarStance(
                    playerParty.MapFaction,
                    targetParty.MapFaction);
                playerParty.CurrentSettlement = settlement;

                Assert.True(besiegerCamp.HasInvolvedPartyForEventType(targetParty.Party));
                Assert.True(settlement.HasInvolvedPartyForEventType(playerParty.Party));
                Assert.Equal(playerParty.MapFaction, settlement.MapFaction);

                Server.Resolve<LordBarterHandler>().ApplySafePassage(
                    targetParty,
                    playerParty,
                    new[] { targetParty });

                Assert.Equal(initialInfluence - 10f, playerHero.Clan.Influence);
            });
        }
        finally
        {
            Server.Call(DefaultMobilePartyAIModelPatches.ResetPersistedAttackProtections);
        }
    }

    [Fact]
    public void SafePassage_Besieger_ClearsCampOnServer()
    {
        var player = CreatePartyWithRegisteredLeader();
        var target = CreatePartyWithRegisteredLeader();
        var siegeEventId = CreateSyntheticSiegeEvent();

        try
        {
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                    player.MobilePartyId,
                    out var playerParty));
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(
                    target.MobilePartyId,
                    out var targetParty));
                Assert.True(Server.ObjectManager.TryGetObject<SiegeEvent>(
                    siegeEventId,
                    out var siegeEvent));

                var besiegerCamp = siegeEvent.BesiegerCamp;
                besiegerCamp._besiegerParties.Add(playerParty);
                playerParty._besiegerCamp = besiegerCamp;
                Assert.True(besiegerCamp.HasInvolvedPartyForEventType(playerParty.Party));

                Server.Resolve<LordBarterHandler>().ApplySafePassage(
                    targetParty,
                    playerParty,
                    new[] { targetParty });

                Assert.Null(playerParty.BesiegerCamp);
            }, new[]
            {
                AccessTools.Method(
                    typeof(MobileParty),
                    nameof(MobileParty.OnPartyLeftSiegeInternal)),
            });
        }
        finally
        {
            Server.Call(DefaultMobilePartyAIModelPatches.ResetPersistedAttackProtections);
        }
    }

    [Fact]
    public void ClanChangedFactionNotification_ForDefectingClan_RestoresCulturesAndShowsJoinKingdomScene()
    {
        var client = Clients.First();
        var player = CreatePartyWithRegisteredLeader();
        var target = CreatePartyWithRegisteredLeader();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var harmony = new Harmony($"e2e.join-kingdom-scene.{Guid.NewGuid():N}");
        string? targetClanId = null;
        shownJoinKingdomScene = null;

        SetMainHero(player.HeroId);
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(target.HeroId, out var targetHero));
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client.ObjectManager.TryGetId(targetHero.Clan, out targetClanId));
            Assert.NotNull(playerHero.Culture);
            Assert.NotNull(targetHero.Culture);

            using (new AllowedThread())
            {
                Campaign.Current.PlayerDefaultFaction = playerHero.Clan;
                playerHero.Clan._kingdom = kingdom;
                targetHero.Clan._kingdom = kingdom;
                kingdom._rulingClan = playerHero.Clan;
                ((BasicCharacterObject)playerHero.CharacterObject).Culture = null;
                ((BasicCharacterObject)targetHero.CharacterObject).Culture = null;
            }

            harmony.Patch(
                AccessTools.Method(
                    typeof(MBInformationManager),
                    nameof(MBInformationManager.ShowSceneNotification),
                    new[] { typeof(SceneNotificationData) }),
                prefix: new HarmonyMethod(
                    typeof(LordBarterSyncTests),
                    nameof(CaptureJoinKingdomScene)));
        });

        try
        {
            Assert.NotNull(targetClanId);
            client.SimulateMessage(
                Server.NetPeer,
                new NetworkNotifyClanChangedFaction(
                    targetClanId,
                    oldKingdomId: null,
                    newKingdomId: kingdomId,
                    detail: ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdomByDefection,
                    showNotification: true));

            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(target.HeroId, out var targetHero));
                Assert.Same(playerHero.Culture, ((BasicCharacterObject)playerHero.CharacterObject).Culture);
                Assert.Same(targetHero.Culture, ((BasicCharacterObject)targetHero.CharacterObject).Culture);
                var scene = Assert.IsType<JoinKingdomSceneNotificationItem>(shownJoinKingdomScene);
                Assert.All(
                    scene.GetSceneNotificationCharacters(),
                    character => Assert.NotNull(character.Character.Culture));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Fact]
    public void DefaultCutscenesBehavior_OnServer_DoesNotRegisterJoinKingdomScene()
    {
        var player = CreatePartyWithRegisteredLeader();
        var target = CreatePartyWithRegisteredLeader();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var harmony = new Harmony($"e2e.server-join-kingdom-scene.{Guid.NewGuid():N}");
        shownJoinKingdomScene = null;

        SetMainHero(player.HeroId);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(target.HeroId, out var targetHero));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            using (new AllowedThread())
            {
                Campaign.Current.PlayerDefaultFaction = playerHero.Clan;
                playerHero.Clan._kingdom = kingdom;
                targetHero.Clan._kingdom = kingdom;
                kingdom._rulingClan = playerHero.Clan;
            }

            harmony.Patch(
                AccessTools.Method(
                    typeof(MBInformationManager),
                    nameof(MBInformationManager.ShowSceneNotification),
                    new[] { typeof(SceneNotificationData) }),
                prefix: new HarmonyMethod(
                    typeof(LordBarterSyncTests),
                    nameof(CaptureJoinKingdomScene)));
        });

        try
        {
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(target.HeroId, out var targetHero));
                Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
                new DefaultCutscenesCampaignBehavior().RegisterEvents();
                CampaignEventDispatcher.Instance.OnClanChangedKingdom(
                    targetHero.Clan,
                    oldKingdom: null,
                    newKingdom: kingdom,
                    actionDetail: ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdomByDefection,
                    showNotification: true);
                Assert.Null(shownJoinKingdomScene);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    private static bool CaptureJoinKingdomScene(SceneNotificationData data)
    {
        shownJoinKingdomScene = data;
        return false;
    }

    [Fact]
    public void JoinKingdomBarter_Accepted_SynchronizesDestinationKingdomClanList()
    {
        var client = Clients.First();
        var player = CreatePartyWithRegisteredLeader();
        var target = CreatePartyWithRegisteredLeader();
        var ruler = CreatePartyWithRegisteredLeader();
        var destinationKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var originalKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var requestId = Guid.NewGuid().ToString("N");
        var harmony = new Harmony($"e2e.lord-defection-membership.{Guid.NewGuid():N}");

        void ConfigureKingdoms(EnvironmentInstance instance)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(target.HeroId, out var targetHero));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(ruler.HeroId, out var rulerHero));
                Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(destinationKingdomId, out var destinationKingdom));
                Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(originalKingdomId, out var originalKingdom));

                using (new AllowedThread())
                {
                    Campaign.Current.PlayerDefaultFaction = playerHero.Clan;
                    destinationKingdom._rulingClan = rulerHero.Clan;
                    destinationKingdom._clans.Add(rulerHero.Clan);
                    destinationKingdom._clans.Add(playerHero.Clan);
                    rulerHero.Clan._kingdom = destinationKingdom;
                    playerHero.Clan._kingdom = destinationKingdom;

                    originalKingdom._rulingClan = targetHero.Clan;
                    originalKingdom._clans.Add(targetHero.Clan);
                    targetHero.Clan._kingdom = originalKingdom;
                }
            });
        }

        RegisterPlayer(client, player.HeroId, player.MobilePartyId);
        SetMainHero(player.HeroId);
        ConfigureKingdoms(Server);
        foreach (var instance in Clients)
            ConfigureKingdoms(instance);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(target.MobilePartyId, out var targetParty));
            Assert.True(ConversationPartyHold.TryEngage(
                Server.Resolve<ConversationPartyTracker>(),
                client.NetPeer,
                player.PartyId,
                targetParty,
                target.PartyId,
                engagerIsDefender: true));
            harmony.Patch(
                AccessTools.Method(
                    typeof(BarterManager),
                    nameof(BarterManager.GetOfferValueForFaction),
                    new[] { typeof(BarterData), typeof(IFaction) }),
                prefix: new HarmonyMethod(
                    typeof(LordBarterSyncTests),
                    nameof(AcceptLordDefectionOffer)));
        });

        try
        {
            client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkAuthorizeLordBarter(
                requestId,
                target.HeroId,
                PeaceConversationContext.MapParty,
                target.PartyId,
                LordBarterKind.JoinKingdomAsClan,
                destinationKingdomId)));
            Server.NetworkSentMessages.Clear();

            client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestLordBarter(
                target.HeroId,
                PeaceConversationContext.MapParty,
                target.PartyId,
                LordBarterKind.JoinKingdomAsClan,
                Array.Empty<PeaceBarterTerm>(),
                requestId)));
            TestEnvironment.FlushCoalescer();

            var result = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkLordBarterResult>());
            Assert.True(result.Accepted, result.Reason);

            void AssertMembership(EnvironmentInstance instance)
            {
                instance.Call(() =>
                {
                    Assert.True(instance.ObjectManager.TryGetObject<Hero>(target.HeroId, out var targetHero));
                    Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(destinationKingdomId, out var destinationKingdom));
                    Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(originalKingdomId, out var originalKingdom));
                    Assert.Same(destinationKingdom, targetHero.Clan.Kingdom);
                    Assert.Contains(targetHero.Clan, destinationKingdom.Clans);
                    Assert.DoesNotContain(targetHero.Clan, originalKingdom.Clans);
                });
            }

            AssertMembership(Server);
            foreach (var instance in Clients)
                AssertMembership(instance);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    private static bool AcceptLordDefectionOffer(ref float __result)
    {
        __result = 0f;
        return false;
    }

    /// <summary>
    /// The client can't reproduce every input of the defection price, so its barter shows the price the
    /// server authorized, and the server holds that price after its own valuation moves on.
    /// </summary>
    [Fact]
    public void JoinKingdomBarter_ClientAndServerUseTheAuthorizedPrice()
    {
        const int authorizedPrice = -50_000;
        var client = Clients.First();
        var fixture = CreateDefectionFixture(client);
        var harmony = new Harmony($"e2e.lord-defection-price.{Guid.NewGuid():N}");
        BarterData? clientBarter = null;
        JoinKingdomAsClanBarterable? clientJoinKingdom = null;

        Server.Call(() =>
        {
            new GoldBarterBehavior().RegisterEvents();
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.PlayerHeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(fixture.TargetMobilePartyId, out var targetParty));
            playerHero.Gold = 1_000_000;
            Assert.True(ConversationPartyHold.TryEngage(
                Server.Resolve<ConversationPartyTracker>(),
                client.NetPeer,
                fixture.PlayerPartyId,
                targetParty,
                fixture.TargetPartyId,
                engagerIsDefender: true));
        });

        serverJoinKingdomPrice = authorizedPrice;
        clientJoinKingdomPrice = -1;
        harmony.Patch(
            AccessTools.Method(typeof(JoinKingdomAsClanBarterable), nameof(JoinKingdomAsClanBarterable.GetUnitValueForFaction)),
            prefix: new HarmonyMethod(typeof(LordBarterSyncTests), nameof(PriceJoinKingdomPerInstance)));

        try
        {
            client.NetworkSentMessages.Clear();
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.PlayerHeroId, out var playerHero));
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(fixture.PlayerMobilePartyId, out var playerParty));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.TargetHeroId, out var targetHero));
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(fixture.TargetMobilePartyId, out var targetParty));
                Assert.True(client.ObjectManager.TryGetObject<Kingdom>(fixture.DestinationKingdomId, out var destination));

                clientJoinKingdom = new JoinKingdomAsClanBarterable(targetHero, destination, isDefecting: true);
                clientBarter = new BarterData(playerHero, targetHero, playerParty.Party, targetParty.Party, null);
                clientBarter.AddBarterGroup(new DefaultsBarterGroup());
                clientJoinKingdom.SetIsOffered(true);
                clientBarter.AddBarterable<DefaultsBarterGroup>(clientJoinKingdom, true);
                LordBarterPatch.BeginPlayerBarterPostfix(clientBarter);
            });
            PumpClients();

            client.Call(() => Assert.Equal(
                authorizedPrice,
                clientJoinKingdom!.GetValueForFaction(clientJoinKingdom.OriginalOwner.Clan)));

            var authorization = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkAuthorizeLordBarter>());
            serverJoinKingdomPrice = authorizedPrice * 2;
            Server.NetworkSentMessages.Clear();
            client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestLordBarter(
                fixture.TargetHeroId,
                PeaceConversationContext.MapParty,
                fixture.TargetPartyId,
                LordBarterKind.JoinKingdomAsClan,
                new[] { new PeaceBarterTerm(PeaceBarterTermType.Gold, fixture.PlayerHeroId, null, null, true, -authorizedPrice) },
                authorization.RequestId)));
            TestEnvironment.FlushCoalescer();

            var result = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkLordBarterResult>());
            Assert.True(result.Accepted, result.Reason);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            client.Call(LordBarterPatch.ClearPendingRequest);
            Server.Call(() => ConversationPartyHold.EndEngagement(Server.Resolve<ConversationPartyTracker>(), client.NetPeer));
        }

        Server.PumpGameThread();
    }

    private static bool PriceJoinKingdomPerInstance(ref int __result)
    {
        __result = ModInformation.IsServer ? serverJoinKingdomPrice : clientJoinKingdomPrice;
        return false;
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

        // Pricing the defection at authorization needs ruling clans this fixture doesn't set up.
        var harmony = AcceptEveryDefectionOffer(scoreFiefsAsZero: true);
        try
        {
            client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkAuthorizeLordBarter(
                requestId,
                target.HeroId,
                PeaceConversationContext.MapParty,
                target.PartyId,
                LordBarterKind.JoinKingdomAsClan,
                authorizedKingdomId)));
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
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

    /// <summary>
    /// Vanilla starts the prisoner barter with no OtherParty, and the party screen talk sets no location,
    /// so no older branch matches it (#3072). The captor branch goes first, so it also wins while the
    /// player stands in a settlement or a location mission.
    /// </summary>
    [Theory]
    [InlineData("map")]
    [InlineData("settlement")]
    [InlineData("location")]
    public void PrisonerHeldByPlayerParty_ClientResolverResolvesACaptorContext(string where)
    {
        var client = Clients.First();
        var player = CreatePartyWithRegisteredLeader();
        var target = CreatePartyWithRegisteredLeader();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        SetMainHero(player.HeroId);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(target.HeroId, out var targetHero));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            TakePrisonerAction.Apply(playerParty.Party, targetHero);
            if (where == "settlement")
                playerParty.CurrentSettlement = settlement;
        });
        PumpClients();

        try
        {
            if (where == "location")
            {
                var location = ObjectHelper.SkipConstructor<Location>();
                client.Call(() =>
                    Assert.True(client.ObjectManager.AddExisting($"prisoner-talk-{Guid.NewGuid():N}", location)));
                var campaignMission = new Mock<ICampaignMission>();
                campaignMission.SetupGet(value => value.Location).Returns(location);
                client.CampaignMissionContext = campaignMission.Object;
            }

            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(target.HeroId, out var targetHero));
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty));
                // PartyBelongedToAsPrisoner replicates here, but this environment drops the ChangeState broadcast.
                targetHero._heroState = Hero.CharacterStates.Prisoner;
                Assert.Same(playerParty.Party, targetHero.PartyBelongedToAsPrisoner);
                Assert.Null(targetHero.PartyBelongedTo);
                if (where == "settlement")
                    Assert.Same(playerParty.CurrentSettlement, targetHero.CurrentSettlement);
                if (where == "location")
                    Assert.NotNull(CampaignMission.Current?.Location);

                // conversation_leave_faction_barter_consequence passes PartyBelongedTo?.Party, null here.
                var barter = new BarterData(playerHero, targetHero, playerParty.Party, null, null);
                Assert.True(LordBarterPatch.TryGetConversationContext(
                    barter, client.ObjectManager, out var context, out var contextId));
                Assert.Equal(PeaceConversationContext.PlayerPartyPrisoner, context);
                Assert.Equal(player.PartyId, contextId);
            });
        }
        finally
        {
            client.CampaignMissionContext = null!;
        }
    }

    /// <summary>
    /// A dungeon talk resolves the Settlement context, and so does a lord a vassal's party holds inside
    /// a settlement the player's clan owns. Vanilla offers the recruit line for both, and its
    /// PrisonerReleaseCampaignBehavior frees the lord on the server once his clan joins.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void JoinKingdomBarter_PrisonerInPlayerOwnedDungeon_SettlementContext_IsAccepted(bool heldInDungeon)
    {
        var client = Clients.First();
        var fixture = CreateDefectionFixture(client);
        var vassalCaptor = CreatePartyWithRegisteredLeader();
        var settlementId = CreateTownOwnedBy(fixture.PlayerHeroId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(fixture.PlayerMobilePartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(vassalCaptor.MobilePartyId, out var captorParty));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.TargetHeroId, out var targetHero));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(fixture.DestinationKingdomId, out var destination));

            new PrisonerReleaseCampaignBehavior().RegisterEvents();
            playerParty.CurrentSettlement = settlement;
            if (heldInDungeon)
            {
                TakePrisonerAction.Apply(settlement.Party, targetHero);
            }
            else
            {
                using (new AllowedThread())
                {
                    captorParty.LeaderHero.Clan._kingdom = destination;
                    destination._clans.Add(captorParty.LeaderHero.Clan);
                }

                captorParty.CurrentSettlement = settlement;
                TakePrisonerAction.Apply(captorParty.Party, targetHero);
            }

            Assert.True(targetHero.IsPrisoner);
            Assert.Same(settlement, targetHero.CurrentSettlement);
        });

        var harmony = AcceptEveryDefectionOffer(scoreFiefsAsZero: true);
        try
        {
            var result = SendDefectionBarter(client, fixture, PeaceConversationContext.Settlement, settlementId);

            Assert.True(result.Accepted, result.Reason);
            AssertDefection(fixture, defected: true);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }

        Server.PumpGameThread();
    }

    /// <summary>
    /// The screenshot case: a lord held by the player's own party, talked to away from any settlement.
    /// The barter has no OtherParty, so the item and prisoner behaviors source nothing and only the
    /// heroes' gold is on the table. Vanilla's release frees him from the player's war party once his
    /// clan joins.
    /// </summary>
    [Fact]
    public void JoinKingdomBarter_PrisonerHeldByPlayerParty_IsAcceptedAndReleased()
    {
        const int initialPlayerGold = 1_000_000;
        const int initialTargetGold = 50;
        const int offeredGold = 100_000;
        var client = Clients.First();
        var fixture = CreateDefectionFixture(client);
        BarterData? serverBarter = null;
        AddCampaignTowns();

        Server.Call(() =>
        {
            new GoldBarterBehavior().RegisterEvents();
            new ItemBarterBehavior().RegisterEvents();
            new SetPrisonerFreeBarterBehavior().RegisterEvents();
            new TransferPrisonerBarterBehavior().RegisterEvents();
            new PrisonerReleaseCampaignBehavior().RegisterEvents();
            CampaignEvents.BarterablesRequested.AddNonSerializedListener(this, barter => serverBarter = barter);
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.PlayerHeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(fixture.PlayerMobilePartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.TargetHeroId, out var targetHero));

            playerHero.Gold = initialPlayerGold;
            targetHero.Gold = initialTargetGold;
            TakePrisonerAction.Apply(playerParty.Party, targetHero);

            Assert.Null(playerParty.CurrentSettlement);
            Assert.Null(targetHero.PartyBelongedTo);
        });

        var harmony = AcceptEveryDefectionOffer();
        try
        {
            var result = SendDefectionBarter(
                client,
                fixture,
                PeaceConversationContext.PlayerPartyPrisoner,
                fixture.PlayerPartyId,
                terms: new[]
                {
                    new PeaceBarterTerm(PeaceBarterTermType.Gold, fixture.PlayerHeroId, null, null, true, offeredGold),
                });

            Assert.True(result.Accepted, result.Reason);
            Assert.Equal(initialPlayerGold - offeredGold, result.PlayerGold);
            Assert.NotNull(serverBarter);
            Assert.Null(serverBarter!.OtherParty);
            Assert.All(serverBarter.GetBarterables(), barterable => Assert.True(
                barterable is GoldBarterable || barterable is JoinKingdomAsClanBarterable,
                barterable.GetType().Name));
            AssertDefection(fixture, defected: true);
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.TargetHeroId, out var targetHero));
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(fixture.PlayerMobilePartyId, out var playerParty));
                Assert.Equal(initialTargetGold + offeredGold, targetHero.Gold);
                Assert.False(playerParty.PrisonRoster.Contains(targetHero.CharacterObject));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }

        Server.PumpGameThread();
    }

    public enum PrisonerRecruitGuard
    {
        AiCaptorInForeignSettlement,
        CaptorPartyClaimedAsOwn,
        GenericBarter,
        VassalRecruiter,
        NonLeaderClanmate,
        RulingClanPrisoner,
    }

    /// <summary>
    /// The server mirrors vanilla's prisoner condition, so a client that skips it cannot recruit a
    /// lord vanilla would never offer.
    /// </summary>
    [Theory]
    [InlineData(PrisonerRecruitGuard.AiCaptorInForeignSettlement, "prisoners you hold")]
    [InlineData(PrisonerRecruitGuard.CaptorPartyClaimedAsOwn, "prisoners you hold")]
    [InlineData(PrisonerRecruitGuard.GenericBarter, "only be offered to join")]
    [InlineData(PrisonerRecruitGuard.VassalRecruiter, "Only a kingdom leader")]
    [InlineData(PrisonerRecruitGuard.NonLeaderClanmate, "Only a kingdom leader")]
    [InlineData(PrisonerRecruitGuard.RulingClanPrisoner, "ruling clan")]
    public void JoinKingdomBarter_PrisonerRecruitGuards_AreRejected(PrisonerRecruitGuard guard, string expectedReason)
    {
        var client = Clients.First();
        var fixture = CreateDefectionFixture(
            client,
            guard == PrisonerRecruitGuard.VassalRecruiter
                ? RecruiterRole.Vassal
                : guard == PrisonerRecruitGuard.NonLeaderClanmate
                    ? RecruiterRole.NonLeaderClanmate
                    : RecruiterRole.KingdomLeader,
            targetClanRules: guard == PrisonerRecruitGuard.RulingClanPrisoner);
        var aiCaptor = CreatePartyWithRegisteredLeader();
        var captorInSettlement = guard is PrisonerRecruitGuard.AiCaptorInForeignSettlement or PrisonerRecruitGuard.CaptorPartyClaimedAsOwn;
        // A town the player owns, so only the own-party check refuses the captor's party id.
        var settlementId = CreateTownOwnedBy(
            guard == PrisonerRecruitGuard.CaptorPartyClaimedAsOwn ? fixture.PlayerHeroId : aiCaptor.HeroId);
        var context = PeaceConversationContext.PlayerPartyPrisoner;
        var contextId = fixture.PlayerPartyId;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(fixture.PlayerMobilePartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(aiCaptor.MobilePartyId, out var captorParty));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.TargetHeroId, out var targetHero));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            if (captorInSettlement)
            {
                playerParty.CurrentSettlement = settlement;
                captorParty.CurrentSettlement = settlement;
                TakePrisonerAction.Apply(captorParty.Party, targetHero);
            }
            else
            {
                TakePrisonerAction.Apply(playerParty.Party, targetHero);
            }
        });
        if (guard == PrisonerRecruitGuard.AiCaptorInForeignSettlement)
        {
            context = PeaceConversationContext.Settlement;
            contextId = settlementId;
        }
        else if (guard == PrisonerRecruitGuard.CaptorPartyClaimedAsOwn)
        {
            contextId = aiCaptor.PartyId;
        }

        var result = SendDefectionBarter(
            client,
            fixture,
            context,
            contextId,
            guard == PrisonerRecruitGuard.GenericBarter ? LordBarterKind.Generic : LordBarterKind.JoinKingdomAsClan);

        Assert.False(result.Accepted);
        Assert.Contains(expectedReason, result.Reason);
        AssertDefection(fixture, defected: false);

        Server.PumpGameThread();
    }

    public enum PrisonerTalk
    {
        HeldByPlayerParty,
        DungeonSettlementMenu,
        DungeonLocation,
    }

    public enum PrisonerRecruiterBattle
    {
        EndsBeforeOffer,
        StartsBeforeOffer,
    }

    /// <summary>
    /// The requester's battle refuses a prisoner in every context, both when the barter is authorized
    /// and on Offer, so a siege assault that starts during a dungeon barter refuses it too.
    /// </summary>
    [Theory]
    [InlineData(PrisonerTalk.HeldByPlayerParty, PrisonerRecruiterBattle.EndsBeforeOffer, "The lord barter is no longer authorized.")]
    [InlineData(PrisonerTalk.HeldByPlayerParty, PrisonerRecruiterBattle.StartsBeforeOffer, "Your party is in a battle.")]
    [InlineData(PrisonerTalk.DungeonSettlementMenu, PrisonerRecruiterBattle.EndsBeforeOffer, "The lord barter is no longer authorized.")]
    [InlineData(PrisonerTalk.DungeonSettlementMenu, PrisonerRecruiterBattle.StartsBeforeOffer, "Your party is in a battle.")]
    [InlineData(PrisonerTalk.DungeonLocation, PrisonerRecruiterBattle.EndsBeforeOffer, "The lord barter is no longer authorized.")]
    [InlineData(PrisonerTalk.DungeonLocation, PrisonerRecruiterBattle.StartsBeforeOffer, "Your party is in a battle.")]
    public void JoinKingdomBarter_PrisonerRecruiterInBattle_IsRejected(
        PrisonerTalk talk, PrisonerRecruiterBattle battleTiming, string expectedReason)
    {
        const string locationId = "e2e_prisoner_siege_dungeon";
        const int initialPlayerGold = 1_000_000;
        const int initialTargetGold = 50;
        var client = Clients.First();
        var fixture = CreateDefectionFixture(client);
        var settlementId = CreateTownOwnedBy(fixture.PlayerHeroId);
        var mapEvent = CreateServerMapEvent();
        var tracker = Server.Resolve<LocationConversationTracker>();
        var inDungeon = talk != PrisonerTalk.HeldByPlayerParty;
        var (context, contextId) = talk switch
        {
            PrisonerTalk.DungeonSettlementMenu => (PeaceConversationContext.Settlement, settlementId),
            PrisonerTalk.DungeonLocation => (PeaceConversationContext.Location, locationId),
            _ => (PeaceConversationContext.PlayerPartyPrisoner, fixture.PlayerPartyId),
        };

        // In the dungeon the battle is a siege assault on the player's own town, defended from inside.
        void SetRecruiterInBattle(bool inBattle) => Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(fixture.PlayerMobilePartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEvent.MapEventId, out var battle));
            var side = inDungeon ? battle.DefenderSide : battle.AttackerSide;
            playerParty.Party._mapEventSide = inBattle ? side : null;
            Assert.Equal(inBattle, playerParty.MapEvent != null);
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.PlayerHeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(fixture.PlayerMobilePartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.TargetHeroId, out var targetHero));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEvent.MapEventId, out var battle));

            new GoldBarterBehavior().RegisterEvents();
            new PrisonerReleaseCampaignBehavior().RegisterEvents();
            playerHero.Gold = initialPlayerGold;
            targetHero.Gold = initialTargetGold;
            if (inDungeon)
            {
                playerParty.CurrentSettlement = settlement;
                TakePrisonerAction.Apply(settlement.Party, targetHero);
                battle._mapEventType = MapEvent.BattleTypes.Siege;
                battle.MapEventSettlement = settlement;
            }
            else
            {
                TakePrisonerAction.Apply(playerParty.Party, targetHero);
            }

            if (talk == PrisonerTalk.DungeonLocation)
            {
                Assert.True(Server.ObjectManager.TryGetId(playerHero.CharacterObject, out var playerCharacterId));
                Assert.True(Server.ObjectManager.TryGetId(targetHero.CharacterObject, out var targetCharacterId));
                Assert.True(tracker.TryBeginEngagement(
                    client.NetPeer,
                    LocationConversationTracker.ComposeKey(locationId, playerCharacterId),
                    LocationConversationTracker.ComposeKey(locationId, targetCharacterId)));
            }
        });

        var harmony = AcceptEveryDefectionOffer(scoreFiefsAsZero: true);
        try
        {
            if (battleTiming == PrisonerRecruiterBattle.EndsBeforeOffer)
                SetRecruiterInBattle(true);

            var result = SendDefectionBarter(
                client,
                fixture,
                context,
                contextId,
                betweenAuthorizationAndRequest: () =>
                    SetRecruiterInBattle(battleTiming == PrisonerRecruiterBattle.StartsBeforeOffer),
                terms: new[]
                {
                    new PeaceBarterTerm(PeaceBarterTermType.Gold, fixture.PlayerHeroId, null, null, true, 100_000),
                });

            Assert.False(result.Accepted);
            Assert.Equal(expectedReason, result.Reason);
            Assert.Equal(initialPlayerGold, result.PlayerGold);
            AssertDefection(fixture, defected: false);
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.PlayerHeroId, out var playerHero));
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.TargetHeroId, out var targetHero));
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(fixture.PlayerMobilePartyId, out var playerParty));
                Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                Assert.Equal(initialPlayerGold, playerHero.Gold);
                Assert.Equal(initialTargetGold, targetHero.Gold);
                Assert.Same(inDungeon ? settlement.Party : playerParty.Party, targetHero.PartyBelongedToAsPrisoner);
            });
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            SetRecruiterInBattle(false);
            Server.Call(() => tracker.TryEndEngagement(client.NetPeer, out _));
        }

        Server.PumpGameThread();
    }

    public enum PrisonerLocationTalk
    {
        DungeonWithPlayerInside,
        DungeonWithPlayerOutside,
        CaptorOnTheMap,
        HoldEnded,
    }

    /// <summary>
    /// A prisoner talk in a location mission. The location hold names no settlement, so the server also
    /// needs the requester's party in the lord's settlement, which vanilla's talk implies.
    /// </summary>
    [Theory]
    [InlineData(PrisonerLocationTalk.DungeonWithPlayerInside, null)]
    [InlineData(PrisonerLocationTalk.DungeonWithPlayerOutside, "You can only recruit prisoners you hold.")]
    [InlineData(PrisonerLocationTalk.CaptorOnTheMap, "You can only recruit prisoners you hold.")]
    [InlineData(PrisonerLocationTalk.HoldEnded, "The lord location conversation is no longer active.")]
    public void JoinKingdomBarter_PrisonerLocationTalk_NeedsThePlayerInTheLordsSettlement(
        PrisonerLocationTalk talk, string? expectedReason)
    {
        const string locationId = "e2e_prisoner_dungeon";
        var client = Clients.First();
        var fixture = CreateDefectionFixture(client);
        var aiCaptor = CreatePartyWithRegisteredLeader();
        var settlementId = CreateTownOwnedBy(fixture.PlayerHeroId);
        var tracker = Server.Resolve<LocationConversationTracker>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.PlayerHeroId, out var playerHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(fixture.PlayerMobilePartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(aiCaptor.MobilePartyId, out var captorParty));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.TargetHeroId, out var targetHero));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetId(playerHero.CharacterObject, out var playerCharacterId));
            Assert.True(Server.ObjectManager.TryGetId(targetHero.CharacterObject, out var targetCharacterId));

            new PrisonerReleaseCampaignBehavior().RegisterEvents();
            TakePrisonerAction.Apply(
                talk == PrisonerLocationTalk.CaptorOnTheMap ? captorParty.Party : settlement.Party,
                targetHero);
            if (talk is PrisonerLocationTalk.DungeonWithPlayerInside or PrisonerLocationTalk.HoldEnded)
                playerParty.CurrentSettlement = settlement;
            if (talk != PrisonerLocationTalk.HoldEnded)
            {
                Assert.True(tracker.TryBeginEngagement(
                    client.NetPeer,
                    LocationConversationTracker.ComposeKey(locationId, playerCharacterId),
                    LocationConversationTracker.ComposeKey(locationId, targetCharacterId)));
            }
        });

        var harmony = AcceptEveryDefectionOffer(scoreFiefsAsZero: true);
        try
        {
            var result = SendDefectionBarter(client, fixture, PeaceConversationContext.Location, locationId);

            if (expectedReason == null)
            {
                Assert.True(result.Accepted, result.Reason);
            }
            else
            {
                Assert.False(result.Accepted);
                Assert.Equal(expectedReason, result.Reason);
            }

            AssertDefection(fixture, defected: expectedReason == null);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            Server.Call(() => tracker.TryEndEngagement(client.NetPeer, out _));
        }

        Server.PumpGameThread();
    }

    /// <summary>
    /// The request re-runs the custody check, so a lord who escaped or was ransomed after the barter
    /// opened cannot be recruited from the stale screen.
    /// </summary>
    [Fact]
    public void JoinKingdomBarter_PrisonerReleasedBeforeRequest_IsRejected()
    {
        var client = Clients.First();
        var fixture = CreateDefectionFixture(client);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(fixture.PlayerMobilePartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.TargetHeroId, out var targetHero));
            TakePrisonerAction.Apply(playerParty.Party, targetHero);
        });

        var result = SendDefectionBarter(
            client,
            fixture,
            PeaceConversationContext.PlayerPartyPrisoner,
            fixture.PlayerPartyId,
            betweenAuthorizationAndRequest: () => Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.TargetHeroId, out var targetHero));
                EndCaptivityAction.ApplyByEscape(targetHero);
                Assert.False(targetHero.IsPrisoner);
            }));

        Assert.False(result.Accepted);
        Assert.Contains("prisoners you hold", result.Reason);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.TargetHeroId, out var targetHero));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(fixture.OriginKingdomId, out var originKingdom));
            Assert.Same(originKingdom, targetHero.Clan.Kingdom);
        });

        Server.PumpGameThread();
    }

    public enum MapPartyRefusal
    {
        PlayerPartyInBattle,
        LordPartyInBattle,
        HoldEnded,
        HoldOnAnotherLordParty,
        HoldFromAnotherParty,
    }

    /// <summary>
    /// A refused map-party barter names the check that failed, instead of the "no longer active" text
    /// every map-party refusal shared.
    /// </summary>
    [Theory]
    [InlineData(MapPartyRefusal.PlayerPartyInBattle, "Your party is in a battle.")]
    [InlineData(MapPartyRefusal.LordPartyInBattle, "The lord's party is in a battle.")]
    [InlineData(MapPartyRefusal.HoldEnded, "The conversation hold on the lord's party ended or belongs to another party.")]
    [InlineData(MapPartyRefusal.HoldOnAnotherLordParty, "The conversation hold on the lord's party ended or belongs to another party.")]
    [InlineData(MapPartyRefusal.HoldFromAnotherParty, "The conversation hold on the lord's party ended or belongs to another party.")]
    public void MapPartyBarter_Refused_ReportsWhichCheckFailed(MapPartyRefusal refusal, string expectedReason)
    {
        var client = Clients.First();
        var player = CreatePartyWithRegisteredLeader();
        var target = CreatePartyWithRegisteredLeader();
        var other = CreatePartyWithRegisteredLeader();
        var mapEvent = refusal is MapPartyRefusal.PlayerPartyInBattle or MapPartyRefusal.LordPartyInBattle
            ? CreateServerMapEvent()
            : null;
        var tracker = Server.Resolve<ConversationPartyTracker>();
        var requestId = Guid.NewGuid().ToString("N");

        RegisterPlayer(client, player.HeroId, player.MobilePartyId);
        SetMainHero(player.HeroId);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(target.MobilePartyId, out var targetParty));
            Assert.True(ConversationPartyHold.TryEngage(
                tracker,
                client.NetPeer,
                player.PartyId,
                targetParty,
                target.PartyId,
                engagerIsDefender: true));
        });

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkAuthorizeLordBarter(
            requestId, target.HeroId, PeaceConversationContext.MapParty, target.PartyId, LordBarterKind.Generic)));
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(target.MobilePartyId, out var targetParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(other.MobilePartyId, out var otherParty));
            MapEvent? battle = null;
            if (mapEvent != null)
                Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEvent.MapEventId, out battle));

            switch (refusal)
            {
                case MapPartyRefusal.PlayerPartyInBattle:
                    playerParty.Party._mapEventSide = battle!.AttackerSide;
                    break;
                case MapPartyRefusal.LordPartyInBattle:
                    targetParty.Party._mapEventSide = battle!.DefenderSide;
                    break;
                case MapPartyRefusal.HoldEnded:
                    ConversationPartyHold.EndEngagement(tracker, client.NetPeer);
                    break;
                case MapPartyRefusal.HoldOnAnotherLordParty:
                    ConversationPartyHold.EndEngagement(tracker, client.NetPeer);
                    Assert.True(ConversationPartyHold.TryEngage(
                        tracker, client.NetPeer, player.PartyId, otherParty, other.PartyId, engagerIsDefender: true));
                    break;
                case MapPartyRefusal.HoldFromAnotherParty:
                    Assert.True(ConversationPartyHold.TryEngage(
                        tracker, client.NetPeer, other.PartyId, targetParty, target.PartyId, engagerIsDefender: true));
                    break;
            }
        });
        Server.NetworkSentMessages.Clear();

        try
        {
            client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestLordBarter(
                target.HeroId,
                PeaceConversationContext.MapParty,
                target.PartyId,
                LordBarterKind.Generic,
                Array.Empty<PeaceBarterTerm>(),
                requestId)));
            TestEnvironment.FlushCoalescer();

            var result = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkLordBarterResult>());
            Assert.False(result.Accepted);
            Assert.Equal(expectedReason, result.Reason);
        }
        finally
        {
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty));
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(target.MobilePartyId, out var targetParty));
                playerParty.Party._mapEventSide = null;
                targetParty.Party._mapEventSide = null;
                ConversationPartyHold.EndEngagement(tracker, client.NetPeer);
            });
        }

        Server.PumpGameThread();
    }

    public enum UnsendableLordBarter
    {
        UnresolvedContext,
        LordWithoutId,
        KingdomWithoutId,
        GoldAtZero,
        ItemWithoutId,
        FiefWithoutId,
        ReleasedPrisonerWithoutId,
        TransferredPrisonerWithoutId,
        UnsupportedTerm,
    }

    /// <summary>
    /// A lord barter the client can't send says why on Offer in plain words, never a type name, and
    /// no barter request is sent.
    /// </summary>
    [Theory]
    [InlineData(UnsendableLordBarter.UnresolvedContext, "The conversation with this lord could not be identified.")]
    [InlineData(UnsendableLordBarter.LordWithoutId, "This lord could not be identified.")]
    [InlineData(UnsendableLordBarter.KingdomWithoutId, "The kingdom this lord would join could not be identified.")]
    [InlineData(UnsendableLordBarter.GoldAtZero, "The offered gold is set to 0.")]
    [InlineData(UnsendableLordBarter.ItemWithoutId, "The offered item cannot be sent.")]
    [InlineData(UnsendableLordBarter.FiefWithoutId, "The offered fief cannot be sent.")]
    [InlineData(UnsendableLordBarter.ReleasedPrisonerWithoutId, "The offered prisoner cannot be sent.")]
    [InlineData(UnsendableLordBarter.TransferredPrisonerWithoutId, "The offered prisoner cannot be sent.")]
    [InlineData(UnsendableLordBarter.UnsupportedTerm, "The offered term cannot be sent.")]
    public void LordBarter_ClientCannotSend_OfferSaysWhy(UnsendableLordBarter failure, string expectedReason)
    {
        var client = Clients.First();
        var player = CreatePartyWithRegisteredLeader();
        var target = CreatePartyWithRegisteredLeader();
        var harmony = new Harmony($"e2e.lord-barter-unsendable.{Guid.NewGuid():N}");

        RegisterPlayer(client, player.HeroId, player.MobilePartyId);
        SetMainHero(player.HeroId);
        client.NetworkSentMessages.Clear();
        shownMessages.Clear();
        harmony.Patch(
            AccessTools.Method(typeof(InformationManager), nameof(InformationManager.DisplayMessage)),
            prefix: new HarmonyMethod(typeof(LordBarterSyncTests), nameof(CaptureShownMessage)));

        try
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(target.HeroId, out var targetHero));
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(target.MobilePartyId, out var targetParty));

                var barter = CreateUnsendableBarter(failure, playerHero, playerParty.Party, targetHero, targetParty.Party);
                LordBarterPatch.BeginPlayerBarterPostfix(barter);
                Assert.False(LordBarterPatch.ApplyAndFinalizePlayerBarterPrefix(playerHero, barter));
            });

            Assert.Equal($"Unable to send the lord barter to the server. {expectedReason}", Assert.Single(shownMessages));
            Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkRequestLordBarter>());
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            client.Call(LordBarterPatch.ClearPendingRequest);
            shownMessages.Clear();
        }
    }

    private static bool CaptureShownMessage(InformationMessage message)
    {
        shownMessages.Add(message.Information);
        return false;
    }

    /// <summary>
    /// Every case but the first two resolves the map party context, so the failure comes from the
    /// offered term.
    /// </summary>
    private static BarterData CreateUnsendableBarter(
        UnsendableLordBarter failure, Hero playerHero, PartyBase playerParty, Hero targetHero, PartyBase targetParty)
    {
        if (failure == UnsendableLordBarter.UnresolvedContext)
            return new BarterData(playerHero, targetHero, playerParty, null, null);
        if (failure == UnsendableLordBarter.LordWithoutId)
            return new BarterData(playerHero, ObjectHelper.SkipConstructor<Hero>(), playerParty, targetParty, null);

        Barterable term = failure switch
        {
            UnsendableLordBarter.KingdomWithoutId =>
                new JoinKingdomAsClanBarterable(targetHero, ObjectHelper.SkipConstructor<Kingdom>(), isDefecting: true),
            UnsendableLordBarter.GoldAtZero =>
                new GoldBarterable(playerHero, targetHero, playerParty, targetParty, 1000),
            UnsendableLordBarter.ItemWithoutId =>
                new ItemBarterable(
                    playerHero, targetHero, playerParty, targetParty, new ItemRosterElement(ObjectHelper.SkipConstructor<ItemObject>(), 1), 1),
            UnsendableLordBarter.FiefWithoutId =>
                new FiefBarterable(ObjectHelper.SkipConstructor<Settlement>(), playerHero, targetHero),
            UnsendableLordBarter.ReleasedPrisonerWithoutId =>
                new SetPrisonerFreeBarterable(ObjectHelper.SkipConstructor<Hero>(), playerHero, playerParty, targetHero),
            UnsendableLordBarter.TransferredPrisonerWithoutId =>
                new TransferPrisonerBarterable(ObjectHelper.SkipConstructor<Hero>(), playerHero, playerParty, targetHero, targetParty),
            UnsendableLordBarter.UnsupportedTerm =>
                new LeaveKingdomAsClanBarterable(targetHero, targetParty),
            _ => throw new ArgumentOutOfRangeException(nameof(failure)),
        };

        var barter = new BarterData(playerHero, targetHero, playerParty, targetParty, null);
        barter.AddBarterGroup(new DefaultsBarterGroup());
        term.CurrentAmount = failure == UnsendableLordBarter.GoldAtZero ? 0 : 1;
        term.SetIsOffered(true);
        barter.AddBarterable<DefaultsBarterGroup>(term, false);
        return barter;
    }

    public enum RecruiterRole
    {
        KingdomLeader,
        Vassal,
        NonLeaderClanmate,
    }

    private sealed record DefectionFixture(
        string PlayerHeroId,
        string PlayerMobilePartyId,
        string PlayerPartyId,
        string TargetHeroId,
        string DestinationKingdomId,
        string OriginKingdomId,
        string TargetMobilePartyId,
        string TargetPartyId);

    /// <summary>
    /// A registered player whose clan belongs to the destination kingdom, and a target leading a clan in
    /// another kingdom. Set on every instance, like JoinKingdomBarter_Accepted_SynchronizesDestinationKingdomClanList.
    /// </summary>
    private DefectionFixture CreateDefectionFixture(
        EnvironmentInstance client,
        RecruiterRole role = RecruiterRole.KingdomLeader,
        bool targetClanRules = false)
    {
        var player = CreatePartyWithRegisteredLeader();
        var target = CreatePartyWithRegisteredLeader();
        var destinationRuler = CreatePartyWithRegisteredLeader();
        var originRuler = CreatePartyWithRegisteredLeader();
        var destinationKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var originKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        RegisterPlayer(client, player.HeroId, player.MobilePartyId);
        SetMainHero(player.HeroId);
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(player.HeroId, out var playerHero));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(target.HeroId, out var targetHero));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(destinationRuler.HeroId, out var destinationRulerHero));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(originRuler.HeroId, out var originRulerHero));
                Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(destinationKingdomId, out var destination));
                Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(originKingdomId, out var origin));

                using (new AllowedThread())
                {
                    var playerClan = playerHero.Clan;
                    Campaign.Current.PlayerDefaultFaction = playerClan;
                    playerClan._kingdom = destination;
                    destination._clans.Add(playerClan);
                    destination._rulingClan = playerClan;
                    if (role == RecruiterRole.Vassal)
                    {
                        destinationRulerHero.Clan._kingdom = destination;
                        destination._clans.Add(destinationRulerHero.Clan);
                        destination._rulingClan = destinationRulerHero.Clan;
                    }
                    else if (role == RecruiterRole.NonLeaderClanmate)
                    {
                        // The player's clan rules, but another member leads it.
                        playerClan._leader = destinationRulerHero;
                    }

                    targetHero.Clan._kingdom = origin;
                    origin._clans.Add(targetHero.Clan);
                    origin._rulingClan = targetHero.Clan;
                    if (!targetClanRules)
                    {
                        originRulerHero.Clan._kingdom = origin;
                        origin._clans.Add(originRulerHero.Clan);
                        origin._rulingClan = originRulerHero.Clan;
                    }
                }
            });
        }

        return new DefectionFixture(
            player.HeroId,
            player.MobilePartyId,
            player.PartyId,
            target.HeroId,
            destinationKingdomId,
            originKingdomId,
            target.MobilePartyId,
            target.PartyId);
    }

    private string CreateTownOwnedBy(string ownerHeroId)
    {
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Town>(townId, out var town));

            settlement.SetSettlementComponent(town);
            town.OwnerClan = owner.Clan;
            town.IsOwnerUnassigned = false;
            Assert.Same(owner.Clan, settlement.OwnerClan);
        });
        return settlementId;
    }

    /// <summary>
    /// ItemBarterBehavior reads the three closest towns before it checks the parties, and a real
    /// campaign always has them.
    /// </summary>
    private void AddCampaignTowns()
    {
        for (var i = 0; i < 3; i++)
        {
            var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
            var townId = TestEnvironment.CreateRegisteredObject<Town>();
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                Assert.True(Server.ObjectManager.TryGetObject<Town>(townId, out var town));
                settlement.SetSettlementComponent(town);
                Campaign.Current._towns.Add(town);
            });
        }
    }

    private NetworkLordBarterResult SendDefectionBarter(
        EnvironmentInstance client,
        DefectionFixture fixture,
        PeaceConversationContext context,
        string contextId,
        LordBarterKind kind = LordBarterKind.JoinKingdomAsClan,
        Action? betweenAuthorizationAndRequest = null,
        PeaceBarterTerm[]? terms = null)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var targetKingdomId = kind == LordBarterKind.JoinKingdomAsClan ? fixture.DestinationKingdomId : null;

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkAuthorizeLordBarter(
            requestId, fixture.TargetHeroId, context, contextId, kind, targetKingdomId)));
        betweenAuthorizationAndRequest?.Invoke();
        Server.NetworkSentMessages.Clear();

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkRequestLordBarter(
            fixture.TargetHeroId, context, contextId, kind, terms, requestId)));
        TestEnvironment.FlushCoalescer();

        return Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkLordBarterResult>());
    }

    private Harmony AcceptEveryDefectionOffer(bool scoreFiefsAsZero = false)
    {
        var harmony = new Harmony($"e2e.lord-prisoner-defection.{Guid.NewGuid():N}");
        harmony.Patch(
            AccessTools.Method(
                typeof(BarterManager),
                nameof(BarterManager.GetOfferValueForFaction),
                new[] { typeof(BarterData), typeof(IFaction) }),
            prefix: new HarmonyMethod(typeof(LordBarterSyncTests), nameof(AcceptLordDefectionOffer)));

        // Apply() prices the clan's fiefs, and a synthetic town has no distance cache to price with.
        if (scoreFiefsAsZero)
        {
            harmony.Patch(
                AccessTools.Method(
                    typeof(JoinKingdomAsClanBarterable),
                    nameof(JoinKingdomAsClanBarterable.GetUnitValueForFaction)),
                prefix: new HarmonyMethod(typeof(LordBarterSyncTests), nameof(ZeroJoinKingdomValue)));
        }

        return harmony;
    }

    private static bool ZeroJoinKingdomValue(ref int __result)
    {
        __result = 0;
        return false;
    }

    /// <summary>
    /// A defected lord is in the destination kingdom and held by no party on every instance, and not a
    /// prisoner on the server; a refused one is still held and still in his origin kingdom.
    /// </summary>
    private void AssertDefection(DefectionFixture fixture, bool defected)
    {
        PumpClients();
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.TargetHeroId, out var targetHero));
                Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(
                    defected ? fixture.DestinationKingdomId : fixture.OriginKingdomId,
                    out var expectedKingdom));

                Assert.True(expectedKingdom == targetHero.Clan.Kingdom, $"{instance.GetType().Name}: {targetHero.Clan.Kingdom?.StringId}");
                if (instance == Server)
                    Assert.Equal(!defected, targetHero.IsPrisoner);
                if (defected)
                    Assert.Null(targetHero.PartyBelongedToAsPrisoner);
                else
                    Assert.NotNull(targetHero.PartyBelongedToAsPrisoner);
            });
        }
    }

    // Clients apply replicated hero state on their game thread.
    private void PumpClients()
    {
        foreach (var client in Clients)
            client.PumpGameThread();
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

    private string CreateSyntheticSiegeEvent()
    {
        return TestEnvironment.CreateRegisteredObject<SiegeEvent>(new[]
        {
            AccessTools.Method(
                typeof(MobileParty),
                nameof(MobileParty.OnPartyJoinedSiegeInternal)),
            AccessTools.Method(
                typeof(BesiegerCamp),
                nameof(BesiegerCamp.InitializeSiegeEventSide)),
            AccessTools.Method(
                typeof(Settlement),
                nameof(Settlement.InitializeSiegeEventSide)),
        });
    }

    private void RegisterPlayer(EnvironmentInstance client, string heroId, string mobilePartyId)
        => RegisterPlayer(client, heroId, mobilePartyId, "PlayerOne");

    private void RegisterPlayer(EnvironmentInstance client, string heroId, string mobilePartyId, string controllerId)
    {
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

    private static bool SupplySafePassageWarState(
        IFaction faction1,
        IFaction faction2,
        ref bool __result)
    {
        var playerFaction = safePassagePlayerFaction;
        var targetFaction = safePassageTargetFaction;
        var nearbyFaction = safePassageNearbyFaction;
        if (playerFaction == null || targetFaction == null || nearbyFaction == null)
            return true;

        if ((faction1 == playerFaction &&
             (faction2 == targetFaction || faction2 == nearbyFaction)) ||
            (faction2 == playerFaction &&
             (faction1 == targetFaction || faction1 == nearbyFaction)))
        {
            __result = true;
            return false;
        }

        if ((faction1 == targetFaction && faction2 == nearbyFaction) ||
            (faction1 == nearbyFaction && faction2 == targetFaction))
        {
            __result = false;
            return false;
        }

        return true;
    }
}
