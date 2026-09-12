using Common;
using System;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.SiegeEvents;

public interface ISiegeJoinMenuActivationGate : IGameAbstraction
{
    void ArmJoinRequest(MapEvent mapEvent, PartyBase joiningParty);
    bool TryDeferActivation();
    bool ResumeAfterSnapshot(MapEvent mapEvent);
    void CancelDeferredActivation();
}

internal sealed class SiegeJoinMenuActivationGate : ISiegeJoinMenuActivationGate
{
    private PartyBase pendingJoiningParty;
    private PlayerEncounter pendingEncounter;
    private MapEvent pendingMapEvent;

    public void ArmJoinRequest(MapEvent mapEvent, PartyBase joiningParty)
    {
        if (!mapEvent.IsSiegeAssault && !mapEvent.IsSallyOut)
            return;

        pendingMapEvent = mapEvent;
        pendingJoiningParty = joiningParty;
    }

    public bool TryDeferActivation()
    {
        var encounter = PlayerEncounter.Current;
        var mapEvent = encounter?._mapEvent;
        if (!ReferenceEquals(mapEvent, pendingMapEvent) ||
            !ReferenceEquals(pendingJoiningParty, PartyBase.MainParty))
            return false;

        if (ReferenceEquals(MobileParty.MainParty?.MapEvent, mapEvent))
        {
            CancelDeferredActivation();
            return false;
        }

        pendingEncounter = encounter;
        return true;
    }

    public bool ResumeAfterSnapshot(MapEvent mapEvent)
    {
        if (!ReferenceEquals(pendingMapEvent, mapEvent))
            return false;

        if (pendingEncounter == null)
        {
            CancelDeferredActivation();
            return false;
        }

        if (!ReferenceEquals(PlayerEncounter.Current, pendingEncounter))
        {
            CancelDeferredActivation();
            return true;
        }

        if (!ReferenceEquals(MobileParty.MainParty?.MapEvent, mapEvent))
            return false;

        CancelDeferredActivation();
        GameMenu.ActivateGameMenu("join_siege_event");
        GameMenu.SwitchToMenu("encounter");
        return true;
    }

    public void CancelDeferredActivation()
    {
        pendingJoiningParty = null;
        pendingEncounter = null;
        pendingMapEvent = null;
    }
}
