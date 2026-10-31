using Common.Messaging;
using Coop.Core.Server.Services.Save.Messages;
using E2E.Tests.Environment;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.Policies;
using GameInterface.Registry;
using GameInterface.Services.Heroes.HeirSelection.Interfaces;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Registry;

/// <summary>
/// The load repairs run while the server's sync policy still allows originals, so the lifetime patches publish
/// nothing for what vanilla ends or destroys during them. These tests run the repairs in that window, the way
/// InitialServerState does at CampaignReady, and check that whatever the repairs ended leaves the registry and
/// that an ended battle is announced.
/// </summary>
public class LoadRepairLifetimeTests : MapEventTestBase
{
    public LoadRepairLifetimeTests(ITestOutputHelper output) : base(output)
    {
    }

    // test heroes have no encyclopedia data, which the obituary and retirement log text need, and a disbanded army
    // refreshes visibility against the main party
    private IReadOnlyList<MethodBase> RepairDisabledMethods => MapEventDisabledMethods
        .Append(AccessTools.Method(typeof(KillCharacterAction), "CreateObituary"))
        .Append(AccessTools.Method(typeof(ApplyHeirSelectionActionInterface), "RecordRetirement"))
        .Append(AccessTools.Method(typeof(PartyBaseVisibilityExtensions), nameof(PartyBaseVisibilityExtensions.UpdateVisibilityAndInspected)))
        .Append(AccessTools.Method(typeof(CultureObject), nameof(CultureObject.HasFeat)))
        .ToList();

