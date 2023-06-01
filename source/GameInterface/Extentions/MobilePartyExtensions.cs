using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace Coop.Mod.Extentions
{
    public static class MobilePartyExtensions
    {
        private static readonly FieldInfo MobileParty_partyComponent = typeof(MobileParty).GetField("_partyComponent", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static void SetPartyComponent(this MobileParty party, PartyComponent component)
        {
            MobileParty_partyComponent.SetValue(party, component);
        }

        public static bool IsAnyPlayerMainParty(this MobileParty party)
        {
            // TODO remove this method and all references to it - use the MessageBroker system instead.
            // TODO create player controlled party registry instead of hardcoding
            List<string> debug_players = new List<string>();
            debug_players.Add("player_party");
            debug_players.Add("TransferredParty");
            return debug_players.Contains(party.StringId);
        }
    }
}
