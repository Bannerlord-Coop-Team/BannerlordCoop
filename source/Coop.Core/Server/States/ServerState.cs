using Common.Messaging;

namespace Coop.Core.Server.States;

/// <inheritdoc cref="IServerState"/>
public abstract class ServerStateBase : IServerState
{
    protected IServerLogic Logic;
    public ServerStateBase(IServerLogic logic)
    {
        Logic = logic;
    }

    ~ServerStateBase()
    {
        Dispose();
    }

    public abstract void Dispose();
    public abstract void Start();
    public abstract void Stop();
}
