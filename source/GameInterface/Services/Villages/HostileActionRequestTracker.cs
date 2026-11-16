using TaleWorlds.CampaignSystem.Encounters;

namespace GameInterface.Services.Villages;

public interface IHostileActionRequestTracker : IGameAbstraction
{
    bool TryTrack(PlayerEncounter encounter);
    PlayerEncounter Take();
}

internal sealed class HostileActionRequestTracker : IHostileActionRequestTracker
{
    private PlayerEncounter pending;

    public bool TryTrack(PlayerEncounter encounter)
    {
        if (pending != null) return false;
        pending = encounter;
        return true;
    }

    public PlayerEncounter Take()
    {
        var encounter = pending;
        pending = null;
        return encounter;
    }
}
