namespace Coop.Core.Server.Connections.States;

/// <summary>
/// Setup for a given ConnectionState
/// </summary>
public abstract class ConnectionStateBase : IConnectionState
{
    public IConnectionLogic ConnectionLogic { get; }

    public ConnectionStateBase(IConnectionLogic connectionLogic)
    {
        ConnectionLogic = connectionLogic;
    }

    /// <summary>
    /// Connections do not block time by default; loading states override this.
    /// </summary>
    public virtual bool IsLoading => false;

    /// <summary>
    /// Connections have not finished joining by default; the in-game states override this.
    /// </summary>
    public virtual bool HasJoinedGame => false;

    public abstract void CreateCharacter();
    public abstract void TransferSave();
    public abstract void Load();
    public abstract void EnterCampaign();
    public abstract void EnterMission();
    public abstract void Dispose();
}
