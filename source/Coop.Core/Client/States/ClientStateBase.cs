using Common.Messaging;

namespace Coop.Core.Client.States
{
    public abstract class ClientStateBase : IClientState
    {
        protected readonly IClientLogic Logic;
        protected readonly IMessageBroker MessageBroker;

        public ClientStateBase(IClientLogic logic, IMessageBroker messageBroker)
        {
            Logic = logic;
            MessageBroker = messageBroker;
        }

        public abstract void Connect();
        public abstract void Disconnect();
        public abstract void StartCharacterCreation();
        public abstract void LoadSavedData();
        public abstract void ExitGame();
        public abstract void EnterMainMenu();
        public abstract void Dispose();
    }
}
