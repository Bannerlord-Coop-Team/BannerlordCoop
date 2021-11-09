using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Data
{
    [Serializable]
    class PartyData : IEquatable<PartyData>
    {
        string name;
        public PartyData(MobileParty party)
        {
            name = party.Name.ToString();
        }

        public bool Equals(PartyData other)
        {
            return name == other.name;
        }
    }

    class PartyDataComparer : IEqualityComparer<PartyData>
    {
        public bool Equals(PartyData x, PartyData y)
        {
            return x.Equals(y);
        }

        public int GetHashCode(PartyData obj)
        {
            return obj.GetHashCode();
        }
    }
}
