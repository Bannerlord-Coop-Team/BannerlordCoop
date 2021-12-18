using Coop.Mod.Serializers.Custom;
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

        public Guid PlayerPartyId { get; set; }

        public bool RequiresCharacterCreation => !Coop.IsServer;

        //CharacterObjectSerializer[] characterObjects;
        //ClanSerializer[] clans;

        // TODO 
        //KingdomSerializer[] kingdoms;
        SettlementSerializer[] settlements;
        HeroSerializer[] heros;

        

        public GameData()
        {
            heros = CoopObjectManager.GetObjects<Hero>().Select(hero => new HeroSerializer(hero)).ToArray();
            settlements = CoopObjectManager.GetObjects<Settlement>().Select(settlement => new SettlementSerializer(settlement)).ToArray();
        }

        public void Unpack()
        {
            // Deserialize everything
            foreach (var hero in heros)
            {
                hero.Deserialize();
            }

            foreach (var settlement in settlements)
            {
                settlement.Deserialize();
            }

            // Resolve all references
            foreach (var hero in heros)
            {
                hero.ResolveReferenceGuids();
            }
        }
    }
}
