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
using GameInterface.Services.Villages.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
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
