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
    class HeroSerializer : CustomSerializer
    {
        public HeroSerializer() { }

        [NonSerialized]
        public Hero hero;

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();
        public CharacterObjectSerializer CharacterObject { get; private set; }
        public string FathersName { get; private set; }
        public string MothersName { get; private set; }
        public string SpouseName { get; private set; }
        readonly List<string> ExSpouses = new List<string>();

        public HeroSerializer(Hero hero) : base(hero)
        {
            NonSerializableObjects.Count();
            foreach(FieldInfo fieldInfo in NonSerializableObjects)
            {
                switch (fieldInfo.Name)
                {
                    case "_characterObject":
                        SNNSO.Add(fieldInfo, new CharacterObjectSerializer((CharacterObject)fieldInfo.GetValue(hero), this));
                        break;
                    case "<BattleEquipment>k__BackingField":
                        SNNSO.Add(fieldInfo, new EquipmentSerializer((Equipment)fieldInfo.GetValue(hero)));
                        break;
                    case "<CivilianEquipment>k__BackingField":
                        SNNSO.Add(fieldInfo, new EquipmentSerializer((Equipment)fieldInfo.GetValue(hero)));
                        break;
                    case "<CaptivityStartTime>k__BackingField":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)fieldInfo.GetValue(hero)));
                        break;
                    case "_heroTraits":
                        SNNSO.Add(fieldInfo, new CharacterTraitsSerializer((CharacterTraits)fieldInfo.GetValue(hero)));
                        break;
                    case "_heroPerks":
                        SNNSO.Add(fieldInfo, new CharacterPerksSerializer((CharacterPerks)fieldInfo.GetValue(hero)));
                        break;
                    case "_heroSkills":
                        SNNSO.Add(fieldInfo, new CharacterSkillsSerializer((CharacterSkills)fieldInfo.GetValue(hero)));
                        break;
                    case "_cachedLastSeenInformation":
                        SNNSO.Add(fieldInfo, new HeroLastSeenInformationSerializer((Hero.HeroLastSeenInformation)fieldInfo.GetValue(hero)));
                        break;
                    case "_lastSeenInformationKnownToPlayer":
                        SNNSO.Add(fieldInfo, new HeroLastSeenInformationSerializer((Hero.HeroLastSeenInformation)fieldInfo.GetValue(hero)));
                        break;
                    case "_birthDay":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)fieldInfo.GetValue(hero)));
                        break;
                    case "_deathDay":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)fieldInfo.GetValue(hero)));
                        break;
                    case "<LastCommentTime>k__BackingField":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)fieldInfo.GetValue(hero)));
                        break;
                    case "_clan":
                        SNNSO.Add(fieldInfo, new ClanSerializer((Clan)fieldInfo.GetValue(hero)));
                        break;
                    case "Culture":
                        // TODO Repoint (can use server obj)
                        // NOTE: May want to read from server before character creation
                        SNNSO.Add(fieldInfo, new CultureObjectSerializer((CultureObject)fieldInfo.GetValue(hero)));
                        break;
                    case "_partyBelongedTo":
                        SNNSO.Add(fieldInfo, new MobilePartySerializer((MobileParty)fieldInfo.GetValue(hero)));
                        break;
                    case "<LastMeetingTimeWithPlayer>k__BackingField":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)fieldInfo.GetValue(hero)));
                        break;
                    case "_bornSettlement":
                        SNNSO.Add(fieldInfo, new SettlementSerializer((Settlement)fieldInfo.GetValue(hero)));
                        break;
                    case "<HomeSettlement>k__BackingField":
                        SNNSO.Add(fieldInfo, new SettlementSerializer((Settlement)fieldInfo.GetValue(hero)));
                        break;
                    case "_father":
                        FathersName = ((Hero)fieldInfo.GetValue(hero)).Name.ToString();
                        break;
                    case "_mother":
                        MothersName = ((Hero)fieldInfo.GetValue(hero)).Name.ToString();
                        break;
                    case "ExSpouses":
                        foreach (Hero exSpouse in (MBReadOnlyList<Hero>)fieldInfo.GetValue(hero))
                        {
                            ExSpouses.Add(exSpouse.Name.ToString());
                        }
                        break;
                    case "_heroDeveloper":
                        // Can reinstantiate on recipient as this is hero data loaded at start of game.
                        break;
                    default:
                        throw new NotImplementedException("Cannot serialize " + fieldInfo.Name);

                }
            }
        }
        public override object Deserialize()
        {
            hero = new Hero();
            return hero;
        }
    }
}
