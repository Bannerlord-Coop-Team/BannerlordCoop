using System;
using TaleWorlds.CampaignSystem;

namespace Coop.Game
{
    public class PlayerJoinedBehaviour : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnGameLoaded));
        }

        public override void SyncData(IDataStore dataStore)
        {
            throw new System.NotImplementedException();
        }

        private void OnGameLoaded(CampaignGameStarter gameStarter)
        {
            CoopClient.Client.GameState.AddPlayerControllerParty(MobileParty.MainParty);
        }
    }
}
