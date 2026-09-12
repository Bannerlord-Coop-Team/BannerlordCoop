using Common;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties;

public interface IPartyVisibilitySweep
{
    void RebuildAroundMainParty();
}

/// <summary>
/// Rebuilds fog of war around the local main party, mirroring the native
/// <c>Campaign.GameInitTick</c> pass that otherwise only runs when a new campaign is created.
/// Loading a save restores each party's persisted visibility verbatim, and the native per-tick
/// sweep (<c>CampaignTickCacheDataStore.UpdateVisibilitiesAroundMainParty</c>) only re-evaluates
/// parties within seeing range of the main party — visibility is sticky everywhere else. A joining
/// client loads the server's transferred save, where <see cref="Patches.PartyVisibilityOnServerPatch"/>
/// keeps every party visible, so without this pass the whole map stays revealed for the client.
/// </summary>
internal sealed class PartyVisibilitySweep : IPartyVisibilitySweep
{
    public void RebuildAroundMainParty()
    {
        // The server (and debug builds via the visibility patches) intentionally keeps everything
        // visible; sweeping there would only churn visibility-changed events.
        if (ModInformation.IsServer) return;

        var mainParty = MobileParty.MainParty;
        // Vec2-level validity is all the distance math below needs; CampaignVec2.IsValid() would
        // additionally require a resolved navigation face, which the native code only needs for its
        // locator-grid proximity search (this sweep iterates the campaign lists directly).
        if (Campaign.Current == null || mainParty == null || !mainParty.Position.ToVec2().IsValid) return;

        // Order matters: UpdateVisibilityAndInspected copies dependent state that must already be
        // recomputed — besieging/raiding parties read their settlement's IsInspected, and attached
        // army followers copy their leader's visibility. So: settlements, then army leaders and
        // unattached parties, then attached followers. A follower swept before its leader would
        // keep the server save's stale visibility.
        foreach (var settlement in Settlement.All)
        {
            settlement.Party.UpdateVisibilityAndInspected(mainParty.Position);
        }

        foreach (var mobileParty in Campaign.Current.MobileParties)
        {
            if (IsAttachedFollower(mobileParty)) continue;
            mobileParty.Party.UpdateVisibilityAndInspected(mainParty.Position);
        }

        foreach (var mobileParty in Campaign.Current.MobileParties)
        {
            if (!IsAttachedFollower(mobileParty)) continue;
            mobileParty.Party.UpdateVisibilityAndInspected(mainParty.Position);
        }
    }

    /// <summary>
    /// Mirrors the attached-party check in the native <c>PartyBase.CalculateVisibilityAndInspected</c>,
    /// which short-circuits these parties to their army leader's current visibility.
    /// </summary>
    private static bool IsAttachedFollower(MobileParty mobileParty) =>
        mobileParty.Army != null &&
        mobileParty.Army.LeaderParty.AttachedParties.IndexOf(mobileParty) >= 0;
}
