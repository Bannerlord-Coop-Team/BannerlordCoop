using Coop.NetImpl;
using SandBox.View.Map;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using TaleWorlds.PlayerServices;

namespace Coop.Mod.Serializers
{
    [Serializable]
    internal class HeroSerializer : CustomSerializer
    {
        private string name;

        [NonSerialized]
        Hero newHero;

        /// <summary>
        /// Serialized Natively Non Serializable Objects (SNNSO)
        /// </summary>
        Dictionary<FieldInfo, ICustomSerializer> SNNSO = new Dictionary<FieldInfo, ICustomSerializer>();

        readonly List<Guid> ExSpouses = new List<Guid>();

        Guid clan;
        Guid mother;
        Guid father;
        Guid bornSettlement;
        Guid homeSettlement;
        Guid culture;

        public HeroSerializer(Hero hero)
        {
            List<string> UnmanagedFields = new List<string>();

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
                    case "_characterObject":
                        SNNSO.Add(fieldInfo, new CharacterObjectSerializer((CharacterObject)value));
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
                        clan = CoopObjectManager.GetGuid((Clan)value);
                        break;
                    case "Culture":
                        // NOTE: May want to read from server before character creation
                        culture = CoopObjectManager.GetGuid((CultureObject)value);
                        break;
                    case "_partyBelongedTo":
                        SNNSO.Add(fieldInfo, new MobilePartySerializer((MobileParty)value));
                        break;
                    case "<LastMeetingTimeWithPlayer>k__BackingField":
                        SNNSO.Add(fieldInfo, new CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "_bornSettlement":
                        bornSettlement = CoopObjectManager.GetGuid((Settlement)value);
                        break;
                    case "<HomeSettlement>k__BackingField":
                        // Do nothing, public getter
                        break;
                    case "_homeSettlement":
                        homeSettlement = CoopObjectManager.GetGuid((Settlement)value);
                        break;
                    case "_father":
                        SNNSO.Add(fieldInfo, null);
                        break;
                    case "_mother":
                        SNNSO.Add(fieldInfo, null);
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
                    default:
                        UnmanagedFields.Add(fieldInfo.Name);
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
                // Pass references to specified serializers
                switch (entry.Value)
                {
                    case CharacterObjectSerializer characterObjectSerializer:
                        characterObjectSerializer.SetHeroReference(newHero);
                        break;
                }

                entry.Key.SetValue(newHero, entry.Value.Deserialize());
            }

            ConstructorInfo ctor = typeof(HeroDeveloper).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance,
                                    null, new Type[] { typeof(Hero) }, null);

            HeroDeveloper newDeveloper = (HeroDeveloper)ctor.Invoke(new object[] { newHero });
            newHero.GetType()
                .GetField("_heroDeveloper", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(newHero, newDeveloper);

            base.Deserialize(newHero);

            // Update health due to member starting as injured
            newHero.PartyBelongedTo.Party.MemberRoster.OnHeroHealthStatusChanged(newHero);

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

            newHero.Culture = (CultureObject)CoopObjectManager.GetObject(culture);

            newHero.GetType()
                .GetField("_exSpouses", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(newHero, lExSpouses);

            newHero.GetType()
                .GetField("_father", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(newHero, CoopObjectManager.GetObject(this.father));

            newHero.GetType()
                .GetField("_mother", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(newHero, CoopObjectManager.GetObject(mother));

            newHero.GetType()
                .GetField("_bornSettlement", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(newHero, CoopObjectManager.GetObject(bornSettlement));

            newHero.GetType()
                .GetField("_homeSettlement", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(newHero, CoopObjectManager.GetObject(homeSettlement));
        }
    }
}