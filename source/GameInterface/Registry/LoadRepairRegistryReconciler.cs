using Common.Messaging;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Registry;

/// <summary>
/// The campaign parties, armies and hero equipment captured before a load repair pass runs.
/// </summary>
public sealed class LoadRepairSnapshot
{
    internal static readonly LoadRepairSnapshot Empty =
        new(Array.Empty<MobileParty>(), Array.Empty<Army>(), Array.Empty<HeroEquipmentSnapshot>());

    internal LoadRepairSnapshot(MobileParty[] parties, Army[] armies, HeroEquipmentSnapshot[] heroEquipments)
    {
        Parties = parties;
        Armies = armies;
        HeroEquipments = heroEquipments;
    }

    internal MobileParty[] Parties { get; }
    internal Army[] Armies { get; }
    internal HeroEquipmentSnapshot[] HeroEquipments { get; }
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
    /// Unregisters every captured party, party Ai, army and hero equipment that left the campaign since
    /// <paramref name="snapshot"/> but is still registered. A throw for one object is logged and the rest are
    /// still unregistered.
    /// </summary>
    void UnregisterRemoved(LoadRepairSnapshot snapshot);
}

/// <summary>
/// The load repairs run before the server state that lets lifetime patches publish, so an object that
/// vanilla destroyed or replaced during a repair (a participant, or a cascade elsewhere) is never
/// unregistered otherwise and a joining client receives its stale id. This publishes the destroy messages
/// the lifetime patches would have: LifetimePatches for parties and armies, the RemoveParty postfix for the
/// party's Ai, and the Hero.OnDeath and Hero.ResetEquipments prefixes for hero equipment.
/// </summary>
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
            Hero.AllAliveHeroes.Where(hero => hero != null).Select(hero => new HeroEquipmentSnapshot(hero)).ToArray());
    }

    public void UnregisterRemoved(LoadRepairSnapshot snapshot)
    {
        var campaignObjects = Campaign.Current?.CampaignObjectManager;
        if (campaignObjects == null)
            return;

        var campaignArmies = new HashSet<Army>(CampaignArmies());
        foreach (var army in snapshot.Armies)
        {
            if (campaignArmies.Contains(army) || !objectManager.Contains(army))
                continue;

            Unregister(army, army.LeaderParty?.StringId);
        }

        var campaignParties = new HashSet<MobileParty>(campaignObjects.MobileParties);
        foreach (var party in snapshot.Parties)
        {
            if (campaignParties.Contains(party))
                continue;

            if (objectManager.Contains(party))
                Unregister(party, party.StringId);

            // MobileParty.RemoveParty keeps the Ai, and its registry id is derived from the party's StringId
            var ai = party.Ai;
            if (ai != null && objectManager.Contains(ai))
                Unregister(ai, party.StringId);
        }

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

    // a hero that died lets go of its equipment, as the Hero.OnDeath lifetime patch does when it is allowed to run
    private void UnregisterReplaced(Hero hero, Equipment captured, Equipment current)
    {
        if (captured == null || (hero.IsAlive && ReferenceEquals(captured, current)) || !objectManager.Contains(captured))
            return;

        Unregister(captured, hero.StringId);
    }

    private void Unregister<T>(T instance, string name) where T : class
    {
        try
        {
            logger.Warning("Unregistering {Type} {Id} removed from the campaign during a load repair pass", typeof(T).Name, name);
            messageBroker.Publish(instance, new InstanceDestroyed<T>(instance));
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to unregister {Type} {Id} removed during a load repair pass", typeof(T).Name, name);
        }
    }

    // ArmyRegistry registers armies from the kingdom lists, and a dispersed army leaves its kingdom's list.
    private static IEnumerable<Army> CampaignArmies() =>
        (Campaign.Current?.Kingdoms ?? Enumerable.Empty<Kingdom>()).SelectMany(kingdom => kingdom.Armies);
}
