using Common.Messaging;

namespace Coop.Mod.LogicStates.Client
{
    internal class MainMenuState : ClientStateBase
    {
        public MainMenuState(IClientLogic logic, IMessageBroker messageBroker) : base(logic, messageBroker)
        {
        }

        public override void Connect()
        {
            //TODO: connect
        }

        #region unused
        public override void Disconnect()
        {
        }

        public override void EnterMainMenu()
        {
        }

        public override void ExitGame()
        {
        }

        public override void LoadSavedData()
        {
        }

        public override void StartCharacterCreation()
        {
        }
        #endregion
    }
}
