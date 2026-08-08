using Common.Network;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using GameInterface.Services.Barters;
using GameInterface.Services.Barters.Handlers;
using GameInterface.Services.Barters.Messages;
using GameInterface.Services.Barters.Patches;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MobilePartyAIs.Patches;
using GameInterface.Services.Players;
using GameInterface.Services.UI.Notifications.Messages;
using GameInterface.Services.Villages.Interfaces;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.SceneInformationPopupTypes;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Heroes;

public class LordBarterSyncTests : MapEventTestBase
{
    private static int observedBarterAcceptedDispatches;
    private static IFaction? safePassagePlayerFaction;
    private static IFaction? safePassageTargetFaction;
    private static IFaction? safePassageNearbyFaction;
    private static SceneNotificationData? shownJoinKingdomScene;

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
