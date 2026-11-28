using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Villages.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

/// <summary>
/// A clan changing kingdom runs vanilla's siege continuity check on the server. A player it pulls out of its own
/// siege gets the "no longer enemies" menu, which the client's own run never shows because the leave arrives first.
/// </summary>
public class SiegeKingdomChangeTests : SiegeTestBase
{
    private const string PlayerControllerId = "SiegeKingdomJoiner";
    private const string LeaderControllerId = "SiegeKingdomStayer";
    private const string TownStayerControllerId = "SiegeKingdomTownStayer";
    private const string PeaceMenuId = "hostile_action_end_by_peace";

    public SiegeKingdomChangeTests(ITestOutputHelper output) : base(output)
    {
    }

    private enum PlayerSiegeRole
    {
        Besieger,
        SoleBesieger,
        Defender,
    }

    [Fact]
    public void CampedPlayerJoinsKingdomAtPeaceWithDefenders_LeavesTheSiegeWithTheNoLongerEnemiesMenu()
    {
        var client = Clients.First();
        var bystander = Clients.Last();
        var scenario = SetupSiegeScenario(PlayerSiegeRole.Besieger);
        ConfigureSiegeMenuClient(client, scenario.Participants.PlayerPartyId);
        // The bystander plays the camp leader, a player party the check leaves in the camp, so the empty assert
        // below fails if either the server's stayed check or the client's party id check goes away.
        ConfigureSiegeMenuClient(bystander, scenario.Participants.LeaderPartyId);

        using var menus = RecordMenus();
        JoinKingdomOnServer(scenario.Participants.PlayerClanId, scenario.Participants.NeutralKingdomId);

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var siegeEvent = instance.GetRegisteredObject<SiegeEvent>(scenario.SiegeEventId);
                var leader = instance.GetRegisteredObject<MobileParty>(scenario.Participants.LeaderPartyId);

                Assert.Null(instance.GetRegisteredObject<MobileParty>(scenario.Participants.PlayerPartyId).BesiegerCamp);
                Assert.Equal(new[] { leader }, siegeEvent.BesiegerCamp._besiegerParties);
                Assert.Same(siegeEvent, siegeEvent.BesiegedSettlement.SiegeEvent);
            });
        }

        Assert.Equal(new[] { PeaceMenuId }, menus.CallsFor(client));
        Assert.Empty(menus.CallsFor(bystander));
        AssertPeaceMenuFaction(client, scenario.Participants.DefenderKingdomId);
    }

    [Fact]
    public void SoleBesiegerJoinsKingdomAtPeaceWithDefenders_EndsTheSiegeOnTheNoLongerEnemiesMenu()
    {
        var client = Clients.First();
        var scenario = SetupSiegeScenario(PlayerSiegeRole.SoleBesieger);
        ConfigureSiegeMenuClient(client, scenario.Participants.PlayerPartyId);

        using var menus = RecordMenus();
        JoinKingdomOnServer(scenario.Participants.PlayerClanId, scenario.Participants.NeutralKingdomId);

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.Null(instance.GetRegisteredObject<MobileParty>(scenario.Participants.PlayerPartyId).BesiegerCamp);
                Assert.Null(instance.GetRegisteredObject<Settlement>(scenario.SettlementId).SiegeEvent);
                Assert.False(instance.ObjectManager.TryGetObject<SiegeEvent>(scenario.SiegeEventId, out _));
            });
        }

        // The siege-ended prompt leaves the siege menu first, then the peace prompt opens its menu.
        Assert.Equal(new[] { nameof(GameMenu.ExitToLast), PeaceMenuId }, menus.CallsFor(client));
        AssertPeaceMenuFaction(client, scenario.Participants.DefenderKingdomId);
    }

    [Fact]
    public void DefendingPlayerJoinsTheBesiegers_LeavesTheTownWithTheNoLongerEnemiesMenu()
    {
        var client = Clients.First();
        var bystander = Clients.Last();
        var scenario = SetupSiegeScenario(PlayerSiegeRole.Defender);
        ConfigureSiegeMenuClient(client, scenario.Participants.PlayerPartyId);
        // The bystander plays a second defender, from the owner clan, that the check leaves in the town, so the empty
        // assert below fails if either the server's stayed check or the client's party id check goes away.
        ConfigureSiegeMenuClient(bystander, scenario.Participants.TownStayerPartyId);

        using var menus = RecordMenus();
        JoinKingdomOnServer(scenario.Participants.PlayerClanId, scenario.Participants.BesiegerKingdomId);

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var siegeEvent = instance.GetRegisteredObject<SiegeEvent>(scenario.SiegeEventId);
                var settlement = siegeEvent.BesiegedSettlement;
                var player = instance.GetRegisteredObject<MobileParty>(scenario.Participants.PlayerPartyId);
                var townStayer = instance.GetRegisteredObject<MobileParty>(scenario.Participants.TownStayerPartyId);

                Assert.Null(player.CurrentSettlement);
                Assert.DoesNotContain(player, settlement.Parties);
                Assert.Same(settlement, townStayer.CurrentSettlement);
                Assert.Same(siegeEvent, settlement.SiegeEvent);
            });
        }

        Assert.Equal(new[] { PeaceMenuId }, menus.CallsFor(client));
        Assert.Empty(menus.CallsFor(bystander));
        AssertPeaceMenuFaction(client, scenario.Participants.BesiegerKingdomId);
    }

    // Records, without running them, every way the prompts open or leave a menu, in call order.
    private static MethodCallRecorder RecordMenus() => new(
        AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu)),
        AccessTools.Method(typeof(MenuContext), nameof(MenuContext.SwitchToMenu), new[] { typeof(string) }),
        AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)));

    private void JoinKingdomOnServer(string clanId, string kingdomId)
    {
        Server.Call(() => ChangeKingdomAction.ApplyByJoinToKingdom(
            Server.GetRegisteredObject<Clan>(clanId),
            Server.GetRegisteredObject<Kingdom>(kingdomId)));
    }

    private static void AssertPeaceMenuFaction(EnvironmentInstance client, string kingdomId)
    {
        client.Call(() => Assert.Same(
            client.GetRegisteredObject<Kingdom>(kingdomId),
            Campaign.Current.GetCampaignBehavior<PartyDiplomaticHandlerCampaignBehavior>()
                ._lastFactionMadePeaceWithCausedPlayerToLeaveEvent));
    }

    /// <summary>
    /// The besieger kingdom's lord party besieges a town of the defender kingdom, at war. The player is a vassal
    /// camped alongside it, camped alone, or inside the town.
    /// </summary>
    private SiegeScenario SetupSiegeScenario(PlayerSiegeRole role)
    {
        var instances = Clients.Append(Server).ToArray();
        var participants = CreateParticipants(instances);

        var playerDefends = role == PlayerSiegeRole.Defender;
        var playerKingdomId = playerDefends ? participants.DefenderKingdomId : participants.BesiegerKingdomId;
        var playerSide = playerDefends ? BattleSideEnum.Defender : BattleSideEnum.Attacker;
        var townPartyIds = playerDefends
            ? new[] { participants.PlayerPartyId, participants.TownStayerPartyId }
            : Array.Empty<string>();
        var campPartyIds = role switch
        {
            PlayerSiegeRole.Besieger => new[] { participants.LeaderPartyId, participants.PlayerPartyId },
            PlayerSiegeRole.SoleBesieger => new[] { participants.PlayerPartyId },
            PlayerSiegeRole.Defender => new[] { participants.LeaderPartyId },
            _ => throw new ArgumentOutOfRangeException(nameof(role)),
        };

        StageKingdomsAtWar(instances, participants, playerKingdomId);
        LinkPartiesToClans(instances, participants);
        var siege = SetupSiege(campPartyIds);
        // The settlement outlives the siege graph, so the facts read it by its own id.
        string? settlementId = null;
        Server.Call(() => Assert.True(Server.ObjectManager.TryGetId(
            Server.GetRegisteredObject<SiegeEvent>(siege.SiegeEventId).BesiegedSettlement, out settlementId)));
        StageBesiegedTown(instances, siege.SiegeEventId, participants, townPartyIds);
        AttachDiplomaticHandler(instances);
        AssertPlayerCanJoin(instances, siege.SiegeEventId, participants.PlayerPartyId, playerSide);

        return new SiegeScenario(siege.SiegeEventId, settlementId!, participants);
    }

    private SiegeParticipants CreateParticipants(IReadOnlyList<EnvironmentInstance> instances)
    {
        var (playerHeroId, playerPartyId) = CreatePlayerHeroParty(PlayerControllerId);
        var (leaderHeroId, leaderPartyId) = CreatePlayerHeroParty(LeaderControllerId);
        var (townStayerHeroId, townStayerPartyId) = CreatePlayerHeroParty(TownStayerControllerId);

        return new SiegeParticipants(
            BesiegerKingdomId: CreateKingdomEverywhere(instances),
            DefenderKingdomId: CreateKingdomEverywhere(instances),
            NeutralKingdomId: CreateKingdomEverywhere(instances),
            BesiegerClanId: TestEnvironment.CreateRegisteredObject<Clan>(),
            OwnerClanId: TestEnvironment.CreateRegisteredObject<Clan>(),
            PlayerClanId: TestEnvironment.CreateRegisteredObject<Clan>(),
            TownId: TestEnvironment.CreateRegisteredObject<Town>(),
            LeaderHeroId: leaderHeroId,
            LeaderPartyId: leaderPartyId,
            TownStayerHeroId: townStayerHeroId,
            TownStayerPartyId: townStayerPartyId,
            PlayerHeroId: playerHeroId,
            PlayerPartyId: playerPartyId);
    }

    // Puts the lord's clan, the town owner's clan and the player's clan in their kingdoms, and the two kingdoms at war.
    private static void StageKingdomsAtWar(
        IReadOnlyList<EnvironmentInstance> instances,
        SiegeParticipants participants,
        string playerKingdomId)
    {
        foreach (var instance in instances)
        {
            instance.Call(() =>
            {
                var besiegers = instance.GetRegisteredObject<Kingdom>(participants.BesiegerKingdomId);
                var defenders = instance.GetRegisteredObject<Kingdom>(participants.DefenderKingdomId);
                var playerKingdom = instance.GetRegisteredObject<Kingdom>(playerKingdomId);
                var besiegerClan = instance.GetRegisteredObject<Clan>(participants.BesiegerClanId);
                var ownerClan = instance.GetRegisteredObject<Clan>(participants.OwnerClanId);
                var playerClan = instance.GetRegisteredObject<Clan>(participants.PlayerClanId);

                using (new AllowedThread())
                {
                    KingdomTestSetup.PlaceClanInKingdom(besiegerClan, besiegers);
                    KingdomTestSetup.PlaceClanInKingdom(ownerClan, defenders);
                    KingdomTestSetup.PlaceClanInKingdom(playerClan, playerKingdom);
                    VillageHostileFactionStanceHelper.ApplyWarStance(besiegers, defenders);
                }
            });
        }
    }

    // Makes the joining hero lead its named clan and party, and puts the camp leader in the besieger clan and the town
    // stayer in the owner clan.
    private static void LinkPartiesToClans(IReadOnlyList<EnvironmentInstance> instances, SiegeParticipants participants)
    {
        foreach (var instance in instances)
        {
            instance.Call(() =>
            {
                var besiegerClan = instance.GetRegisteredObject<Clan>(participants.BesiegerClanId);
                var playerClan = instance.GetRegisteredObject<Clan>(participants.PlayerClanId);
                var playerHero = instance.GetRegisteredObject<Hero>(participants.PlayerHeroId);
                var playerParty = instance.GetRegisteredObject<MobileParty>(participants.PlayerPartyId);
                var ownerClan = instance.GetRegisteredObject<Clan>(participants.OwnerClanId);
                var leaderHero = instance.GetRegisteredObject<Hero>(participants.LeaderHeroId);
                var townStayerHero = instance.GetRegisteredObject<Hero>(participants.TownStayerHeroId);
                var townStayerParty = instance.GetRegisteredObject<MobileParty>(participants.TownStayerPartyId);
                var leaderParty = instance.GetRegisteredObject<MobileParty>(participants.LeaderPartyId);

                using (new AllowedThread())
                {
                    playerClan.Name = new TextObject("joiner clan");
                    playerHero.Clan = playerClan;
                    playerClan.SetLeader(playerHero);
                    playerHero.PartyBelongedTo = playerParty;
                    playerParty.ActualClan = playerClan;
                    leaderHero.Clan = besiegerClan;
                    leaderHero.PartyBelongedTo = leaderParty;
                    leaderParty.ActualClan = besiegerClan;
                    townStayerHero.Clan = ownerClan;
                    townStayerHero.PartyBelongedTo = townStayerParty;
                    townStayerParty.ActualClan = ownerClan;
                }
            });
        }
    }

    // Turns the besieged settlement into the owner clan's listed town with the given parties inside.
    private static void StageBesiegedTown(
        IReadOnlyList<EnvironmentInstance> instances,
        string siegeEventId,
        SiegeParticipants participants,
        IReadOnlyList<string> townPartyIds)
    {
        foreach (var instance in instances)
        {
            instance.Call(() =>
            {
                var siegeEvent = instance.GetRegisteredObject<SiegeEvent>(siegeEventId);
                var settlement = siegeEvent.BesiegedSettlement;
                var town = instance.GetRegisteredObject<Town>(participants.TownId);

                using (new AllowedThread())
                {
                    settlement.SetSettlementComponent(town);
                    town._ownerClan = instance.GetRegisteredObject<Clan>(participants.OwnerClanId);
                    settlement.SiegeEvent = siegeEvent;
                    // The live server starts sieges through SiegeEventManager.StartSiegeEvent, which lists them.
                    if (!Campaign.Current.SiegeEventManager._siegeEvents.Contains(siegeEvent))
                        Campaign.Current.SiegeEventManager._siegeEvents.Add(siegeEvent);

                    foreach (var partyId in townPartyIds)
                    {
                        var party = instance.GetRegisteredObject<MobileParty>(partyId);
                        party._currentSettlement = settlement;
                        settlement._partiesCache.Add(party);
                    }
                }

                Assert.True(settlement.IsFortification);
                Assert.Same(instance.GetRegisteredObject<Kingdom>(participants.DefenderKingdomId), settlement.MapFaction);
            });
        }
    }

    // Lists vanilla's continuity behavior with the campaign and subscribes it, the way session launch does.
    private static void AttachDiplomaticHandler(IReadOnlyList<EnvironmentInstance> instances)
    {
        foreach (var instance in instances)
        {
            instance.Call(() =>
            {
                var diplomaticHandler = new PartyDiplomaticHandlerCampaignBehavior();
                Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(
                    new CampaignBehaviorBase[] { diplomaticHandler }));
                diplomaticHandler.RegisterEvents();
            });
        }
    }

    private static void AssertPlayerCanJoin(
        IReadOnlyList<EnvironmentInstance> instances,
        string siegeEventId,
        string playerPartyId,
        BattleSideEnum side)
    {
        foreach (var instance in instances)
        {
            instance.Call(() => Assert.True(instance.GetRegisteredObject<SiegeEvent>(siegeEventId)
                .CanPartyJoinSide(instance.GetRegisteredObject<MobileParty>(playerPartyId).Party, side)));
        }
    }

    private string CreateKingdomEverywhere(IEnumerable<EnvironmentInstance> instances)
    {
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        KingdomTestSetup.EnsureKingdomRegistered(instances, kingdomId);
        return kingdomId;
    }

    private readonly record struct SiegeParticipants(
        string BesiegerKingdomId,
        string DefenderKingdomId,
        string NeutralKingdomId,
        string BesiegerClanId,
        string OwnerClanId,
        string PlayerClanId,
        string TownId,
        string LeaderHeroId,
        string LeaderPartyId,
        string TownStayerHeroId,
        string TownStayerPartyId,
        string PlayerHeroId,
        string PlayerPartyId);

    private readonly record struct SiegeScenario(
        string SiegeEventId,
        string SettlementId,
        SiegeParticipants Participants);
}
