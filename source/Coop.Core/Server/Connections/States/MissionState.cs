namespace Coop.Core.Server.Connections.States
{
    public class MissionState : ConnectionStateBase
    {
        public MissionState(IConnectionLogic connectionLogic) : base(connectionLogic)
        {
        }

        public override void ResolveCharacter()
        {
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
            ConnectionLogic.State = new CampaignState(ConnectionLogic);
        }

        public override void EnterMission()
        {
        }
    }
}
