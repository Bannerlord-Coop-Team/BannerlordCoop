namespace GameInterface.Services.Villages;

// Tracks whether one hostile-action request is waiting for a server reply; Complete clears it.
public interface IHostileActionRequestTracker : IGameAbstraction
{
    bool TryBeginRequest();
    void Complete();
}

internal sealed class HostileActionRequestTracker : IHostileActionRequestTracker
{
    private bool pending;

    public bool TryBeginRequest()
    {
        if (pending) return false;
        pending = true;
        return true;
    }

    public void Complete()
    {
        pending = false;
    }
}
