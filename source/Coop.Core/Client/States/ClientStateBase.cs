namespace Coop.Core.Client.States;

/// <summary>
/// Base implementation for all client state controllers
/// </summary>
public abstract class ClientStateBase : IClientState
{
    protected readonly IClientLogic Logic;

    public ClientStateBase(IClientLogic logic)
    {
        Logic = logic;
    }

    /// <summary>
    /// Hook for entry side effects (message publishes, network sends). SetState calls it on every new state
    /// after it becomes the current state; a constructor would run too early, while the previous state is
    /// still current. No-op by default: only states with entry side effects override it.
    /// </summary>
    public virtual void Enter()
    {
    }

    /// <inheritdoc/>
    public abstract void Dispose();

    /// <inheritdoc/>
    public abstract void Connect();

    /// <inheritdoc/>
    public abstract void Disconnect();

    /// <inheritdoc/>
    public abstract void StartCharacterCreation();

    /// <inheritdoc/>
    public abstract void LoadSavedData();

    /// <inheritdoc/>
    public abstract void ExitGame();

    /// <inheritdoc/>
    public abstract void EnterMainMenu();

    /// <inheritdoc/>
    public abstract void EnterCampaignState();

    /// <inheritdoc/>
    public abstract void EnterMissionState();

    /// <inheritdoc/>
    public abstract void ValidateModules();
}
