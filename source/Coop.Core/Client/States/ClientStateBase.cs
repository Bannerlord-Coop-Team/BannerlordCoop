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
    /// Runs this state's entry side effects. Called by the owning logic's SetState after this state has been
    /// assigned as the current state, so anything observable from here (message publishes, network sends) sees
    /// the state machine already in this state. Side effects in the constructor would instead run while the
    /// previous state is still current.
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
