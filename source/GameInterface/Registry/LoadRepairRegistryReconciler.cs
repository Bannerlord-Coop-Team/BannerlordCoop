using Common.Messaging;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Registry;

/// <summary>
/// The campaign parties and armies captured before a load repair pass runs.
/// </summary>
public sealed class LoadRepairSnapshot
{
    internal static readonly LoadRepairSnapshot Empty = new(Array.Empty<MobileParty>(), Array.Empty<Army>());

    internal LoadRepairSnapshot(MobileParty[] parties, Army[] armies)
    {
        Parties = parties;
        Armies = armies;
    }

    internal MobileParty[] Parties { get; }
    internal Army[] Armies { get; }
}

public interface ILoadRepairRegistryReconciler
{
    LoadRepairSnapshot Capture();

    /// <summary>
    /// Unregisters every captured party and army that left the campaign since <paramref name="snapshot"/>
    /// but is still registered. A throw for one object is logged and the rest are still unregistered.
    /// </summary>
    void UnregisterRemoved(LoadRepairSnapshot snapshot);
}

/// <summary>
/// The load repairs run before the server state that lets lifetime patches publish, so a party or army
/// that vanilla destroyed during a repair (a participant, or a cascade elsewhere) is never unregistered
/// otherwise and a joining client receives its stale id.
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

        return new LoadRepairSnapshot(campaignObjects.MobileParties.ToArray(), CampaignArmies().ToArray());
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
            if (campaignParties.Contains(party) || !objectManager.Contains(party))
                continue;

            Unregister(party, party.StringId);
        }
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
