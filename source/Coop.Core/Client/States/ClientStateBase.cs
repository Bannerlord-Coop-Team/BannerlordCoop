using Common.Messaging;

namespace Coop.Core.Client.States
{
    /// <summary>
    /// Base implementation for all client state controllers
    /// </summary>
    public abstract class ClientStateBase : IClientState
    {
        protected readonly IClientLogic Logic;
        protected readonly IMessageBroker MessageBroker;

        public ClientStateBase(IClientLogic logic, IMessageBroker messageBroker)
        {
            Logic = logic;
            MessageBroker = messageBroker;
        }

        /// <summary>
        /// Unsubscribe all event listeners on a given state controller
        /// </summary>
        public abstract void Dispose();

        /// <summary>
        /// Connect to Coop Server
        /// </summary>
        public abstract void Connect();

        /// <summary>
        /// Disconnect from Coop Server
        /// </summary>
        public abstract void Disconnect();

        /// <summary>
        /// Begin character creation for Coop Server
        /// </summary>
        public abstract void StartCharacterCreation();

        /// <summary>
        /// Load Data before entering Coop Server
        /// </summary>
        public abstract void LoadSavedData();

        /// <summary>
        /// Ensure data alignment before Entering Coop Server
        /// </summary>
        public abstract void ResolveNetworkGuids();

        /// <summary>
        /// Exit Bannerlord
        /// </summary>
        public abstract void ExitGame();

        /// <summary>
        /// Enter Bannerlord's main menu
        /// </summary>
        public abstract void EnterMainMenu();

        /// <summary>
        /// Join coop server campaign map
        /// </summary>
        public abstract void EnterCampaignState();

        /// <summary>
        /// Join p2p mission (battle) instance
        /// </summary>
        public abstract void EnterMissionState();
    }
}
