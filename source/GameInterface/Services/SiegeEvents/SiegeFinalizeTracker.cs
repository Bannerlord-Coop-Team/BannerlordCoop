using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeEvents;

/// <summary>
/// Tracks the sieges the server is finalizing and the parties their camps held before vanilla emptied them.
/// </summary>
public interface ISiegeFinalizeTracker
{
    /// <summary>Marks the siege as finalizing; returns false when an outer finalize of it is already running.</summary>
    bool TryBeginFinalize(SiegeEvent siegeEvent);

    /// <summary>Clears the finalizing mark of the siege.</summary>
    void EndFinalize(SiegeEvent siegeEvent);

    /// <summary>Returns true while the siege is finalizing.</summary>
    bool IsFinalizing(SiegeEvent siegeEvent);

    /// <summary>Stores the parties camped at the siege, replacing an earlier snapshot.</summary>
    void RecordCampParties(SiegeEvent siegeEvent, MobileParty[] parties);

    /// <summary>Returns and drops the camp snapshot of the siege, or null when none was recorded.</summary>
    MobileParty[] TakeCampParties(SiegeEvent siegeEvent);
}

/// <summary>
/// Holds the finalizing sieges and the emptied camp snapshots for one campaign session.
/// </summary>
internal sealed class SiegeFinalizeTracker : ISiegeFinalizeTracker
{
    private readonly HashSet<SiegeEvent> finalizingSieges = new HashSet<SiegeEvent>();
    // Weak keys so a siege that never finalizes cannot keep its parties alive
    private readonly ConditionalWeakTable<SiegeEvent, MobileParty[]> emptiedCampParties = new ConditionalWeakTable<SiegeEvent, MobileParty[]>();

    public bool TryBeginFinalize(SiegeEvent siegeEvent) => finalizingSieges.Add(siegeEvent);

    public void EndFinalize(SiegeEvent siegeEvent) => finalizingSieges.Remove(siegeEvent);

    public bool IsFinalizing(SiegeEvent siegeEvent) => finalizingSieges.Contains(siegeEvent);

    public void RecordCampParties(SiegeEvent siegeEvent, MobileParty[] parties)
    {
        emptiedCampParties.Remove(siegeEvent);
        emptiedCampParties.Add(siegeEvent, parties);
    }

    public MobileParty[] TakeCampParties(SiegeEvent siegeEvent)
    {
        emptiedCampParties.TryGetValue(siegeEvent, out var parties);
        emptiedCampParties.Remove(siegeEvent);
        return parties;
    }
}
