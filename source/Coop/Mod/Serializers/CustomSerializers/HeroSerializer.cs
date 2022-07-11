using Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers.Custom
{
    [Serializable]
    public class HeroSerializer : CustomSerializerWithGuid
    {
        [NonSerialized]
        Hero newHero;

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        readonly Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();
        readonly Dictionary<FieldInfo, Guid> References = new Dictionary<FieldInfo, Guid>();

        readonly List<Guid> ExSpouses = new List<Guid>();

        public HeroSerializer(Hero hero) : base(hero)
        {
            List<FieldInfo> UnmanagedFields = new List<FieldInfo>();

            foreach (FieldInfo fieldInfo in NonSerializableObjects)
            {
                // Get value from fieldInfo
                object value = fieldInfo.GetValue(hero);

                // If value is null, no need to serialize
                if (value == null)
                {
                    continue;
                }

                // Assign serializer to nonserializable objects
                switch (fieldInfo.Name)
                {
                    case "<Id>k__BackingField":
                        // Ignore current MB id
                        break;
                    case "_firstName":
                    case "_name":
                    case "<EncyclopediaText>k__BackingField":
                        SNNSO.Add(fieldInfo, new TextObjectSerializer((TextObject)value));
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
                    case "Culture":
                        // NOTE: May want to read from server before character creation
                        SNNSO.Add(fieldInfo, new CultureObjectSerializer((CultureObject)value));
                        break;
                    case "<LastMeetingTimeWithPlayer>k__BackingField":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "<HomeSettlement>k__BackingField":
                        // Do nothing, public getter
                        break;
                    case "ExSpouses":
                        // Do nothing, public getter
                        break;
                    case "_exSpouses":
                        foreach (Guid exSpouse in CoopObjectManager.GetGuids((MBReadOnlyList<Hero>)value))
                        {
                            ExSpouses.Add(exSpouse);
                        }
                        break;
                    case "_heroDeveloper":
                        // Can reinstantiate on recipient as this is hero data loaded at start of game.
                        SNNSO.Add(fieldInfo, new HeroDeveloperSerializer((HeroDeveloper)value));
                        break;
                    case "_characterAttributes":
                        // TODO: Fix this joke
                        break;
                    case "<Issue>k__BackingField":
                        // TODO: Fix this joke
                        break;
                    case "<BannerItem>k__BackingField":
                        // TODO: Fix this joke
                        break;
                    case "_characterObject":
                    case "<Template>k__BackingField":
                    case "_clan":
                    case "_bornSettlement":
                    case "_homeSettlement":
                    case "_stayingInSettlement":
                    case "_father":
                    case "_mother":
                    case "_spouse":
                    case "_supporterOf":
                    case "_governorOf":
                    case "_partyBelongedTo":
                        References.Add(fieldInfo, CoopObjectManager.GetGuid(value));
                        break;
                    default:
                        UnmanagedFields.Add(fieldInfo);
                        break;
                }
            }

            if (!UnmanagedFields.IsEmpty())
            {
                throw new NotImplementedException($"Cannot serialize {UnmanagedFields}");
            }
        }

        public override object Deserialize()
        {
            newHero = MBObjectManager.Instance.CreateObject<Hero>();

            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
            {
                entry.Key.SetValue(newHero, entry.Value.Deserialize());
            }

            base.Deserialize(newHero);

            return newHero;
        }

        public override void ResolveReferenceGuids()
        {
            if(newHero == null)
            {
                throw new NullReferenceException("Deserialize() has not been called before ResolveReferenceGuids().");
            }

            // Deserialize exSpouse list
            List<Hero> lExSpouses = new List<Hero>();
            foreach (Guid exSpouseId in ExSpouses)
            {
                lExSpouses.Add((Hero)CoopObjectManager.GetObject(exSpouseId));
            }

            foreach(KeyValuePair<FieldInfo, Guid> entry in References)
            {
                FieldInfo field = entry.Key;
                Guid id = entry.Value;

                field.SetValue(newHero, CoopObjectManager.GetObject(id));
            }

            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
            {
                entry.Value.ResolveReferenceGuids();
            }
        }
    }
}