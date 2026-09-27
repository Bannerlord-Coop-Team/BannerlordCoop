namespace Coop.Core.Server.Services.Shutdown;

/// <summary>Decides whether the server accepts new connections while a restart is pending.</summary>
public interface IServerAdmissionGate
{
    bool IsOpen { get; }
    void Open();
    void Close();
}

/// <inheritdoc cref="IServerAdmissionGate"/>
public class ServerAdmissionGate : IServerAdmissionGate
{
    // Volatile because the network poll thread reads it while the game thread changes it.
    private volatile bool isOpen = true;

    public bool IsOpen => isOpen;

    public void Open() => isOpen = true;

    public void Close() => isOpen = false;
}
