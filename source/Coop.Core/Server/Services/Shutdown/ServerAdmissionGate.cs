using System;

namespace Coop.Core.Server.Services.Shutdown;

/// <summary>Decides whether the server accepts new connections while a restart is pending.</summary>
public interface IServerAdmissionGate
{
    bool IsOpen { get; }
    void Open();

    /// <summary>Closes the gate once a running <see cref="TryAdmit"/> has returned.</summary>
    void Close();

    /// <summary>Runs <paramref name="register"/> only while the gate is open, and holds off <see cref="Close"/> until it returns.</summary>
    bool TryAdmit(Action register);
}

/// <inheritdoc cref="IServerAdmissionGate"/>
public class ServerAdmissionGate : IServerAdmissionGate
{
    // Close takes this on the game thread, so a registration running under it must not wait for the game thread.
    private readonly object admission = new object();
    // Volatile because the network poll thread reads it while the game thread changes it.
    private volatile bool isOpen = true;

    public bool IsOpen => isOpen;

    public void Open() => isOpen = true;

    public void Close()
    {
        lock (admission)
        {
            isOpen = false;
        }
    }

    public bool TryAdmit(Action register)
    {
        lock (admission)
        {
            if (!isOpen) return false;

            register();
            return true;
        }
    }
}
