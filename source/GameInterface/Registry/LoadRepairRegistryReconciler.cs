using Common.Messaging;
using GameInterface.Registry.Auto;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEngines;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace GameInterface.Registry;

/// <summary>
/// The campaign objects held before a load repair pass runs: parties, armies, hero equipment, unended map events and
/// sieges, and owned ships.
/// </summary>
public sealed class LoadRepairSnapshot
{
    internal static readonly LoadRepairSnapshot Empty = new(
        Array.Empty<MobileParty>(),
        Array.Empty<Army>(),
        Array.Empty<HeroEquipmentSnapshot>(),
        Array.Empty<MapEvent>(),
        Array.Empty<SiegeEvent>(),
        Array.Empty<Ship>());

    internal LoadRepairSnapshot(
        MobileParty[] parties,
        Army[] armies,
        HeroEquipmentSnapshot[] heroEquipments,
        MapEvent[] mapEvents,
        SiegeEvent[] siegeEvents,
        Ship[] ships)
    {
        Parties = parties;
        Armies = armies;
        HeroEquipments = heroEquipments;
        MapEvents = mapEvents;
        SiegeEvents = siegeEvents;
        Ships = ships;
    }

    internal MobileParty[] Parties { get; }
    internal Army[] Armies { get; }
    internal HeroEquipmentSnapshot[] HeroEquipments { get; }
    internal MapEvent[] MapEvents { get; }
    internal SiegeEvent[] SiegeEvents { get; }
    internal Ship[] Ships { get; }
}

/// <summary>The battle, civilian and stealth equipment an alive hero held when the snapshot was taken.</summary>
internal readonly struct HeroEquipmentSnapshot
{
    public HeroEquipmentSnapshot(Hero hero)
    {
        Hero = hero;
        Battle = hero.BattleEquipment;
        Civilian = hero.CivilianEquipment;
        Stealth = hero.StealthEquipment;
    }

    public Hero Hero { get; }
    public Equipment Battle { get; }
    public Equipment Civilian { get; }
    public Equipment Stealth { get; }
}

public interface ILoadRepairRegistryReconciler
{
    LoadRepairSnapshot Capture();

    /// <summary>
    /// Unregisters every captured object the campaign no longer holds since <paramref name="snapshot"/> but that is
    /// still registered, and announces each captured map event that ended. A throw for one object is logged and the
    /// rest are still unregistered.
    /// </summary>
    void UnregisterRemoved(LoadRepairSnapshot snapshot);
}

/// <summary>
/// The load repairs run before the server state that lets lifetime patches publish, so an object that vanilla
/// destroyed, ended or replaced during a repair (a participant, or a cascade elsewhere) is never unregistered
/// otherwise and a joining client receives its stale id. This publishes the destroy messages the lifetime patches
/// would have: LifetimePatches for parties, armies, sieges and besieger camps, the RemoveParty postfix for the party's
/// Ai, the FinalizeEventAux postfix for map events (with their MapEventFinalized), the DestroyShipAction postfix for
/// ships, and the Hero.OnDeath and Hero.ResetEquipments prefixes for hero equipment.
/// </summary>
/// <remarks>
/// Each type is held while the list its registry registers from still yields it, so the second registration pass
/// cannot register an unregistered object again: a party while CampaignObjectManager lists it, an army while a kingdom
/// lists it, a map event while MapEventManager lists it unfinalized (its Tick drops finalized events), a siege and its
/// camp while SiegeEventManager lists the siege and it is not ReadyToBeRemoved, and a ship while a campaign party or
/// settlement owns it (DestroyShipAction clears the owner).
/// </remarks>
internal sealed class LoadRepairRegistryReconciler : ILoadRepairRegistryReconciler
{
    private readonly ILogger logger;
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public LoadRepairRegistryReconciler(ILogger logger, IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.logger = logger;
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
    }

    public LoadRepairSnapshot Capture()
    {
        var campaignObjects = Campaign.Current?.CampaignObjectManager;
        if (campaignObjects == null)
            return LoadRepairSnapshot.Empty;

        return new LoadRepairSnapshot(
            campaignObjects.MobileParties.ToArray(),
            CampaignArmies().ToArray(),
            Hero.AllAliveHeroes.Where(hero => hero != null).Select(hero => new HeroEquipmentSnapshot(hero)).ToArray(),
            UnendedMapEvents().ToArray(),
            SiegeContainerLookup.UnendedSieges().ToArray(),
            CampaignShips(campaignObjects).ToArray());
    }

