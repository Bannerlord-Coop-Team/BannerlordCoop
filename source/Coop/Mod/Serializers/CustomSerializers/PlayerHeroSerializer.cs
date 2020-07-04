using HarmonyLib;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Coop.Mod.Serializers
{
    [Serializable]
    public class PlayerHeroSerializer : CustomSerializer
    {
        public PlayerHeroSerializer() { }

        [NonSerialized]
        public Hero hero;

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();
        public CharacterObjectSerializer CharacterObject { get; private set; }
        readonly List<HeroSerializer> ExSpouses = new List<HeroSerializer>();

        public PlayerHeroSerializer(Hero hero) : base(hero)
        {
            foreach(FieldInfo fieldInfo in NonSerializableObjects)
            {
                object value = fieldInfo.GetValue(hero);

                if (value == null){
                    continue;
                }

                switch (fieldInfo.Name)
                {
                    case "_characterObject":
                        //SNNSO.Add(fieldInfo, new CharacterObjectSerializer((CharacterObject)value, this));
                        break;
                    case "<BattleEquipment>k__BackingField":
                        SNNSO.Add(fieldInfo, new EquipmentSerializer((Equipment)value));
                        break;
                    case "<CivilianEquipment>k__BackingField":
                        SNNSO.Add(fieldInfo, new EquipmentSerializer((Equipment)value));
                        break;
                    case "<CaptivityStartTime>k__BackingField":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "_heroTraits":
                        SNNSO.Add(fieldInfo, new CharacterTraitsSerializer((CharacterTraits)value));
                        break;
                    case "_heroPerks":
                        SNNSO.Add(fieldInfo, new CharacterPerksSerializer((CharacterPerks)value));
                        break;
                    case "_heroSkills":
                        SNNSO.Add(fieldInfo, new CharacterSkillsSerializer((CharacterSkills)value));
                        break;
                    case "_cachedLastSeenInformation":
                        SNNSO.Add(fieldInfo, new HeroLastSeenInformationSerializer((Hero.HeroLastSeenInformation)value));
                        break;
                    case "_lastSeenInformationKnownToPlayer":
                        SNNSO.Add(fieldInfo, new HeroLastSeenInformationSerializer((Hero.HeroLastSeenInformation)value));
                        break;
                    case "_birthDay":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "_deathDay":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "<LastCommentTime>k__BackingField":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "_clan":
                        //SNNSO.Add(fieldInfo, new ClanSerializer((Clan)value, this));
                        break;
                    case "Culture":
                        // TODO Repoint (can use server obj)
                        // NOTE: May want to read from server before character creation
                        SNNSO.Add(fieldInfo, new CultureObjectSerializer((CultureObject)value));
                        break;
                    case "_partyBelongedTo":
                        //k  SNNSO.Add(fieldInfo, new MobilePartySerializer((MobileParty)value, this));
                        break;
                    case "<LastMeetingTimeWithPlayer>k__BackingField":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "_bornSettlement":
                        SNNSO.Add(fieldInfo, new SettlementSerializer((Settlement)value));
                        break;
                    case "<HomeSettlement>k__BackingField":
                        SNNSO.Add(fieldInfo, new SettlementSerializer((Settlement)value));
                        break;
                    case "_father":
                        SNNSO.Add(fieldInfo, new HeroSerializer((Hero)value));
                        break;
                    case "_mother":
                        SNNSO.Add(fieldInfo, new HeroSerializer((Hero)value));
                        break;
                    case "ExSpouses":
                        foreach (Hero exSpouse in (MBReadOnlyList<Hero>)value)
                        {
                            ExSpouses.Add(new HeroSerializer((Hero)value));
                        }
                        break;
                    case "_heroDeveloper":
                        // Can reinstantiate on recipient as this is hero data loaded at start of game.
                        SNNSO.Add(fieldInfo, new HeroDeveloperSerializer((HeroDeveloper)value));
                        break;
                    default:
                        throw new NotImplementedException("Cannot serialize " + fieldInfo.Name);

                }
            }
        }
        public override object Deserialize()
        {
            hero = new Hero();
            foreach (FieldInfo field in SNNSO.Keys)
            {
                field.SetValue(hero, SNNSO[field].Deserialize());
            }

            List<Hero> lExSpouses = new List<Hero>();
            foreach (HeroSerializer exSpouse in ExSpouses)
            {
                lExSpouses.Add((Hero)exSpouse.Deserialize());
            }
            hero.GetType()
                .GetField("_exSpouses", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(hero, lExSpouses);
            return base.Deserialize(hero);
        }
    }
}
