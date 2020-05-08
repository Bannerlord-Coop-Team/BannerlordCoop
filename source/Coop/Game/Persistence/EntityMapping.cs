using System.Collections.Generic;
using RailgunNet.System.Types;
using TaleWorlds.CampaignSystem;

namespace Coop.Game.Persistence.Party
{
    public class EntityMapping
    {
        public Dictionary<EntityId, MobileParty> Parties { get; } =
            new Dictionary<EntityId, MobileParty>();
    }
}
