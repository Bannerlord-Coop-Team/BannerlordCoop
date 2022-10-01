using HarmonyLib;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using static TaleWorlds.CampaignSystem.Hero;
using GameInterface.Serialization.Collections;
using static TaleWorlds.CampaignSystem.Actions.KillCharacterAction;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Serialization.Surrogates
{
    [ProtoContract(SkipConstructor = true)]
    public class HeroSurrogate
    {
        #region Fields
        [ProtoMember(1)]
        int LastTimeStampForActivity { get; }

        [ProtoMember(2)]
        ArraySerializer<CharacterObject> VolunteerTypes { get; }

        [ProtoMember(3)]
        float _passedTimeAtHomeSettlement { get; }

        [ProtoMember(4)]
        CharacterObject _characterObject { get; }

        [ProtoMember(5)]
        TextObject _firstName { get; }

        [ProtoMember(6)]
        TextObject _name { get; }

        [ProtoMember(7)]
        string HairTags { get; }

        [ProtoMember(8)]
        string BeardTags { get; }

        [ProtoMember(9)]
        string TattooTags { get; }

        [ProtoMember(10)]
        CharacterStates _heroState { get; }

        [ProtoMember(11)]
        CharacterTraits _heroTraits { get; }

        [ProtoMember(12)]
        CharacterPerks _heroPerks { get; }

        [ProtoMember(13)]
        CharacterSkills _heroSkills { get; }

        [ProtoMember(14)]
        CharacterAttributes _characterAttributes { get; }

        [ProtoMember(15)]
        bool IsNobleForOldSaves { get; }

        [ProtoMember(16)]
        float LastVisitTimeOfHomeSettlement { get; }

        [ProtoMember(17)]
        int Level { get; }

        [ProtoMember(18)]
        Clan _companionOf { get; }

        [ProtoMember(19)]
        HeroLastSeenInformation _cachedLastSeenInformation { get; }

        [ProtoMember(20)]
        HeroLastSeenInformation _lastSeenInformationKnownToPlayer { get; }

        [ProtoMember(21)]
        int SpcDaysInLocation { get; }

        [ProtoMember(22)]
        int _health { get; }

        [ProtoMember(23)]
        float _defaultAge { get; }

        [ProtoMember(24)]
        CampaignTime _birthDay { get; }

        [ProtoMember(25)]
        CampaignTime _deathDay { get; }

        [ProtoMember(26)]
        float _power { get; }

        [ProtoMember(27)]
        Clan _clan { get; }

        [ProtoMember(28)]
        Clan _supporterOf { get; }

        [ProtoMember(29)]
        Town _governorOf { get; }

        [ProtoMember(30)]
        ListSerializer<Workshop> _ownedWorkshops { get; }

        [ProtoMember(31)]
        CultureObject Culture { get; }

        [ProtoMember(32)]
        MobileParty _partyBelongedTo { get; }

        [ProtoMember(33)]
        Settlement _stayingInSettlement { get; }

        [ProtoMember(34)]
        ListSerializer<ItemObject> SpecialItems { get; }

        [ProtoMember(35)]
        bool _hasMet { get; }

        [ProtoMember(36)]
        Settlement _bornSettlement { get; }

        [ProtoMember(37)]
        Settlement _homeSettlement { get; }

        [ProtoMember(38)]
        int _gold { get; }

        //[ProtoMember(39)]
        //Hero _father { get; }

        //[ProtoMember(40)]
        //Hero _mother { get; }

        //[ProtoMember(41)]
        //ListSerializer<Hero> _exSpouses { get; }

        //[ProtoMember(42)]
        //Hero _spouse { get; }

        //[ProtoMember(43)]
        //ListSerializer<Hero> _children { get; }

        [ProtoMember(44)]
        bool IsPregnant { get; }

        [ProtoMember(45)]
        IHeroDeveloper _heroDeveloper { get; }
        #endregion

        #region Properties
        [ProtoMember(47)]
        StaticBodyProperties StaticBodyProperties { get; }

        [ProtoMember(48)]
        float Weight { get; }

        [ProtoMember(49)]
        float Build { get; }

        [ProtoMember(50)]
        TextObject EncyclopediaText { get; }

        [ProtoMember(51)]
        bool IsFemale { get; }

        [ProtoMember(52)]
        Equipment BattleEquipment { get; }

        [ProtoMember(53)]
        Equipment CivilianEquipment { get; }

        [ProtoMember(54)]
        CampaignTime CaptivityStartTime { get; }

        [ProtoMember(55)]
        FormationClass PreferredUpgradeFormation { get; }

        [ProtoMember(56)]
        bool IsMinorFactionHero { get; }

        [ProtoMember(57)]
        IssueBase Issue { get; }

        [ProtoMember(58)]
        Occupation Occupation { get; }

        [ProtoMember(59)]
        KillCharacterActionDetail DeathMark { get; }

        //[ProtoMember(60)]
        //Hero DeathMarkKillerHero { get; }

        [ProtoMember(61)]
        long LastExaminedLogEntryID { get; }

        [ProtoMember(62)]
        ListSerializer<CommonAreaPartyComponent> OwnedCommonAreas { get; }

        [ProtoMember(63)]
        ListSerializer<CaravanPartyComponent> OwnedCaravans { get; }

        [ProtoMember(64)]
        PartyBase PartyBelongedToAsPrisoner { get; }

        [ProtoMember(65)]
        CampaignTime LastMeetingTimeWithPlayer { get; }

        [ProtoMember(66)]
        int RandomValue { get; }

        [ProtoMember(67)]
        EquipmentElement BannerItem { get; }
        #endregion

        #region Reflection
        private static readonly Dictionary<string, FieldInfo> Fields = new Dictionary<string, FieldInfo>
        {
            { nameof(LastTimeStampForActivity), AccessTools.Field(typeof(Hero), nameof(LastTimeStampForActivity)) },
            { nameof(VolunteerTypes), AccessTools.Field(typeof(Hero), nameof(VolunteerTypes)) },
            { nameof(_passedTimeAtHomeSettlement), AccessTools.Field(typeof(Hero), nameof(_passedTimeAtHomeSettlement)) },
            { nameof(_characterObject), AccessTools.Field(typeof(Hero), nameof(_characterObject)) },
            { nameof(_firstName), AccessTools.Field(typeof(Hero), nameof(_firstName)) },
            { nameof(_name), AccessTools.Field(typeof(Hero), nameof(_name)) },
            { nameof(HairTags), AccessTools.Field(typeof(Hero), nameof(HairTags)) },
            { nameof(BeardTags), AccessTools.Field(typeof(Hero), nameof(BeardTags)) },
            { nameof(TattooTags), AccessTools.Field(typeof(Hero), nameof(TattooTags)) },
            { nameof(_heroState), AccessTools.Field(typeof(Hero), nameof(_heroState)) },
            { nameof(_heroTraits), AccessTools.Field(typeof(Hero), nameof(_heroTraits)) },
            { nameof(_heroPerks), AccessTools.Field(typeof(Hero), nameof(_heroPerks)) },
            { nameof(_heroSkills), AccessTools.Field(typeof(Hero), nameof(_heroSkills)) },
            { nameof(_characterAttributes), AccessTools.Field(typeof(Hero), nameof(_characterAttributes)) },
            { nameof(IsNobleForOldSaves), AccessTools.Field(typeof(Hero), nameof(IsNobleForOldSaves)) },
            { nameof(LastVisitTimeOfHomeSettlement), AccessTools.Field(typeof(Hero), nameof(LastVisitTimeOfHomeSettlement)) },
            { nameof(Level), AccessTools.Field(typeof(Hero), nameof(Level)) },
            { nameof(_companionOf), AccessTools.Field(typeof(Hero), nameof(_companionOf)) },
            { nameof(_cachedLastSeenInformation), AccessTools.Field(typeof(Hero), nameof(_cachedLastSeenInformation)) },
            { nameof(_lastSeenInformationKnownToPlayer), AccessTools.Field(typeof(Hero), nameof(_lastSeenInformationKnownToPlayer)) },
            { nameof(SpcDaysInLocation), AccessTools.Field(typeof(Hero), nameof(SpcDaysInLocation)) },
            { nameof(_health), AccessTools.Field(typeof(Hero), nameof(_health)) },
            { nameof(_defaultAge), AccessTools.Field(typeof(Hero), nameof(_defaultAge)) },
            { nameof(_birthDay), AccessTools.Field(typeof(Hero), nameof(_birthDay)) },
            { nameof(_deathDay), AccessTools.Field(typeof(Hero), nameof(_deathDay)) },
            { nameof(_power), AccessTools.Field(typeof(Hero), nameof(_power)) },
            { nameof(_clan), AccessTools.Field(typeof(Hero), nameof(_clan)) },
            { nameof(_supporterOf), AccessTools.Field(typeof(Hero), nameof(_supporterOf)) },
            { nameof(_governorOf), AccessTools.Field(typeof(Hero), nameof(_governorOf)) },
            { nameof(_ownedWorkshops), AccessTools.Field(typeof(Hero), nameof(_ownedWorkshops)) },
            { nameof(Culture), AccessTools.Field(typeof(Hero), nameof(Culture)) },
            { nameof(_partyBelongedTo), AccessTools.Field(typeof(Hero), nameof(_partyBelongedTo)) },
            { nameof(_stayingInSettlement), AccessTools.Field(typeof(Hero), nameof(_stayingInSettlement)) },
            { nameof(SpecialItems), AccessTools.Field(typeof(Hero), nameof(SpecialItems)) },
            { nameof(_hasMet), AccessTools.Field(typeof(Hero), nameof(_hasMet)) },
            { nameof(_bornSettlement), AccessTools.Field(typeof(Hero), nameof(_bornSettlement)) },
            { nameof(_homeSettlement), AccessTools.Field(typeof(Hero), nameof(_homeSettlement)) },
            { nameof(_gold), AccessTools.Field(typeof(Hero), nameof(_gold)) },
            //{ nameof(_father), AccessTools.Field(typeof(Hero), nameof(_father)) },
            //{ nameof(_mother), AccessTools.Field(typeof(Hero), nameof(_mother)) },
            //{ nameof(_exSpouses), AccessTools.Field(typeof(Hero), nameof(_exSpouses)) },
            //{ nameof(_spouse), AccessTools.Field(typeof(Hero), nameof(_spouse)) },
            //{ nameof(_children), AccessTools.Field(typeof(Hero), nameof(_children)) },
            { nameof(IsPregnant), AccessTools.Field(typeof(Hero), nameof(IsPregnant)) },
            { nameof(_heroDeveloper), AccessTools.Field(typeof(Hero), nameof(_heroDeveloper)) },
        };
        

        private static readonly Dictionary<string, PropertyInfo> Properties = new Dictionary<string, PropertyInfo>
        {
            { nameof(StaticBodyProperties), AccessTools.Property(typeof(Hero), nameof(StaticBodyProperties)) },
            { nameof(Weight), AccessTools.Property(typeof(Hero), nameof(Weight)) },
            { nameof(Build), AccessTools.Property(typeof(Hero), nameof(Build)) },
            { nameof(EncyclopediaText), AccessTools.Property(typeof(Hero), nameof(EncyclopediaText)) },
            { nameof(IsFemale), AccessTools.Property(typeof(Hero), nameof(IsFemale)) },
            { nameof(BattleEquipment), AccessTools.Property(typeof(Hero), nameof(BattleEquipment)) },
            { nameof(CivilianEquipment), AccessTools.Property(typeof(Hero), nameof(CivilianEquipment)) },
            { nameof(CaptivityStartTime), AccessTools.Property(typeof(Hero), nameof(CaptivityStartTime)) },
            { nameof(PreferredUpgradeFormation), AccessTools.Property(typeof(Hero), nameof(PreferredUpgradeFormation)) },
            { nameof(IsMinorFactionHero), AccessTools.Property(typeof(Hero), nameof(IsMinorFactionHero)) },
            { nameof(Issue), AccessTools.Property(typeof(Hero), nameof(Issue)) },
            { nameof(Occupation), AccessTools.Property(typeof(Hero), nameof(Occupation)) },
            { nameof(DeathMark), AccessTools.Property(typeof(Hero), nameof(DeathMark)) },
            //{ nameof(DeathMarkKillerHero), AccessTools.Property(typeof(Hero), nameof(DeathMarkKillerHero)) },
            { nameof(LastExaminedLogEntryID), AccessTools.Property(typeof(Hero), nameof(LastExaminedLogEntryID)) },
            { nameof(OwnedCommonAreas), AccessTools.Property(typeof(Hero), nameof(OwnedCommonAreas)) },
            { nameof(OwnedCaravans), AccessTools.Property(typeof(Hero), nameof(OwnedCaravans)) },
            { nameof(PartyBelongedToAsPrisoner), AccessTools.Property(typeof(Hero), nameof(PartyBelongedToAsPrisoner)) },
            { nameof(LastMeetingTimeWithPlayer), AccessTools.Property(typeof(Hero), nameof(LastMeetingTimeWithPlayer)) },
            { nameof(RandomValue), AccessTools.Property(typeof(Hero), nameof(RandomValue)) },
            { nameof(BannerItem), AccessTools.Property(typeof(Hero), nameof(BannerItem)) },
        };
        #endregion

        public HeroSurrogate(Hero hero)
        {
            if(hero == null) return;

            LastTimeStampForActivity = (int)Fields[nameof(LastTimeStampForActivity)].GetValue(hero);
            _passedTimeAtHomeSettlement = (float)Fields[nameof(_passedTimeAtHomeSettlement)].GetValue(hero);
            _characterObject = (CharacterObject)Fields[nameof(_characterObject)].GetValue(hero);
            _firstName = (TextObject)Fields[nameof(_firstName)].GetValue(hero);
            _name = (TextObject)Fields[nameof(_name)].GetValue(hero);
            HairTags = (string)Fields[nameof(HairTags)].GetValue(hero);
            BeardTags = (string)Fields[nameof(BeardTags)].GetValue(hero);
            TattooTags = (string)Fields[nameof(TattooTags)].GetValue(hero);
            _heroState = (CharacterStates)Fields[nameof(_heroState)].GetValue(hero);
            _heroTraits = (CharacterTraits)Fields[nameof(_heroTraits)].GetValue(hero);
            _heroPerks = (CharacterPerks)Fields[nameof(_heroPerks)].GetValue(hero);
            _heroSkills = (CharacterSkills)Fields[nameof(_heroSkills)].GetValue(hero);
            _characterAttributes = (CharacterAttributes)Fields[nameof(_characterAttributes)].GetValue(hero);
            IsNobleForOldSaves = (bool)Fields[nameof(IsNobleForOldSaves)].GetValue(hero);
            LastVisitTimeOfHomeSettlement = (float)Fields[nameof(LastVisitTimeOfHomeSettlement)].GetValue(hero);
            Level = (int)Fields[nameof(Level)].GetValue(hero);
            _companionOf = (Clan)Fields[nameof(_companionOf)].GetValue(hero);
            _cachedLastSeenInformation = (HeroLastSeenInformation)Fields[nameof(_cachedLastSeenInformation)].GetValue(hero);
            _lastSeenInformationKnownToPlayer = (HeroLastSeenInformation)Fields[nameof(_lastSeenInformationKnownToPlayer)].GetValue(hero);
            SpcDaysInLocation = (int)Fields[nameof(SpcDaysInLocation)].GetValue(hero);
            _health = (int)Fields[nameof(_health)].GetValue(hero);
            _defaultAge = (float)Fields[nameof(_defaultAge)].GetValue(hero);
            _birthDay = (CampaignTime)Fields[nameof(_birthDay)].GetValue(hero);
            _deathDay = (CampaignTime)Fields[nameof(_deathDay)].GetValue(hero);
            _power = (float)Fields[nameof(_power)].GetValue(hero);
            _clan = (Clan)Fields[nameof(_clan)].GetValue(hero);
            _supporterOf = (Clan)Fields[nameof(_supporterOf)].GetValue(hero);
            _governorOf = (Town)Fields[nameof(_governorOf)].GetValue(hero);
            Culture = (CultureObject)Fields[nameof(Culture)].GetValue(hero);
            _partyBelongedTo = (MobileParty)Fields[nameof(_partyBelongedTo)].GetValue(hero);
            _stayingInSettlement = (Settlement)Fields[nameof(_stayingInSettlement)].GetValue(hero);
            
            _hasMet = (bool)Fields[nameof(_hasMet)].GetValue(hero);
            _bornSettlement = (Settlement)Fields[nameof(_bornSettlement)].GetValue(hero);
            _homeSettlement = (Settlement)Fields[nameof(_homeSettlement)].GetValue(hero);
            _gold = (int)Fields[nameof(_gold)].GetValue(hero);
            //_father = (Hero)Fields[nameof(_father)].GetValue(hero);
            //_mother = (Hero)Fields[nameof(_mother)].GetValue(hero);
            //_spouse = (Hero)Fields[nameof(_spouse)].GetValue(hero);
            IsPregnant = (bool)Fields[nameof(IsPregnant)].GetValue(hero);
            _heroDeveloper = (IHeroDeveloper)Fields[nameof(_heroDeveloper)].GetValue(hero);

            StaticBodyProperties = (StaticBodyProperties)Properties[nameof(StaticBodyProperties)].GetValue(hero);
            Weight = (Single)Properties[nameof(Weight)].GetValue(hero);
            Build = (Single)Properties[nameof(Build)].GetValue(hero);
            EncyclopediaText = (TextObject)Properties[nameof(EncyclopediaText)].GetValue(hero);
            IsFemale = (Boolean)Properties[nameof(IsFemale)].GetValue(hero);
            BattleEquipment = (Equipment)Properties[nameof(BattleEquipment)].GetValue(hero);
            CivilianEquipment = (Equipment)Properties[nameof(CivilianEquipment)].GetValue(hero);
            CaptivityStartTime = (CampaignTime)Properties[nameof(CaptivityStartTime)].GetValue(hero);
            PreferredUpgradeFormation = (FormationClass)Properties[nameof(PreferredUpgradeFormation)].GetValue(hero);
            IsMinorFactionHero = (Boolean)Properties[nameof(IsMinorFactionHero)].GetValue(hero);
            Issue = (IssueBase)Properties[nameof(Issue)].GetValue(hero);
            Occupation = (Occupation)Properties[nameof(Occupation)].GetValue(hero);
            DeathMark = (KillCharacterActionDetail)Properties[nameof(DeathMark)].GetValue(hero);
            //DeathMarkKillerHero = (Hero)Properties[nameof(DeathMarkKillerHero)].GetValue(hero);
            LastExaminedLogEntryID = (Int64)Properties[nameof(LastExaminedLogEntryID)].GetValue(hero);
            PartyBelongedToAsPrisoner = (PartyBase)Properties[nameof(PartyBelongedToAsPrisoner)].GetValue(hero);
            LastMeetingTimeWithPlayer = (CampaignTime)Properties[nameof(LastMeetingTimeWithPlayer)].GetValue(hero);
            RandomValue = (Int32)Properties[nameof(RandomValue)].GetValue(hero);
            BannerItem = (EquipmentElement)Properties[nameof(BannerItem)].GetValue(hero);

            // Collections
            ArraySerializer<CharacterObject> volunteerTypesArraySerializer = new ArraySerializer<CharacterObject>();
            volunteerTypesArraySerializer.Pack((CharacterObject[])Fields[nameof(VolunteerTypes)].GetValue(hero));
            VolunteerTypes = volunteerTypesArraySerializer;

            ListSerializer<Workshop> workshopListSerializer = new ListSerializer<Workshop>();
            workshopListSerializer.Pack((List<Workshop>)Fields[nameof(_ownedWorkshops)].GetValue(hero));
            _ownedWorkshops = workshopListSerializer;

            ListSerializer<ItemObject> specialItemsListSerializer = new ListSerializer<ItemObject>();
            specialItemsListSerializer.Pack((List<ItemObject>)Fields[nameof(SpecialItems)].GetValue(hero));
            SpecialItems = specialItemsListSerializer;

            //ListSerializer<Hero> exSpousesListSerializer = new ListSerializer<Hero>();
            //exSpousesListSerializer.Pack((List<Hero>)Fields[nameof(_exSpouses)].GetValue(hero));
            //_exSpouses = exSpousesListSerializer;

            //ListSerializer<Hero> childrenListSerializer = new ListSerializer<Hero>();
            //childrenListSerializer.Pack((List<Hero>)Fields[nameof(_children)].GetValue(hero));
            //_children = childrenListSerializer;

            ListSerializer<CommonAreaPartyComponent> commonAreaListSerializer = new ListSerializer<CommonAreaPartyComponent>();
            commonAreaListSerializer.Pack((List<CommonAreaPartyComponent>)Properties[nameof(OwnedCommonAreas)].GetValue(hero));
            OwnedCommonAreas = commonAreaListSerializer;

            ListSerializer<CaravanPartyComponent> caravanListSerializer = new ListSerializer<CaravanPartyComponent>();
            caravanListSerializer.Pack((List<CaravanPartyComponent>)Properties[nameof(OwnedCaravans)].GetValue(hero));
            OwnedCaravans = caravanListSerializer;
        }

        private Hero Deserialize()
        {
            Hero newHero = new Hero();

            Fields[nameof(LastTimeStampForActivity)].SetValue(newHero, LastTimeStampForActivity);
            Fields[nameof(_passedTimeAtHomeSettlement)].SetValue(newHero, _passedTimeAtHomeSettlement);
            Fields[nameof(_characterObject)].SetValue(newHero, _characterObject);
            Fields[nameof(_firstName)].SetValue(newHero, _firstName);
            Fields[nameof(_name)].SetValue(newHero, _name);
            Fields[nameof(HairTags)].SetValue(newHero, HairTags);
            Fields[nameof(BeardTags)].SetValue(newHero, BeardTags);
            Fields[nameof(TattooTags)].SetValue(newHero, TattooTags);
            Fields[nameof(_heroState)].SetValue(newHero, _heroState);
            Fields[nameof(_heroTraits)].SetValue(newHero, _heroTraits);
            Fields[nameof(_heroPerks)].SetValue(newHero, _heroPerks);
            Fields[nameof(_heroSkills)].SetValue(newHero, _heroSkills);
            Fields[nameof(_characterAttributes)].SetValue(newHero, _characterAttributes);
            Fields[nameof(IsNobleForOldSaves)].SetValue(newHero, IsNobleForOldSaves);
            Fields[nameof(LastVisitTimeOfHomeSettlement)].SetValue(newHero, LastVisitTimeOfHomeSettlement);
            Fields[nameof(Level)].SetValue(newHero, Level);
            Fields[nameof(_companionOf)].SetValue(newHero, _companionOf);
            Fields[nameof(_cachedLastSeenInformation)].SetValue(newHero, _cachedLastSeenInformation);
            Fields[nameof(_lastSeenInformationKnownToPlayer)].SetValue(newHero, _lastSeenInformationKnownToPlayer);
            Fields[nameof(SpcDaysInLocation)].SetValue(newHero, SpcDaysInLocation);
            Fields[nameof(_health)].SetValue(newHero, _health);
            Fields[nameof(_defaultAge)].SetValue(newHero, _defaultAge);
            Fields[nameof(_birthDay)].SetValue(newHero, _birthDay);
            Fields[nameof(_deathDay)].SetValue(newHero, _deathDay);
            Fields[nameof(_power)].SetValue(newHero, _power);
            Fields[nameof(_clan)].SetValue(newHero, _clan);
            Fields[nameof(_supporterOf)].SetValue(newHero, _supporterOf);
            Fields[nameof(_governorOf)].SetValue(newHero, _governorOf);
            Fields[nameof(Culture)].SetValue(newHero, Culture);
            Fields[nameof(_partyBelongedTo)].SetValue(newHero, _partyBelongedTo);
            Fields[nameof(_stayingInSettlement)].SetValue(newHero, _stayingInSettlement);
            Fields[nameof(_hasMet)].SetValue(newHero, _hasMet);
            Fields[nameof(_bornSettlement)].SetValue(newHero, _bornSettlement);
            Fields[nameof(_homeSettlement)].SetValue(newHero, _homeSettlement);
            Fields[nameof(_gold)].SetValue(newHero, _gold);
            //Fields[nameof(_father)].SetValue(newHero, _father);
            //Fields[nameof(_mother)].SetValue(newHero, _mother);
            //Fields[nameof(_spouse)].SetValue(newHero, _spouse);
            Fields[nameof(IsPregnant)].SetValue(newHero, IsPregnant);
            Fields[nameof(_heroDeveloper)].SetValue(newHero, _heroDeveloper);

            Properties[nameof(StaticBodyProperties)].SetValue(newHero, StaticBodyProperties);
            Properties[nameof(Weight)].SetValue(newHero, Weight);
            Properties[nameof(Build)].SetValue(newHero, Build);
            Properties[nameof(EncyclopediaText)].SetValue(newHero, EncyclopediaText);
            Properties[nameof(IsFemale)].SetValue(newHero, IsFemale);
            Properties[nameof(BattleEquipment)].SetValue(newHero, BattleEquipment);
            Properties[nameof(CivilianEquipment)].SetValue(newHero, CivilianEquipment);
            Properties[nameof(CaptivityStartTime)].SetValue(newHero, CaptivityStartTime);
            Properties[nameof(PreferredUpgradeFormation)].SetValue(newHero, PreferredUpgradeFormation);
            Properties[nameof(IsMinorFactionHero)].SetValue(newHero, IsMinorFactionHero);
            Properties[nameof(Issue)].SetValue(newHero, Issue);
            Properties[nameof(Occupation)].SetValue(newHero, Occupation);
            Properties[nameof(DeathMark)].SetValue(newHero, DeathMark);
            //Properties[nameof(DeathMarkKillerHero)].SetValue(newHero, DeathMarkKillerHero);
            Properties[nameof(LastExaminedLogEntryID)].SetValue(newHero, LastExaminedLogEntryID);
            
            Properties[nameof(PartyBelongedToAsPrisoner)].SetValue(newHero, PartyBelongedToAsPrisoner);
            Properties[nameof(LastMeetingTimeWithPlayer)].SetValue(newHero, LastMeetingTimeWithPlayer);
            Properties[nameof(RandomValue)].SetValue(newHero, RandomValue);
            Properties[nameof(BannerItem)].SetValue(newHero, BannerItem);


            Fields[nameof(VolunteerTypes)].SetValue(newHero, VolunteerTypes.Unpack());
            Fields[nameof(_ownedWorkshops)].SetValue(newHero, _ownedWorkshops.Unpack());
            Fields[nameof(SpecialItems)].SetValue(newHero, SpecialItems.Unpack());
            //Fields[nameof(_exSpouses)].SetValue(newHero, _exSpouses.Unpack());
            //Fields[nameof(_children)].SetValue(newHero, _children.Unpack());

            Properties[nameof(OwnedCommonAreas)].SetValue(newHero, OwnedCommonAreas.Unpack());
            Properties[nameof(OwnedCaravans)].SetValue(newHero, OwnedCaravans.Unpack());

            return newHero;
        }

        /// <summary>
        ///     TODO
        /// </summary>
        /// <param name="obj">TODO</param>
        /// <returns>TODO</returns>
        public static implicit operator HeroSurrogate(Hero obj)
        {
            return new HeroSurrogate(obj);
        }

        /// <summary>
        ///     TODO
        /// </summary>
        /// <param name="surrogate">TODO</param>
        /// <returns>TODO</returns>
        public static implicit operator Hero(HeroSurrogate surrogate)
        {
            return surrogate.Deserialize();
        }
    }
}
