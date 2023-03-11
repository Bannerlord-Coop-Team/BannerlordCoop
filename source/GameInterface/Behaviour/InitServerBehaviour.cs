using TaleWorlds.CampaignSystem;

namespace GameInterface.Behaviour
{
    public class InitServerBehaviour : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnGameLoaded(CampaignGameStarter gameStarter)
        {
            // CoopServer.Instance.StartServer();
        }
    }
}
