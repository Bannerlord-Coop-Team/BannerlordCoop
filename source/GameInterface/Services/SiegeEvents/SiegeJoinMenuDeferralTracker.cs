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

    /// <summary>Decides what the snapshot for <paramref name="mapEvent"/> does with the deferred menu; keeps the deferral only for another map event or a main party that has not joined yet.</summary>
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
            return SiegeJoinMenuDeferralResult.None;

        if (pendingEncounter == null)
        {
            Clear();
            return SiegeJoinMenuDeferralResult.None;
        }

        if (!ReferenceEquals(currentEncounter, pendingEncounter))
        {
            Clear();
            return SiegeJoinMenuDeferralResult.DroppedForChangedEncounter;
        }

        if (!ReferenceEquals(mainParty?.MapEvent, mapEvent))
            return SiegeJoinMenuDeferralResult.None;

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
    /// <summary>No deferred menu opens for this snapshot, so the caller takes its normal path; a deferral whose main party has not joined yet stays pending.</summary>
    None,

    /// <summary>The deferred menu should open now.</summary>
    Open,

    /// <summary>The deferral was dropped because the player's encounter changed, so the caller does nothing.</summary>
    DroppedForChangedEncounter,
}
