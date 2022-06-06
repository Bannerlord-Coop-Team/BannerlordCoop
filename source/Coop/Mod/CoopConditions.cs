using Sync.Behaviour;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.Mod
{
    public static class CoopConditions
    {
        public static Condition ControlsParty = new Condition(((_0, obj) => controlsParty(obj)));
        public static Condition IsServer = new Condition((_0, _1) => isServer());
        public static Condition IsRemoteClient = new Condition((_0, _1) => isRemoteClient());

        private static bool controlsParty(object obj)
        {
            return obj is MobileParty party && Coop.IsController(party);
        }
        private static bool isServer()
        {
            return Coop.IsServer;
        }

        private static bool isRemoteClient()
        {
            return !isServer();
        }
    }
}