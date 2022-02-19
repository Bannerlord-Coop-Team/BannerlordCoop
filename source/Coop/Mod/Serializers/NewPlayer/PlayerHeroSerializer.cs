using Coop.NetImpl;
using SandBox.View.Map;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TaleWorlds.PlayerServices;
using Debug = System.Diagnostics.Debug;

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
        public string PlayerId { get; }

        public PlayerHeroSerializer(Hero hero) : base(hero)
        {
            PlayerId = new PlatformAPI().GetPlayerID().ToString();

            List<string> UnmanagedFields = new List<string>();

            foreach (FieldInfo fieldInfo in NonSerializableObjects)
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
                    case "<Id>k__BackingField":
                        // Ignore current MB id
                        break;
                    case "_firstName":
#if DEBUG
                        SNNSO.Add(fieldInfo, new TextObjectSerializer(new TextObject("Client_Player")));
                        break;
#endif
                    case "_name":
                    case "<EncyclopediaText>k__BackingField":
                        SNNSO.Add(fieldInfo, new TextObjectSerializer((TextObject)value));
                        break;
                    case "_characterObject":
                        SNNSO.Add(fieldInfo, new PlayerCharacterObjectSerializer((CharacterObject)value));
                        break;
                    case "<BattleEquipment>k__BackingField":
                        SNNSO.Add(fieldInfo, new Custom.EquipmentSerializer((Equipment)value));
                        break;
                    case "<CivilianEquipment>k__BackingField":
                        SNNSO.Add(fieldInfo, new Custom.EquipmentSerializer((Equipment)value));
                        break;
                    case "<CaptivityStartTime>k__BackingField":
                        SNNSO.Add(fieldInfo, new Custom.CampaignTimeSerializer((CampaignTime)value));
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
                        SNNSO.Add(fieldInfo, new PlayerHeroLastSeenInformationSerializer((Hero.HeroLastSeenInformation)value));
                        break;
                    case "_lastSeenInformationKnownToPlayer":
                        SNNSO.Add(fieldInfo, new PlayerHeroLastSeenInformationSerializer((Hero.HeroLastSeenInformation)value));
                        break;
                    case "_birthDay":
                        SNNSO.Add(fieldInfo, new Custom.CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "_deathDay":
                        SNNSO.Add(fieldInfo, new Custom.CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "<LastCommentTime>k__BackingField":
                        SNNSO.Add(fieldInfo, new Custom.CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "_clan":
                        SNNSO.Add(fieldInfo, new PlayerClanSerializer((Clan)value));
                        break;
                    case "Culture":
                        // NOTE: May want to read from server before character creation
                        SNNSO.Add(fieldInfo, new PlayerCultureObjectSerializer((CultureObject)value));
                        break;
                    case "_partyBelongedTo":
                        SNNSO.Add(fieldInfo, new PlayerMobilePartySerializer((MobileParty)value));
                        break;
                    case "<LastMeetingTimeWithPlayer>k__BackingField":
                        SNNSO.Add(fieldInfo, new Custom.CampaignTimeSerializer((CampaignTime)value));
                        break;
                    case "_bornSettlement":
                        SNNSO.Add(fieldInfo, new PlayerSettlementSerializer((Settlement)value));
                        break;
                    case "<HomeSettlement>k__BackingField":
                        SNNSO.Add(fieldInfo, new PlayerSettlementSerializer((Settlement)value));
                        break;
                    case "_homeSettlement":
                        SNNSO.Add(fieldInfo, new PlayerSettlementSerializer((Settlement)value));
                        break;
                    case "_father":
                        SNNSO.Add(fieldInfo, null);
                        break;
                    case "_mother":
                        SNNSO.Add(fieldInfo, null);
                        break;
                    case "ExSpouses":
                        // This starts empty
                        break;
                    case "_heroDeveloper":
                        // Can reinstantiate on recipient as this is hero data loaded at start of game.
                        SNNSO.Add(fieldInfo, new PlayerHeroDeveloperSerializer((HeroDeveloper)value));
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

            // TODO manage collections

            Debug.WriteLine($"{hero.Id}");
        }
        public override object Deserialize()
        {
            hero = MBObjectManager.Instance.CreateObject<Hero>();

            foreach (KeyValuePair<FieldInfo, ICustomSerializer> entry in SNNSO)
            {
                // Pass references to specified serializers
                switch (entry.Value)
                {
                    case PlayerCharacterObjectSerializer characterObjectSerializer:
                        characterObjectSerializer.SetHeroReference(hero);
                        break;
                    case PlayerClanSerializer clanSerializer:
                        clanSerializer.SetHeroReference(hero);
                        break;
                    case PlayerMobilePartySerializer mobilePartySerializer:
                        mobilePartySerializer.SetHeroReference(hero);
                        mobilePartySerializer.SetClanReference(hero.Clan);
                        break;
                    case PlayerHeroDeveloperSerializer heroDeveloperSerializer:
                        heroDeveloperSerializer.SetHeroReference(hero);
                        break;
                }

                entry.Key.SetValue(hero, entry.Value?.Deserialize());
            }

            hero.GetType()
                .GetField("_exSpouses", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(hero, new List<Hero>());


            ConstructorInfo ctor = typeof(HeroDeveloper).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance,
                                    null, new Type[] { typeof(Hero) }, null);

            HeroDeveloper newDeveloper = (HeroDeveloper)ctor.Invoke(new object[] { hero });
            hero.GetType()
                .GetField("_heroDeveloper", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(hero, newDeveloper);

            base.Deserialize(hero);

            // Update health due to member starting as injured
            hero.PartyBelongedTo.Party.MemberRoster.OnHeroHealthStatusChanged(hero);


            ConstructorInfo ctorInfo = typeof(LordPartyComponent)
                .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null,
                    new Type[] { typeof(Hero) }, new ParameterModifier[0]);
            LordPartyComponent lordPartyComponent = (LordPartyComponent)ctorInfo.Invoke(new object[] { Hero.MainHero });

            Campaign.Current.MainParty.ActualClan = Clan.PlayerClan;
            Campaign.Current.MainParty.PartyComponent = lordPartyComponent;
            
            // Invoke party visual onstartup to initialize properly
            typeof(PartyVisual).GetMethod("TaleWorlds.CampaignSystem.IPartyVisual.OnStartup", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(hero.PartyBelongedTo.Party.Visuals, new object[] { hero.PartyBelongedTo.Party });

            Debug.WriteLine($"{hero.Id}");

            return hero;
        }

        public override void ResolveReferenceGuids()
        {
            throw new NotImplementedException();
        }
    }
}
