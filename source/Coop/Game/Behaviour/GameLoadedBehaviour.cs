using TaleWorlds.CampaignSystem;

namespace Coop.Game.Behaviour
{
    public class GameLoadedBehaviour : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, GameLoaded);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private static void GameLoaded(CampaignGameStarter gameStarter)
        {
            CoopClient.Instance.Events.OnGameLoaded.Invoke();

            CoopClient.Instance.GameState.AddPlayerControllerParty(MobileParty.MainParty);
            CoopClient.Instance.Events.OnBeforePlayerPartySpawned.Invoke(MobileParty.MainParty);
        }
    }
}
