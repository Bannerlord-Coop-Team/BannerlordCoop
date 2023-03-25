using Common.Messaging;
using Coop.Core.Client.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Modules.Messages;
using System;

namespace Coop.Core.Client.States
{
    /// <summary>
    /// State Logic Controller for the Resolve Network Guids State
    /// </summary>
    public class ResolveNetworkGuidsState : ClientStateBase
    {
        private Guid transactionId;

        public ResolveNetworkGuidsState(IClientLogic logic) : base(logic)
        {
            Logic.NetworkMessageBroker.Subscribe<MainMenuEntered>(Handle);
            Logic.NetworkMessageBroker.Subscribe<ObjectGuidsResolved>(Handle);

            transactionId = Guid.NewGuid();
            Logic.NetworkMessageBroker.Publish(this, new ResolveObjectGuids(transactionId));
        }

        public override void Dispose()
        {
            Logic.NetworkMessageBroker.Unsubscribe<MainMenuEntered>(Handle);
            Logic.NetworkMessageBroker.Unsubscribe<ObjectGuidsResolved>(Handle);
        }

        private void Handle(MessagePayload<ObjectGuidsResolved> obj)
        {
            if(obj.What.TransactionID == transactionId)
            {
                var evnt = new NetworkObjectGuidsResolved();
                Logic.NetworkMessageBroker.PublishNetworkEvent(evnt);
                Logic.EnterCampaignState();
            }
        }

        public override void EnterCampaignState()
        {
            Logic.State = new CampaignState(Logic);
        }

        public override void EnterMainMenu()
        {
            Logic.NetworkMessageBroker.Publish(this, new EnterMainMenu());
        }

        private void Handle(MessagePayload<MainMenuEntered> obj)
        {
            Logic.State = new MainMenuState(Logic);
        }

        public override void Connect()
        {
        }

        public override void Disconnect()
        {
            Logic.NetworkMessageBroker.Publish(this, new EnterMainMenu());
        }

        public override void EnterMissionState()
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

        public override void ResolveNetworkGuids()
        {
        }

        public override void ValidateModules()
        {
        }
    }
}
