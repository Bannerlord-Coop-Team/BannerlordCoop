using TaleWorlds.CampaignSystem;

namespace Coop.Game
{
    public class CoopEvents
    {
        public readonly MbEvent
            OnGameLoaded = new MbEvent();
        public readonly MbEvent<MobileParty>
            OnBeforePlayerPartySpawned = new MbEvent<MobileParty>();

        public void RemoveListeners(object obj)
        {
            OnBeforePlayerPartySpawned.ClearListeners(obj);
            OnGameLoaded.ClearListeners(obj);
        }
    }
}