    // a siege built without a scene: the camp positions and both sides' engine setup need a town scene
    private IReadOnlyList<MethodBase> SiegeDisabledMethods => RepairDisabledMethods
        .Append(AccessTools.Method(typeof(MobileParty), "OnPartyJoinedSiegeInternal"))
        .Append(AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide)))
        .Append(AccessTools.Method(typeof(Settlement), nameof(Settlement.InitializeSiegeEventSide)))
        .ToList();

    [Fact]
    public void RunLoadRepairs_UnregisteredOrphanPlayerEvent_AnnouncesItsEndAndParksTheOfflineParty()
    {
        // the map event registry registers only the events the manager lists, while the cleaner also finalizes an
        // unlisted event a saved player party still points at, so in production that event is not registered
        var mapEventContext = CreateServerMapEvent(commit: false);
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        RegisterAsPlayerParty("offline-player", heroId, mapEventContext.AttackerPartyId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.AttackerPartyId, out var playerParty));
            Assert.DoesNotContain(mapEvent, Campaign.Current.MapEventManager.MapEvents);
            Assert.True(Server.ObjectManager.Remove(mapEvent));

            Server.Resolve<IMessageBroker>().Publish(this, new SavedPlayerRegistrationsRestored());
            Assert.True(playerParty.IsActive);
            Assert.Same(mapEvent, playerParty.MapEvent);

            RunLoadRepairsInBootWindow();

            Assert.True(mapEvent.IsFinalized);
            Assert.Null(playerParty.Party.MapEventSide);
            Assert.Equal(1, FinalizedAnnouncements(mapEvent));
            Assert.False(playerParty.IsActive);
            Assert.False(playerParty.IsVisible);
        }, RepairDisabledMethods);
    }

    [Fact]
    public void RunLoadRepairs_ClanClosedWithAWarPartyInABattle_AnnouncesAndUnregistersTheBattle()
    {
        // DestroyClanAction takes a war party out of its battle, and vanilla finalizes a battle whose side loses its
        // last party without the FinalizeEventAux postfix publishing anything in this window
        var mapEventContext = CreateServerMapEvent(commit: true);
        var clanId = CreateKingdomClan();
        JoinClan(mapEventContext.AttackerPartyId, clanId);

        RunWithRemovedHostParty(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            var clan = Server.GetRegisteredObject<Clan>(clanId);

            RunLoadRepairsInBootWindow();

            Assert.True(clan.IsEliminated);
            Assert.True(mapEvent.IsFinalized);
            Assert.Equal(1, FinalizedAnnouncements(mapEvent));
            Assert.False(Server.ObjectManager.Contains(mapEvent));
            Assert.DoesNotContain(mapEvent, Campaign.Current.MapEventManager.MapEvents);
        }, RepairDisabledMethods);
    }

    [Fact]
    public void RunLoadRepairs_ClanClosedWithItsOnlyBesieger_UnregistersTheSiegeAndCamp()
    {
        // the besieger's RemoveParty clears its camp, and a camp left with no besiegers ends the siege
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var clanId = CreateKingdomClan();
        JoinClan(partyId, clanId);

        SiegeEvent? siegeEvent = null;
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            siegeEvent = new SiegeEvent(settlement, party);
            siegeEvent.BesiegerCamp.SiegeEngines = new SiegeEvent.SiegeEnginesContainer(BattleSideEnum.Attacker, null);
            settlement.SiegeEngines = new SiegeEvent.SiegeEnginesContainer(BattleSideEnum.Defender, null);
            siegeEvent.BesiegerCamp._besiegerParties.Add(party);
            siegeEvent.BesiegerCamp._leaderParty = party;
            siegeEvent.BesiegerCamp._faction = party.MapFaction;
            Campaign.Current.SiegeEventManager._siegeEvents.Add(siegeEvent);

            Assert.True(Server.ObjectManager.Contains(siegeEvent));
            Assert.True(Server.ObjectManager.Contains(siegeEvent.BesiegerCamp));
            Assert.True(Server.ObjectManager.Contains(siegeEvent.BesiegerCamp.SiegeEngines));
            Assert.True(Server.ObjectManager.Contains(settlement.SiegeEngines));
            Assert.Same(siegeEvent.BesiegerCamp, party.BesiegerCamp);
        }, SiegeDisabledMethods);

        RunWithRemovedHostParty(() =>
        {
            var camp = siegeEvent!.BesiegerCamp;

            RunLoadRepairsInBootWindow();

            Assert.True(Server.GetRegisteredObject<Clan>(clanId).IsEliminated);
            Assert.True(siegeEvent.ReadyToBeRemoved);
            // the second registration pass runs last, so the ended siege must not have been registered again
            Assert.False(Server.ObjectManager.Contains(siegeEvent));
            Assert.False(Server.ObjectManager.Contains(camp));
            // finalizing a siege leaves both engine containers referenced, so the pass must skip the ended siege
            Assert.False(Server.ObjectManager.Contains(camp.SiegeEngines));
            Assert.False(Server.ObjectManager.Contains(siegeEvent.BesiegedSettlement.SiegeEngines));
        }, SiegeDisabledMethods);
    }

    [Fact]
    public void RunLoadRepairs_ClanClosedWithAWarPartyOwningAShip_UnregistersTheShip()
    {
        // MobileParty.RemoveParty destroys every ship the party owns, and the DestroyShipAction postfix publishes
        // nothing in this window
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var clanId = CreateKingdomClan();
        JoinClan(partyId, clanId);

        Ship? ship = null;
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            ship = GameObjectCreator.CreateInitializedObject<Ship>();
            ship.Owner = party.Party;

            Assert.Contains(ship, party.Ships);
            Assert.True(Server.ObjectManager.Contains(ship));
        }, RepairDisabledMethods);

        RunWithRemovedHostParty(() =>
        {
            RunLoadRepairsInBootWindow();

            Assert.True(Server.GetRegisteredObject<Clan>(clanId).IsEliminated);
            Assert.Null(ship!.Owner);
            Assert.False(Server.ObjectManager.Contains(ship));
        }, RepairDisabledMethods);
    }

    private int FinalizedAnnouncements(MapEvent mapEvent) =>
        Server.InternalMessages.GetMessages<MapEventFinalized>().Count(message => ReferenceEquals(message.MapEvent, mapEvent));

    private void RunLoadRepairsInBootWindow()
    {
        var policy = (TestPolicy)Server.Resolve<ISyncPolicy>();
        var previous = policy.AllowOriginals;
        policy.AllowOriginals = true;
        try
        {
            Server.Resolve<ILoadRepairRunner>().RunLoadRepairs();
        }
        finally
        {
            policy.AllowOriginals = previous;
        }
    }

    // InitialServerState removes the host's own party before the repairs, so production has a main party that is in
    // no battle and no settlement, which DestroyClanAction and SiegeEvent.FinalizeSiegeEvent both read
    private void RunWithRemovedHostParty(Action action, IReadOnlyList<MethodBase> disabledMethods)
    {
        Server.Call(() =>
        {
            var previousMainParty = Campaign.Current.MainParty;
            var hostParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            hostParty.SetCurrentSettlementDirectly(null);
            Campaign.Current.MainParty = hostParty;
            try
            {
                action();
            }
            finally
            {
                Campaign.Current.MainParty = previousMainParty;
            }
        }, disabledMethods);
    }

    // a kingdom clan with no leader and nobody who can lead it, which the clan repair closes
    private string CreateKingdomClan()
    {
        string? clanId = null;
        Server.Call(() =>
        {
            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            var clan = Clan.CreateClan("LoadRepairLifetimeClan");
            clan._defaultPartyTemplate = GameObjectCreator.CreateInitializedObject<PartyTemplateObject>();
            clan.Culture = GameObjectCreator.CreateInitializedObject<CultureObject>();
            clan._kingdom = kingdom;
            kingdom._clans.Add(clan);
            Assert.True(Server.ObjectManager.TryGetId(clan, out clanId));
        });
        return clanId!;
    }

    // makes the party one of the clan's war parties, the list DestroyClanAction destroys
    private void JoinClan(string partyId, string clanId)
    {
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            var clan = Server.GetRegisteredObject<Clan>(clanId);
            party.ActualClan = clan;
            Assert.Contains(party.WarPartyComponent, clan.WarPartyComponents);
        }, RepairDisabledMethods);
    }
}
