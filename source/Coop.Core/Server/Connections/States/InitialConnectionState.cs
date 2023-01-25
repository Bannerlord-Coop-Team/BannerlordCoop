using Common.Messaging;

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
            ConnectionLogic.State = new ResolveCharacterState(ConnectionLogic);
        }

        public override void CreateCharacter()
        {
        }

        public override void TransferCharacter()
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