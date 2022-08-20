using Common.Messaging;
using System;

namespace Coop.Core.Client.States
{
    internal class ReceivingSavedDataState : ClientStateBase
    {
        public ReceivingSavedDataState(IClientLogic logic, IMessageBroker messageBroker) : base(logic, messageBroker)
        {
        }

        public override void Dispose()
        {
            throw new NotImplementedException();
        }

        public override void Connect()
        {
            throw new NotImplementedException();
        }

        public override void Disconnect()
        {
            throw new NotImplementedException();
        }

        public override void EnterMainMenu()
        {
            throw new NotImplementedException();
        }

        public override void ExitGame()
        {
            throw new NotImplementedException();
        }

        public override void LoadSavedData()
        {
            throw new NotImplementedException();
        }

        public override void StartCharacterCreation()
        {
            throw new NotImplementedException();
        }
    }
}
