using Common.Messaging;
using Coop.Core.Client.States;

namespace Coop.Core.Server.Connections.States
{
    public class InitialConnectionState : ConnectionStateBase
    {
        public InitialConnectionState(IConnectionLogic connectionLogic)
            : base(connectionLogic)
        {
        }

        public override void Dispose()
        {

        }

        public override void ResolveCharacter()
        {
            // TODO Implement resolve character state
            //ConnectionLogic.State = new ResolveCharacterState(ConnectionLogic);
            ConnectionLogic.State = new CreateCharacterState(ConnectionLogic);
        }

        public override void CreateCharacter()
        {
        }

        public override void TransferSave()
        {
        }

        public override void Load()
        {
        }

        public override void EnterCampaign()
        {
        }

        public override void EnterMission()
        {
        }
    }
}