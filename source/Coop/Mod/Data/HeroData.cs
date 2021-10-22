using System;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Data
{
    [Serializable]
    class HeroData : IEquatable<HeroData>
    {
        string name;
        ClanData clan;
        public HeroData(Hero hero)
        {
            name = hero.Name.ToString();
            clan = new ClanData(hero.Clan);
        }

        public bool Equals(HeroData other)
        {
            return name == other.name &&
                clan.Equals(other.clan);
        }
    }
}