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
        // Gives the bystander a main party so its prompt handler reaches the party id check, otherwise the empty
        // assert below proves nothing.
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
        var scenario = SetupSiegeScenario(PlayerSiegeRole.Defender);
        ConfigureSiegeMenuClient(client, scenario.Participants.PlayerPartyId);

        using var menus = RecordMenus();
        JoinKingdomOnServer(scenario.Participants.PlayerClanId, scenario.Participants.BesiegerKingdomId);

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var siegeEvent = instance.GetRegisteredObject<SiegeEvent>(scenario.SiegeEventId);
                var player = instance.GetRegisteredObject<MobileParty>(scenario.Participants.PlayerPartyId);

                Assert.Null(player.CurrentSettlement);
                Assert.DoesNotContain(player, siegeEvent.BesiegedSettlement.Parties);
                Assert.Same(siegeEvent, siegeEvent.BesiegedSettlement.SiegeEvent);
            });
        }

        Assert.Equal(new[] { PeaceMenuId }, menus.CallsFor(client));
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
        var defenderPartyId = playerDefends ? participants.PlayerPartyId : null;
        var campPartyIds = role switch
        {
            PlayerSiegeRole.Besieger => new[] { participants.LeaderPartyId, participants.PlayerPartyId },
            PlayerSiegeRole.SoleBesieger => new[] { participants.PlayerPartyId },
            PlayerSiegeRole.Defender => new[] { participants.LeaderPartyId },
            _ => throw new ArgumentOutOfRangeException(nameof(role)),
        };

        StageKingdomsAtWar(instances, participants, playerKingdomId);
        var siege = SetupSiege(campPartyIds);
        var settlementId = StageBesiegedTown(instances, siege.SiegeEventId, participants, defenderPartyId);
        AssertPlayerCanJoin(instances, siege.SiegeEventId, participants.PlayerPartyId, playerSide);

        return new SiegeScenario(siege.SiegeEventId, settlementId, participants);
    }

    private SiegeParticipants CreateParticipants(IReadOnlyList<EnvironmentInstance> instances)
    {
        var (playerHeroId, playerPartyId) = CreatePlayerHeroParty(PlayerControllerId);

        return new SiegeParticipants(
            BesiegerKingdomId: CreateKingdomEverywhere(instances),
            DefenderKingdomId: CreateKingdomEverywhere(instances),
            NeutralKingdomId: CreateKingdomEverywhere(instances),
            BesiegerClanId: TestEnvironment.CreateRegisteredObject<Clan>(),
            OwnerClanId: TestEnvironment.CreateRegisteredObject<Clan>(),
            PlayerClanId: TestEnvironment.CreateRegisteredObject<Clan>(),
            TownId: TestEnvironment.CreateRegisteredObject<Town>(),
            LeaderPartyId: TestEnvironment.CreateRegisteredObject<MobileParty>(),
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
                var besiegerClan = instance.GetRegisteredObject<Clan>(participants.BesiegerClanId);
                var playerClan = instance.GetRegisteredObject<Clan>(participants.PlayerClanId);
                var playerHero = instance.GetRegisteredObject<Hero>(participants.PlayerHeroId);
                var playerParty = instance.GetRegisteredObject<MobileParty>(participants.PlayerPartyId);

                using (new AllowedThread())
                {
                    KingdomTestSetup.PlaceClanInKingdom(besiegerClan, besiegers);
                    KingdomTestSetup.PlaceClanInKingdom(
                        instance.GetRegisteredObject<Clan>(participants.OwnerClanId), defenders);
                    KingdomTestSetup.PlaceClanInKingdom(playerClan, instance.GetRegisteredObject<Kingdom>(playerKingdomId));
                    playerClan.Name = new TextObject("joiner clan");
                    playerHero.Clan = playerClan;
                    playerClan.SetLeader(playerHero);
                    playerHero.PartyBelongedTo = playerParty;
                    playerParty.ActualClan = playerClan;
                    instance.GetRegisteredObject<MobileParty>(participants.LeaderPartyId).ActualClan = besiegerClan;
                    VillageHostileFactionStanceHelper.ApplyWarStance(besiegers, defenders);
                }
            });
        }
    }

    // Turns the besieged settlement into the owner clan's listed town with the diplomatic behavior attached, and puts
    // the defending party inside when there is one. Returns the settlement id, which outlives the siege graph.
    private string StageBesiegedTown(
        IReadOnlyList<EnvironmentInstance> instances,
        string siegeEventId,
        SiegeParticipants participants,
        string? defenderPartyId)
    {
        string? settlementId = null;
        Server.Call(() => Assert.True(Server.ObjectManager.TryGetId(
            Server.GetRegisteredObject<SiegeEvent>(siegeEventId).BesiegedSettlement, out settlementId)));

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

                    if (defenderPartyId != null)
                    {
                        var defender = instance.GetRegisteredObject<MobileParty>(defenderPartyId);
                        defender._currentSettlement = settlement;
                        settlement._partiesCache.Add(defender);
                    }
                }

                var diplomaticHandler = new PartyDiplomaticHandlerCampaignBehavior();
                Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(
                    new CampaignBehaviorBase[] { diplomaticHandler }));
                diplomaticHandler.RegisterEvents();

                Assert.True(settlement.IsFortification);
                Assert.Same(instance.GetRegisteredObject<Kingdom>(participants.DefenderKingdomId), settlement.MapFaction);
            });
        }

        return settlementId!;
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
        string LeaderPartyId,
        string PlayerHeroId,
        string PlayerPartyId);

    private readonly record struct SiegeScenario(
        string SiegeEventId,
        string SettlementId,
        SiegeParticipants Participants);
}
