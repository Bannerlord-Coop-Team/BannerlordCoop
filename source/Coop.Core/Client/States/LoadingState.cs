using Common.Messaging;
using Coop.Core.Client.Messages;
using GameInterface.Services.GameState.Messages;
using System;

namespace Coop.Core.Client.States
{
    /// <summary>
    /// State Logic Controller for the Loading Client State
    /// </summary>
    public class LoadingState : ClientStateBase
    {
        public LoadingState(IClientLogic logic) : base(logic)
        {
            Logic.NetworkMessageBroker.Subscribe<GameLoaded>(Handle);
            Logic.NetworkMessageBroker.Subscribe<MainMenuEntered>(Handle);
        }

        public override void Dispose()
        {
            Logic.NetworkMessageBroker.Unsubscribe<GameLoaded>(Handle);
            Logic.NetworkMessageBroker.Unsubscribe<MainMenuEntered>(Handle);
        }

        public override void EnterMainMenu()
        {
            Logic.NetworkMessageBroker.Publish(this, new EnterMainMenu());
        }

        private void Handle(MessagePayload<MainMenuEntered> obj)
        {
            Logic.State = new MainMenuState(Logic);
        }

        private void Handle(MessagePayload<GameLoaded> obj)
        {
            Logic.ResolveNetworkGuids();
        }

        public override void ResolveNetworkGuids()
        {
            Logic.State = new ResolveNetworkGuidsState(Logic);
        }

        public override void Connect()
        {
        }

        public override void Disconnect()
        {
            Logic.NetworkMessageBroker.Publish(this, new EnterMainMenu());
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

        public override void EnterCampaignState()
        {
        }

        public override void EnterMissionState()
        {
        }

        public override void ValidateModules()
        {
        }
    }
}