    public void UnregisterRemoved(LoadRepairSnapshot snapshot)
    {
        var campaignObjects = Campaign.Current?.CampaignObjectManager;
        if (campaignObjects == null)
            return;

        // Battles go first, as after the postfix: their listeners still resolve the battle and its parties by id.
        // A battle another path already announced was unregistered by it as well, so each one is announced once here.
        UnregisterReleased(snapshot.MapEvents, UnendedMapEvents(), Announce);

        // The camp before its siege, the order of the two FinalizeSiegeEvent postfixes.
        var unendedSieges = SiegeContainerLookup.UnendedSieges().ToArray();
        UnregisterReleased(snapshot.SiegeEvents.Select(siegeEvent => siegeEvent.BesiegerCamp), unendedSieges.Select(siegeEvent => siegeEvent.BesiegerCamp));
        UnregisterReleased(snapshot.SiegeEvents, unendedSieges);

        UnregisterReleased(snapshot.Ships, CampaignShips(campaignObjects));
        UnregisterReleased(snapshot.Armies, CampaignArmies());
        UnregisterReleased(snapshot.Parties, campaignObjects.MobileParties);

        // MobileParty.RemoveParty keeps the Ai, and its registry id is derived from the party's StringId
        UnregisterReleased(snapshot.Parties.Select(party => party.Ai), campaignObjects.MobileParties.Select(party => party.Ai));

        // RegistryManager.RegisterNewGameObjects registers a replaced equipment under the owner-derived id the
        // old one still holds (a fired wanderer companion's ResetEquipments), so the old one goes first
        foreach (var heroEquipment in snapshot.HeroEquipments)
        {
            var hero = heroEquipment.Hero;
            UnregisterReplaced(hero, heroEquipment.Battle, hero.BattleEquipment);
            UnregisterReplaced(hero, heroEquipment.Civilian, hero.CivilianEquipment);
            UnregisterReplaced(hero, heroEquipment.Stealth, hero.StealthEquipment);
        }
    }

    // Unregisters each captured object that the campaign no longer holds and that nothing else unregistered yet.
    private void UnregisterReleased<T>(IEnumerable<T> captured, IEnumerable<T> held, Action<T> beforeUnregister = null) where T : class
    {
        var stillHeld = new HashSet<T>(held.Where(instance => instance != null));
        foreach (var instance in captured)
        {
            if (instance == null || stillHeld.Contains(instance) || !objectManager.Contains(instance))
                continue;

            beforeUnregister?.Invoke(instance);
            Unregister(instance);
        }
    }

    // a hero that died lets go of its equipment, as the Hero.OnDeath lifetime patch does when it is allowed to run
    private void UnregisterReplaced(Hero hero, Equipment captured, Equipment current)
    {
        if (captured == null || (hero.IsAlive && ReferenceEquals(captured, current)) || !objectManager.Contains(captured))
            return;

        Unregister(captured);
    }

    private void Announce(MapEvent mapEvent)
    {
        try
        {
            logger.Warning("Announcing the end of map event {MapEventId}, finalized during a load repair pass", mapEvent.StringId);
            messageBroker.Publish(mapEvent, new MapEventFinalized(mapEvent));
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to announce the end of map event {MapEventId} finalized during a load repair pass", mapEvent.StringId);
        }
    }

    private void Unregister<T>(T instance) where T : class
    {
        objectManager.TryGetId(instance, out var id);
        try
        {
            logger.Warning("Unregistering {Type} {Id} removed from the campaign during a load repair pass", typeof(T).Name, id);
            messageBroker.Publish(instance, new InstanceDestroyed<T>(instance));
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to unregister {Type} {Id} removed during a load repair pass", typeof(T).Name, id);
        }
    }

    // ArmyRegistry registers armies from the kingdom lists, and a dispersed army leaves its kingdom's list.
    private static IEnumerable<Army> CampaignArmies() =>
        (Campaign.Current?.Kingdoms ?? Enumerable.Empty<Kingdom>()).SelectMany(kingdom => kingdom.Armies);

    // MapEventRegistry registers the listed events, and MapEventManager.Tick drops the finalized ones.
    private static IEnumerable<MapEvent> UnendedMapEvents() =>
        (Campaign.Current?.MapEventManager?.MapEvents ?? Enumerable.Empty<MapEvent>())
            .Where(mapEvent => mapEvent != null && !mapEvent.IsFinalized);

    // The ships ShipRegistry registers: those a campaign party or settlement owns.
    private static IEnumerable<Ship> CampaignShips(CampaignObjectManager campaignObjects) =>
        campaignObjects.MobileParties
            .Where(party => party?.Ships != null)
            .SelectMany(party => party.Ships)
            .Concat(campaignObjects.Settlements
                .Where(settlement => settlement?.Party?.Ships != null)
                .SelectMany(settlement => settlement.Party.Ships));
}
