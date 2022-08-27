using Common.Messaging;
using Coop.Core.Client.Messages;
using System;

namespace Coop.Core.Client.States
{
    internal class MainMenuState : ClientStateBase
    {
        public MainMenuState(IClientLogic logic, IMessageBroker messageBroker) : base(logic, messageBroker)
        {
            MessageBroker.Subscribe<NetworkConnected>(Handle);
        }

        public override void Dispose()
        {
            MessageBroker.Unsubscribe<NetworkConnected>(Handle);
        }

        public override void Connect()
        {
            Logic.NetworkClient.Start();
        }

        private void Handle(MessagePayload<NetworkConnected> obj)
        {

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
