using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Mod
{
    public class CoopEvents
    {
        public readonly MbEvent<MobileParty>
            OnBeforePlayerPartySpawned = new MbEvent<MobileParty>();

        public readonly MbEvent OnGameLoaded = new MbEvent();

        public void RemoveListeners(object obj)
        {
            OnBeforePlayerPartySpawned.ClearListeners(obj);
            OnGameLoaded.ClearListeners(obj);
        }
    }
}
