using Common;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.SiegeEvents;

/// <summary>
/// Tracks the client's one pending siege assault or sally-out join whose join_siege_event menu waits for the server's involved-party snapshot.
/// </summary>
public interface ISiegeJoinMenuDeferralTracker : IGameAbstraction
{
    /// <summary>Records a join request so its menu activation can be deferred; ignores non-siege battles.</summary>
    void Arm(MapEvent mapEvent, PartyBase joiningParty);

    /// <summary>Returns true when the current join_siege_event activation should wait for the snapshot, otherwise drops the deferral if the main party already joined.</summary>
    bool TryDefer();

    /// <summary>Returns true when the snapshot for <paramref name="mapEvent"/> should open the deferred menu now.</summary>
    /// <param name="droppedForChangedEncounter">Only meaningful when returning false: true when the deferral was dropped because the player's encounter changed.</param>
    bool TryConsume(MapEvent mapEvent, out bool droppedForChangedEncounter);

    /// <summary>Drops any pending deferral.</summary>
    void Clear();
}

/// <summary>
/// Holds the pending join's map event, party, and the encounter whose menu activation was deferred.
/// </summary>
internal sealed class SiegeJoinMenuDeferralTracker : ISiegeJoinMenuDeferralTracker
{
    private PartyBase pendingJoiningParty;
    private PlayerEncounter pendingEncounter;
    private MapEvent pendingMapEvent;

    /// <summary>Records a siege assault or sally-out join request.</summary>
    public void Arm(MapEvent mapEvent, PartyBase joiningParty)
    {
        if (!mapEvent.IsSiegeAssault && !mapEvent.IsSallyOut)
            return;

        pendingMapEvent = mapEvent;
        pendingJoiningParty = joiningParty;
    }

    /// <summary>Defers the activation when it belongs to the main party's pending join that is not applied yet.</summary>
    public bool TryDefer()
    {
        var encounter = PlayerEncounter.Current;
        var mapEvent = encounter?._mapEvent;
        if (!ReferenceEquals(mapEvent, pendingMapEvent) ||
            !ReferenceEquals(pendingJoiningParty, PartyBase.MainParty))
            return false;

        if (ReferenceEquals(MobileParty.MainParty?.MapEvent, mapEvent))
        {
            Clear();
            return false;
        }

        pendingEncounter = encounter;
        return true;
    }

    /// <summary>Consumes the deferral once the main party is in <paramref name="mapEvent"/> under the same encounter.</summary>
    public bool TryConsume(MapEvent mapEvent, out bool droppedForChangedEncounter)
    {
        droppedForChangedEncounter = false;
        if (!ReferenceEquals(pendingMapEvent, mapEvent))
            return false;

        if (pendingEncounter == null)
        {
            Clear();
            return false;
        }

        if (!ReferenceEquals(PlayerEncounter.Current, pendingEncounter))
        {
            Clear();
            droppedForChangedEncounter = true;
            return false;
        }

        if (!ReferenceEquals(MobileParty.MainParty?.MapEvent, mapEvent))
            return false;

        Clear();
        return true;
    }

    /// <summary>Drops any pending deferral.</summary>
    public void Clear()
    {
        pendingJoiningParty = null;
        pendingEncounter = null;
        pendingMapEvent = null;
    }
}
