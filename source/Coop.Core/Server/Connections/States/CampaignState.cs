namespace Coop.Core.Server.Connections.States
{
    public class CampaignState : ConnectionStateBase
    {
        public CampaignState(IConnectionLogic connectionLogic) : base(connectionLogic)
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
            ConnectionLogic.State = new MissionState(ConnectionLogic);
        }
    }
}
