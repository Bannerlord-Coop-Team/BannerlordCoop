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
    bool TryDefer(PlayerEncounter currentEncounter, PartyBase mainParty);

    /// <summary>Decides what the snapshot for <paramref name="mapEvent"/> does with the deferred menu, consuming the deferral unless it is not ready.</summary>
    SiegeJoinMenuDeferralResult Consume(MapEvent mapEvent, PlayerEncounter currentEncounter, PartyBase mainParty);

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
    public bool TryDefer(PlayerEncounter currentEncounter, PartyBase mainParty)
    {
        var mapEvent = currentEncounter?._mapEvent;
        if (!ReferenceEquals(mapEvent, pendingMapEvent) ||
            !ReferenceEquals(pendingJoiningParty, mainParty))
            return false;

        if (ReferenceEquals(mainParty?.MapEvent, mapEvent))
        {
            Clear();
            return false;
        }

        pendingEncounter = currentEncounter;
        return true;
    }

    /// <summary>Consumes the deferral once the main party is in <paramref name="mapEvent"/> under the same encounter.</summary>
    public SiegeJoinMenuDeferralResult Consume(MapEvent mapEvent, PlayerEncounter currentEncounter, PartyBase mainParty)
    {
        if (!ReferenceEquals(pendingMapEvent, mapEvent))
            return SiegeJoinMenuDeferralResult.NotReady;

        if (pendingEncounter == null)
        {
            Clear();
            return SiegeJoinMenuDeferralResult.NotReady;
        }

        if (!ReferenceEquals(currentEncounter, pendingEncounter))
        {
            Clear();
            return SiegeJoinMenuDeferralResult.DroppedForChangedEncounter;
        }

        if (!ReferenceEquals(mainParty?.MapEvent, mapEvent))
            return SiegeJoinMenuDeferralResult.NotReady;

        Clear();
        return SiegeJoinMenuDeferralResult.Open;
    }

    /// <summary>Drops any pending deferral.</summary>
    public void Clear()
    {
        pendingJoiningParty = null;
        pendingEncounter = null;
        pendingMapEvent = null;
    }
}

/// <summary>
/// What an involved-party snapshot should do about a deferred join_siege_event menu.
/// </summary>
public enum SiegeJoinMenuDeferralResult
{
    /// <summary>No deferred menu is ready for this map event, so the caller takes its normal path.</summary>
    NotReady,

    /// <summary>The deferred menu should open now.</summary>
    Open,

    /// <summary>The deferral was dropped because the player's encounter changed, so the caller does nothing.</summary>
    DroppedForChangedEncounter,
}
