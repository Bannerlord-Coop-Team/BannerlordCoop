using Coop.Mod.Serializers;
using NLog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace Coop.Mod.Data
{
    [Serializable]
    public class GameData
    {
        [NonSerialized]
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public bool RequiresCharacterCreation => !Coop.IsServer;

        //CharacterObjectSerializer[] characterObjects;
        //ClanSerializer[] clans;

        // TODO 
        //KingdomSerializer[] kingdoms;
        //SettlementSerializer[] settlements;
        HeroSerializer[] heros;

        public GameData()
        {
            heros = CoopObjectManager.GetObjects<Hero>().Select(hero => new HeroSerializer(hero)).ToArray();
        }

        public void Unpack()
        {
            // Deserialize everything
            foreach (var hero in heros)
            {
                hero.Deserialize();
            }

            // Resolve all references
            //foreach (var hero in heros)
            //{
            //    hero.ResolveReferenceGuids();
            //}
        }
    }
}
