using TaleWorlds.CampaignSystem.Encounters;

namespace GameInterface.Services.Villages.Interfaces;

public interface IHostileActionRequestTracker : IGameAbstraction
{
    bool TryTrack(PlayerEncounter encounter);
    void Complete();
    bool TryTake(PlayerEncounter encounter);
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

    public void Complete() => pending = null;

    public bool TryTake(PlayerEncounter encounter)
    {
        if (!ReferenceEquals(pending, encounter)) return false;
        pending = null;
        return true;
    }
}
