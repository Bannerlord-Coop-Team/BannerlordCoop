using Common.Messaging;

namespace Coop.Core.Client.States
{
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
}
