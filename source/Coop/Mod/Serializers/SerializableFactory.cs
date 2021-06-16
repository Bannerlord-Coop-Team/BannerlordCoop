﻿using Sync.Store;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Coop.Mod.Serializers
{
    /// <summary>
    ///     Factory to create the serialization wrappers for non-serializable game objects.
    /// </summary>
    public class SerializableFactory : ISerializableFactory
    {
        public object Wrap(object obj)
        {
            switch (obj)
            {
                case Banner banner:
                    return new BannerSerializer(banner);
                case CampaignTime campaignTime:
                    return new CampaignTimeSerializer(campaignTime);
                case CharacterObject characterObject:
                    return new CharacterObjectSerializer(characterObject);
                case Clan clan:
                    return new ClanSerializer(clan);
                case CultureObject cultureObject:
                    return new CultureObjectSerializer(cultureObject);
                case DeterministicRandom deterministicRandom:
                    return new DeterministicRandomSerializer(deterministicRandom);
                case EquipmentElement equipmentElement:
                    return new EquipmentElementSerializer(equipmentElement);
                case Equipment equipment:
                    return new EquipmentSerializer(equipment);
                case HeroDeveloper heroDeveloper:
                    return new HeroDeveloperSerializer(heroDeveloper);
                case Hero.HeroLastSeenInformation heroLastSeenInfo:
                    return new HeroLastSeenInformationSerializer(heroLastSeenInfo);
                case Hero hero:
                    if(hero.StringId == "main_hero")
                    {
                        return new PlayerHeroSerializer(hero);
                    }
                    else
                    {
                        return new HeroSerializer(hero);
                    }
                    
                case ItemRoster itemRoster:
                    return new ItemRosterSerializer(itemRoster);
                case MBGUID mbguid:
                    return new MBGUIDSerializer(mbguid);
                case MobilePartiesAroundPositionList partiesAroundPosition:
                    return new MobilePartiesAroundPositionListSerializer(partiesAroundPosition);
                case MobileParty mobileParty:
                    return new MobilePartySerializer(mobileParty);
                case NavigationPath navPath:
                    return new NavigationPathSerializer(navPath);
                case PartyBase partyBase:
                    return new PartyBaseSerializer(partyBase);
                case PathFaceRecord pathFaceRecord:
                    return new PathFaceRecordSerializer(pathFaceRecord);
                case Settlement settlement:
                    return new SettlementSerializer(settlement);
                case TraitObject traitObject:
                    return new TraitObjectSerializer(traitObject);
                case TroopRoster troopRoster:
                    return new TroopRosterSerializer(troopRoster);
                case CharacterFeats characterFeats:
                    return new CharacterFeatsSerializer(characterFeats);
                case CharacterPerks characterPerks:
                    return new CharacterPerksSerializer(characterPerks);
                case CharacterSkills characterSkills:
                    return new CharacterSkillsSerializer(characterSkills);
                case CharacterTraits characterTraits:
                    return new CharacterTraitsSerializer(characterTraits);
                case PartyComponent partyComponent:
                    return new PartyComponentSerializer(partyComponent);
                default:
                    return obj;
            }
        }

        public object Unwrap(object obj)
        {
            switch (obj)
            {
                case TroopRosterSerializer ser:
                    return ser.Deserialize();
                default:
                    return obj;
            }
        }
    }
}
