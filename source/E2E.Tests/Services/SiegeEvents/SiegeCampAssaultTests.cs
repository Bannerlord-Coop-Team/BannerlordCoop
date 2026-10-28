using Common;
using Common.Network;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.Villages.Interfaces;
using HarmonyLib;
using Moq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

/// <summary>
/// "Assault the siege camp" from a besieged settlement's menu (#4105, #4137).
/// </summary>
public class SiegeCampAssaultTests : MapEventTestBase
{
    private static readonly MethodInfo StartBattleInternal =
        AccessTools.Method(typeof(PlayerEncounter), "StartBattleInternal");

    private IReadOnlyList<MethodBase> DisabledMethods => WithoutNetworkDelivery()
        .Append(AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyJoinedSiegeInternal)))
        .Append(AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide)))
        .Append(AccessTools.Method(typeof(Settlement), nameof(Settlement.InitializeSiegeEventSide)))
        .Append(AccessTools.Method(typeof(ChangeRelationAction), "ApplyInternal"))
        // Activating a menu needs a rendered map state. The encounter menu init is the StartBattle call below.
        .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ActivateGameMenu), new[] { typeof(string) }))
        .ToList();

    public SiegeCampAssaultTests(ITestOutputHelper output) : base(output)
    {
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AssaultSiegeCamp_FriendlySettlement_AttacksCampNotSettlement(bool ownKingdom)
    {
        var client = Clients.First();
        var context = CreateContext(client, ownKingdom ? OwnerStance.Own : OwnerStance.Allied);
        string? playerId = null;
        string? campId = null;

        client.Call(() =>
        {
            var encounter = StageSettlementEncounter(client, context);

            new EncounterGameMenuBehavior().game_menu_join_encounter_help_defenders_on_consequence(
                new MenuCallbackArgs((MenuContext)null, null));

            // The restart onto the camp waits for the server, so the encounter still targets the settlement.
            Assert.Same(encounter, PlayerEncounter.Current);
            Assert.True(encounter._defenderParty.IsSettlement);

            Assert.Null(StartBattleInternal.Invoke(encounter, Array.Empty<object>()));

            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(context.CampLeaderId, out var campLeader));
            Assert.Same(campLeader.Party, encounter._defenderParty);
            Assert.Equal(BattleSideEnum.Attacker, encounter.PlayerSide);
            Assert.True(client.ObjectManager.TryGetId(encounter._attackerParty, out playerId));
            Assert.True(client.ObjectManager.TryGetId(campLeader.Party, out campId));
        }, DisabledMethods);

        var restart = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestConversation>());
        Assert.Equal(campId, restart.DefenderId);

        var request = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestCreateMapEvent>());
        Assert.Equal(playerId, request.AttackerId);
        Assert.Equal(campId, request.DefenderId);
    }

    [Fact]
    public void AssaultSiegeCamp_HostileSettlement_KeepsSettlementTarget()
    {
        var client = Clients.First();
        var context = CreateContext(client, OwnerStance.Hostile);
        string? settlementId = null;

        client.Call(() =>
        {
            var encounter = StageSettlementEncounter(client, context);

            Assert.Null(StartBattleInternal.Invoke(encounter, Array.Empty<object>()));

            Assert.True(encounter._defenderParty.IsSettlement);
            Assert.True(client.ObjectManager.TryGetId(encounter._defenderParty, out settlementId));
        }, DisabledMethods);

        var request = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestCreateMapEvent>());
        Assert.Equal(settlementId, request.DefenderId);
    }

    private SiegeContext CreateContext(EnvironmentInstance client, OwnerStance stance)
    {
        var (_, partyId) = CreatePlayerHeroParty("PlayerOne");
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var ownerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var campLeaderId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        SetMapEventCreationTimeout(client, TimeSpan.FromMilliseconds(1));
        client.NetworkSentMessages.Clear();

        return new SiegeContext(partyId, settlementId, townId, ownerClanId, campLeaderId, stance);
    }

    private static PlayerEncounter StageSettlementEncounter(EnvironmentInstance client, SiegeContext context)
    {
        Assert.True(client.ObjectManager.TryGetObject<MobileParty>(context.PartyId, out var party));
        Assert.True(client.ObjectManager.TryGetObject<Settlement>(context.SettlementId, out var settlement));
        Assert.True(client.ObjectManager.TryGetObject<Town>(context.TownId, out var town));
        Assert.True(client.ObjectManager.TryGetObject<Clan>(context.OwnerClanId, out var ownerClan));
        Assert.True(client.ObjectManager.TryGetObject<MobileParty>(context.CampLeaderId, out var campLeader));

        using (new AllowedThread())
        {
            Campaign.Current.MainParty = party;
            settlement.SetSettlementComponent(town);
            town.OwnerClan = context.Stance == OwnerStance.Own ? party.ActualClan : ownerClan;
            if (context.Stance == OwnerStance.Hostile)
                VillageHostileFactionStanceHelper.ApplyWarStance(party.ActualClan, ownerClan);

            // An AI party besieging the town.
            town.OwnerClan = null;
            var siegeEvent = new SiegeEvent(settlement, campLeader);
            town.OwnerClan = context.Stance == OwnerStance.Own ? party.ActualClan : ownerClan;
            siegeEvent.BesiegerCamp._besiegerParties.Add(campLeader);
            siegeEvent.BesiegerCamp._leaderParty = campLeader;
            campLeader._besiegerCamp = siegeEvent.BesiegerCamp;
        }

        Assert.True(settlement.IsFortification);
        Assert.Same(campLeader, settlement.SiegeEvent.BesiegerCamp.LeaderParty);
        Assert.Equal(
            context.Stance == OwnerStance.Hostile,
            party.MapFaction.IsAtWarWith(settlement.MapFaction));

        // The settlement encounter that opens the join_siege_event menu.
        var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
        encounter._attackerParty = party.Party;
        encounter._defenderParty = settlement.Party;
        encounter._encounteredParty = settlement.Party;
        encounter.EncounterSettlementAux = settlement;
        Campaign.Current.PlayerEncounter = encounter;
        return encounter;
    }

    private static void SetMapEventCreationTimeout(EnvironmentInstance instance, TimeSpan timeout)
    {
        instance.Call(() =>
        {
            var config = new Mock<INetworkConfig>();
            config.SetupGet(x => x.ObjectCreationTimeout).Returns(timeout);

            var coordinator = instance.Resolve<MapEventCreationCoordinator>();
            AccessTools.Field(typeof(MapEventCreationCoordinator), "configuration").SetValue(coordinator, config.Object);
        });
    }

    private enum OwnerStance
    {
        Own,
        Allied,
        Hostile,
    }

    private sealed record SiegeContext(
        string PartyId,
        string SettlementId,
        string TownId,
        string OwnerClanId,
        string CampLeaderId,
        OwnerStance Stance);
}
