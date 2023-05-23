using Common.Messaging;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.MobileParties.Messages;

namespace Coop.Core.Server.States
{
    public class InitialServerState : ServerStateBase
    {
        public InitialServerState(IServerLogic context, IMessageBroker messageBroker) : base(context, messageBroker)
        {
            MessageBroker.Subscribe<CampaignLoaded>(Handle_GameLoaded);
        }

        public override void Dispose()
        {
            MessageBroker.Unsubscribe<CampaignLoaded>(Handle_GameLoaded);
        }

        internal void Handle_GameLoaded(MessagePayload<CampaignLoaded> payload)
        {
            // Start server when game is fully loaded
            Logic.Network.Start();

            // Remove server party
            MessageBroker.Publish(this, new RemoveMainParty());

            // Change to server running state
            Logic.State = new ServerRunningState(Logic, MessageBroker);
        }

        public override void Start()
        {
            // TODO use UI screen
            MessageBroker.Publish(this, new LoadDebugGame());
        }

        public override void Stop()
        {
        }
    }
}
