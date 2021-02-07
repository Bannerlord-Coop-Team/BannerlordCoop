using SandBox.View.Map;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers
{
    [Serializable]
    public class PlayerHeroSerializer : CustomSerializer
    {
        /// <summary>
        /// Used for circular reference
        /// </summary>
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
                // Get value from fieldInfo
                object value = fieldInfo.GetValue(hero);

                // If value is null, no need to serialize
                if (value == null){
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
                        SNNSO.Add(fieldInfo, new ClanSerializer((Clan)value));
                        break;
                    case "Culture":
                        // NOTE: May want to read from server before character creation
                        SNNSO.Add(fieldInfo, new CultureObjectSerializer((CultureObject)value));
                        break;
                    case "_partyBelongedTo":
                        SNNSO.Add(fieldInfo, new MobilePartySerializer((MobileParty)value));
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
                    case "_homeSettlement":
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

            // TODO manage collections

            // Remove non serializable objects before serialization
            // They are marked as nonserializable in CustomSerializer but still tries to serialize???
            NonSerializableCollections.Clear();
            NonSerializableObjects.Clear();
        }
        public override object Deserialize()
        {
            hero = MBObjectManager.Instance.CreateObject<Hero>();
            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
            {
                // Pass references to specified serializers
                switch (entry.Value)
                {
                    case CharacterObjectSerializer characterObjectSerializer:
                        characterObjectSerializer.SetHeroReference(hero);
                        break;
                    case ClanSerializer clanSerializer:
                        clanSerializer.SetHeroReference(hero);
                        break;
                    case MobilePartySerializer mobilePartySerializer:
                        mobilePartySerializer.SetHeroReference(hero);
                        break;
                }

                entry.Key.SetValue(hero, entry.Value.Deserialize());
            }

            // Deserialize exSpouse list
            List<Hero> lExSpouses = new List<Hero>();
            foreach (HeroSerializer exSpouse in ExSpouses)
            {
                lExSpouses.Add((Hero)exSpouse.Deserialize());
            }

            hero.GetType()
                .GetField("_exSpouses", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(hero, lExSpouses);


            ConstructorInfo ctor = typeof(HeroDeveloper).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance,
                                    null, new Type[] { typeof(Hero) }, null);

            HeroDeveloper newDeveloper = (HeroDeveloper)ctor.Invoke(new object[] { hero });
            hero.GetType()
                .GetField("_heroDeveloper", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(hero, newDeveloper);

            base.Deserialize(hero);

            // Update health due to member starting as injured
            hero.PartyBelongedTo.Party.MemberRoster.OnHeroHealthStatusChanged(hero);


            ConstructorInfo ctorInfo = typeof(WarPartyComponent)
                .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null,
                    new Type[] { typeof(Clan), typeof(Hero) }, new ParameterModifier[0]);
            Campaign.Current.MainParty.PartyComponent = (WarPartyComponent)ctorInfo.Invoke(new object[] { Clan.PlayerClan, Hero.MainHero });

            // Invoke party visual onstartup to initialize properly
            typeof(PartyVisual).GetMethod("TaleWorlds.CampaignSystem.IPartyVisual.OnStartup", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(hero.PartyBelongedTo.Party.Visuals, new object[] { hero.PartyBelongedTo.Party });

            return hero;
        }
    }
}
