using Common.Messaging;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.CharacterCreation.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.GameState.Messages;
using Common.Network;

namespace Coop.Core.Client.States
{
    /// <summary>
    /// State controller for the character creation client state
    /// </summary>
    public class CharacterCreationState : ClientStateBase
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        public CharacterCreationState(IClientLogic logic) : base(logic)
        {
            messageBroker = logic.MessageBroker;
            network = logic.Network;
            messageBroker.Subscribe<NewHeroPackaged>(Handle_NewHeroPackaged);
            messageBroker.Subscribe<CharacterCreationFinished>(Handle_CharacterCreationFinished);
            messageBroker.Subscribe<MainMenuEntered>(Handle_MainMenuEntered);
        }

        public override void Dispose()
        {
            messageBroker.Unsubscribe<NewHeroPackaged>(Handle_NewHeroPackaged);
            messageBroker.Unsubscribe<CharacterCreationFinished>(Handle_CharacterCreationFinished);
            messageBroker.Unsubscribe<MainMenuEntered>(Handle_MainMenuEntered);
        }

        internal void Handle_NewHeroPackaged(MessagePayload<NewHeroPackaged> obj)
        {
            network.SendAll(new NetworkTransferedHero(obj.What.Package));

            Logic.LoadSavedData();
        }

        internal void Handle_CharacterCreationFinished(MessagePayload<CharacterCreationFinished> obj)
        {
            messageBroker.Publish(this, new PackageMainHero());
        }

        internal void Handle_MainMenuEntered(MessagePayload<MainMenuEntered> obj)
        {
            Logic.State = new MainMenuState(Logic);
        }

        public override void EnterMainMenu()
        {
            messageBroker.Publish(this, new EnterMainMenu());
        }

        public override void Connect()
        {
        }

        public override void Disconnect()
        {
            messageBroker.Publish(this, new EnterMainMenu());
        }

        public override void ExitGame()
        {
        }

        public override void LoadSavedData()
        {
            Logic.State = new ReceivingSavedDataState(Logic);
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
