
namespace GameInterface.Services.Villages;

// Tracks whether one hostile-action request is waiting for a server reply; Take clears the slot.
public interface IHostileActionRequestTracker : IGameAbstraction
{
    bool TryTrackCurrentEncounter();
    void Take();
}

internal sealed class HostileActionRequestTracker : IHostileActionRequestTracker
{
    private bool pending;

    public bool TryTrackCurrentEncounter()
    {
        if (pending) return false;
        pending = true;
        return true;
    }

    public void Take()
    {
        pending = false;
    }
}