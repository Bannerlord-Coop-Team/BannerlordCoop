using TaleWorlds.CampaignSystem.Encounters;

namespace GameInterface.Services.Villages;

// Tracks the one encounter waiting for a server reply; Take returns it and clears the slot.
public interface IHostileActionRequestTracker : IGameAbstraction
{
    bool TryTrackCurrentEncounter(bool finishEncounterOnDenied);
    PlayerEncounter Take(out bool finishEncounterOnDenied);
}

internal sealed class HostileActionRequestTracker : IHostileActionRequestTracker
{
    private PlayerEncounter pending;
    private bool finishEncounterOnDenied;

    public bool TryTrackCurrentEncounter(bool finishEncounterOnDenied)
    {
        if (pending != null) return false;
        pending = PlayerEncounter.Current;
        this.finishEncounterOnDenied = finishEncounterOnDenied;
        return true;
    }

    public PlayerEncounter Take(out bool finishEncounter)
    {
        var encounter = pending;
        finishEncounter = finishEncounterOnDenied;
        pending = null;
        finishEncounterOnDenied = false;
        return encounter;
    }
}
