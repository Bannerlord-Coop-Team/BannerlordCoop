using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace MBMultiplayerCampaign.Serializers
{
    [Serializable]
    class HeroSerializer : CustomSerializer
    {
        public HeroSerializer() { }
        public string CharacterObjectName { get; private set; }
        public string FathersName { get; private set; }
        public string MothersName { get; private set; }
        public string SpouseName { get; private set; }
        readonly List<string> exSpouses = new List<string>();
        //EquipmentSerializer battleEquipment;
        //EquipmentSerializer civilianEquipment;
        //CampaignTimeSerializer campaignTime;
        CharacterTraitsSerializer characterTraits;
        //CharacterPerksSerializer characterPerks;
        //CharacterSkillsSerializer characterSkills;
        //IssueSerializer issues;
        //ClanSerializer clan;
        //TownSerializer governerTown;
        //CulturSerializer cultur;
        //PartySerializer party;
        //SettlementSerializer stayingInSettlement;
        //HeroDeveloperSerializer heroDeveloper;

        public HeroSerializer(Hero hero) : base(hero)
        {
            NonSerializableObjects.Count();
            foreach(Tuple<FieldInfo, object> fieldObj in NonSerializableObjects)
            {
                if(fieldObj.Item1.Name == "_characterObject")
                {
                    CharacterObjectName = (fieldObj.Item2 as CharacterObject).Name.ToString();
                }

                if (fieldObj.Item1.FieldType == typeof(Hero))
                {
                    CharacterObjectName = (fieldObj.Item2 as CharacterObject).Name.ToString();
                }

                if (fieldObj.Item1.FieldType == typeof(CharacterTraits))
                {
                    characterTraits = new CharacterTraitsSerializer((fieldObj.Item2 as CharacterTraits));
                }
            }
        }
        public override ICustomSerializer Serialize(object obj)
        {
            return new HeroSerializer((Hero)obj);
        }
        public override object Deserialize()
        {
            Hero newHero = new Hero();
            return newHero;
        }
    }
}
