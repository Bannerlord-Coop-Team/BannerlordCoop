using Common.Messaging;
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
        public ResolveNetworkGuidsState(IClientLogic logic) : base(logic)
        {
            Logic.NetworkMessageBroker.Subscribe<MainMenuEntered>(Handle);
            Logic.NetworkMessageBroker.Subscribe<NetworkGuidsResolved>(Handle);

#if DEBUG
            EnterCampaignState();
#else
            Logic.NetworkMessageBroker.Publish(this, new ResolveNetworkGuids());
#endif

        }

        public override void Dispose()
        {
            Logic.NetworkMessageBroker.Unsubscribe<MainMenuEntered>(Handle);
            Logic.NetworkMessageBroker.Unsubscribe<NetworkGuidsResolved>(Handle);
        }

        private void Handle(MessagePayload<NetworkGuidsResolved> obj)
        {
            Logic.EnterCampaignState();
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
