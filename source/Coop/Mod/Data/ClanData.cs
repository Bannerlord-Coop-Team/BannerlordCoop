using System;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Data
{
    [Serializable]
    class ClanData : IEquatable<ClanData>
    {
        string name;
        public ClanData(Clan clan)
        {
            name = clan.Name.ToString();
        }

        public bool Equals(ClanData other)
        {
            return name == other.name;
        }
    }
}