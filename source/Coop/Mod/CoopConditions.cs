using Sync.Behaviour;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod
{
    public static class CoopConditions
    {
        public static Condition ControlsParty = new Condition(((originator, o) => o is MobileParty party && Coop.IsController(party)));
    }
}