namespace Coop.Core.Server.Connections.States
{
    public class ResolveCharacterState : ConnectionStateBase
    {
        public ResolveCharacterState(IConnectionLogic connectionLogic) 
            : base(connectionLogic)
        {
        }

        public override void Dispose()
        {

        }

        public override void ResolveCharacter()
        {
        }

        public override void CreateCharacter()
        {
            ConnectionLogic.State = new CreateCharacterState(ConnectionLogic);
        }

        public override void TransferCharacter()
        {
        }

        public override void Load()
        {
            ConnectionLogic.State = new LoadingState(ConnectionLogic);
        }

        public override void EnterCampaign()
        {
        }

        public override void EnterMission()
        {
        }
    }
}
