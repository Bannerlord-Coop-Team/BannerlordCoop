using Coop.NetImpl;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;
using TaleWorlds.PlayerServices;

namespace Coop.Mod.Serializers
{
    [Serializable]
    internal class HeroSerializer : ICustomSerializer
    {
        private string name;

        public HeroSerializer(Hero value)
        {
            name = value.Name.ToString();
        }

        public object Deserialize()
        {
            return Hero.FindFirst((hero) => { return hero.Name?.ToString() == name; });
        }
    }
}