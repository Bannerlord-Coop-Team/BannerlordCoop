using TaleWorlds.CampaignSystem.Encounters;

namespace GameInterface.Services.Villages;

// Tracks the one encounter waiting for a server reply; Take returns it and clears the slot.
public interface IHostileActionRequestTracker : IGameAbstraction
{
    bool TryTrackCurrentEncounter();
    PlayerEncounter Take();
}

internal sealed class HostileActionRequestTracker : IHostileActionRequestTracker
{
    private PlayerEncounter pending;

    public bool TryTrackCurrentEncounter()
    {
        if (pending != null) return false;
        pending = PlayerEncounter.Current;
        return true;
    }

    public PlayerEncounter Take()
    {
        var encounter = pending;
        pending = null;
        return encounter;
    }
}
