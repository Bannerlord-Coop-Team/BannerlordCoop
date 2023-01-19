namespace Coop.Core.Server.Connections.States
{
    public class CreateCharacterState : ConnectionStateBase
    {
        public CreateCharacterState(IConnectionLogic connectionLogic) : base(connectionLogic)
        {
        }

        public override void CreateCharacter()
        {
        }

        public override void EnterCampaign()
        {
        }

        public override void EnterMission()
        {
        }

        public override void Load()
        {
        }

        public override void ResolveCharacter()
        {
        }

        public override void TransferCharacter()
        {
            ConnectionLogic.State = new TransferCharacterState(ConnectionLogic);
        }
    }
}
