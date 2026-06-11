using Common.Extensions;
using Common.Util;
using GameInterface.Services.ObjectManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Tests.Serialization.Helpers;

internal class HeroHelper
{
    public MobileParty HeroParty { get; private set; }
    public Settlement HomeSettlement { get; private set; }


    #region HeroFields
    private static readonly FieldInfo Hero_exSpouses = typeof(Hero).GetField("_exSpouses", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
    private static readonly FieldInfo Hero_children = typeof(Hero).GetField("_children", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
    #endregion

    public static RandomHeroWithData CreateRandomHero(IObjectManager objectManager)
    {
        RandomHeroWithData heroData = new RandomHeroWithData(objectManager);

        Hero hero = heroData.Hero;
        hero.StringId = "My Hero";

        #region ValueData
        hero.StaticBodyProperties = heroData.StaticBodyProperties;
        hero.Weight = ReflectionExtensions.Random<float>();
        hero.Build = ReflectionExtensions.Random<float>();
        hero.LastTimeStampForActivity = ReflectionExtensions.Random<int>();
        hero.VolunteerTypes = heroData.VolunteerTypes;
        hero._passedTimeAtHomeSettlement = ReflectionExtensions.Random<float>();
        hero._characterObject = heroData.CharacterObject;
        hero._firstName = new TextObject("My First Name");
        hero._name = new TextObject("My Name");
        hero._originClan = heroData.Clan;
        hero.EncyclopediaText = new TextObject("My EEncyclopedia Text");
        hero.IsFemale = ReflectionExtensions.Random<bool>();
        hero._battleEquipment = (Equipment)FormatterServices.GetUninitializedObject(typeof(Equipment));
        hero._civilianEquipment = (Equipment)FormatterServices.GetUninitializedObject(typeof(Equipment));
        hero._stealthEquipment = (Equipment)FormatterServices.GetUninitializedObject(typeof(Equipment));
        hero.CaptivityStartTime = new CampaignTime();
        hero.PreferredUpgradeFormation = ReflectionExtensions.Random<FormationClass>();
        hero._heroState = ReflectionExtensions.Random<Hero.CharacterStates>();
        hero.IsNobleForOldSaves = ReflectionExtensions.Random<bool>();
        hero.IsMinorFactionHero = ReflectionExtensions.Random<bool>();
        hero.Level = ReflectionExtensions.Random<int>();
        hero._companionOf = heroData.Clan;
        hero.Occupation = ReflectionExtensions.Random<Occupation>();
        hero.DeathMark = ReflectionExtensions.Random<KillCharacterAction.KillCharacterActionDetail>();
        hero.DeathMarkKillerHero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
        hero._health = ReflectionExtensions.Random<int>();
        hero._defaultAge = ReflectionExtensions.Random<float>();
        hero._birthDay = new CampaignTime();
        hero._deathDay = new CampaignTime();
        hero._power = ReflectionExtensions.Random<float>();
        hero.LastExaminedLogEntryID = ReflectionExtensions.Random<long>();
        hero._clan = heroData.Clan;
        hero._supporterOf = heroData.Clan;
        hero._governorOf = heroData.GoverningTown;
        hero._ownedWorkshops = heroData.OwnedWorkshops;
        hero.OwnedAlleys = heroData.OwnedAlleys;
        hero.Culture = heroData.Culture;
        hero.OwnedCaravans = heroData.OwnedCaravans;
        hero._partyBelongedTo = heroData.HeroParty;
        hero.PartyBelongedToAsPrisoner = heroData.PartyBelongedToAsPrisoner;
        hero._stayingInSettlement = heroData.HomeSettlement;
        hero.SpecialItems = heroData.SpecialItems;
        hero._isKnownToPlayer = ReflectionExtensions.Random<bool>();
        hero._hasMet = ReflectionExtensions.Random<bool>();
        hero.LastMeetingTimeWithPlayer = new CampaignTime();
        hero._bornSettlement = heroData.HomeSettlement;
        hero._homeSettlement = heroData.HomeSettlement;
        hero._gold = ReflectionExtensions.Random<int>();
        hero.RandomValue = ReflectionExtensions.Random<int>();
        hero._father = heroData.Father;
        hero._mother = heroData.Mother;
        Hero_exSpouses.SetValue(hero, heroData.ExSpouses);
        hero._spouse = heroData.Spouse;
        Hero_children.SetValue(hero, heroData.Children);
        hero.IsPregnant = ReflectionExtensions.Random<bool>();
        hero._heroDeveloper = heroData.HeroDeveloper;
        hero._heroSkills = new PropertyOwner<SkillObject>();
        hero._heroTraits = new PropertyOwner<TraitObject>();
        hero._heroPerks = new PropertyOwner<PerkObject>();
        hero._characterAttributes = new PropertyOwner<CharacterAttribute>();
        hero.LastKnownClosestSettlement = heroData.LastKnownClosestSettlement;
        #endregion

        return heroData;
    }

    /// <summary>
    /// Random Hero data used for verification comparison
    /// </summary>
    public class RandomHeroWithData
    {
        private readonly IObjectManager objectManager;
        public Hero Hero { get; }
        public StaticBodyProperties StaticBodyProperties { get; private set; } = new StaticBodyProperties(1, 2, 3, 4, 5, 6, 7, 8);
        public MobileParty HeroParty { get; private set; }
        public CharacterObject CharacterObject { get; private set; }
        public Settlement HomeSettlement { get; private set; }
        public Settlement LastKnownClosestSettlement { get; private set; }
        public CharacterObject[] VolunteerTypes { get; private set; }
        public Clan Clan { get; private set; }
        public Town GoverningTown { get; private set; }
        public MBList<Workshop> OwnedWorkshops { get; private set; }
        public MBList<Alley> OwnedAlleys { get; private set; }
        public CultureObject Culture { get; private set; }
        public List<CaravanPartyComponent> OwnedCaravans { get; private set; }
        public PartyBase PartyBelongedToAsPrisoner { get; private set; }
        public MBList<ItemObject> SpecialItems { get; private set; }
        public Hero Father { get; private set; }
        public Hero Mother { get; private set; }
        public MBList<Hero> ExSpouses { get; private set; }
        public Hero Spouse { get; private set; }
        public MBList<Hero> Children { get; private set; }
        public HeroDeveloper HeroDeveloper { get; private set; }

        public RandomHeroWithData(IObjectManager objectManager)
        {
            this.objectManager = objectManager;

            Hero = CreateHero("My Hero");

            Clan = CreateClan("My Clan");
            HeroParty = CreateParty("My Party", Hero, Clan);
            PartyBelongedToAsPrisoner = HeroParty.Party;
            CharacterObject = CreateCharacterObject("My Character Object");
            HomeSettlement = CreateSettlement("Home Settlement");
            HomeSettlement.Town = CreateTown("My Town");
            LastKnownClosestSettlement = CreateSettlement("Last Seen Settlement");

            VolunteerTypes = new CharacterObject[]
            {
                CreateCharacterObject("Volunteer 1"),
                CreateCharacterObject("Volunteer 2"),
                CreateCharacterObject("Volunteer 3"),
            };

            GoverningTown = CreateTown("My Town");
            Hero.GovernorOf = GoverningTown;

            Culture = CreateCulture("My Culture");

            OwnedWorkshops = new MBList<Workshop>
            {
                CreateWorkshop(HomeSettlement),
                CreateWorkshop(HomeSettlement),
                CreateWorkshop(HomeSettlement),
                CreateWorkshop(HomeSettlement),
            };

            HomeSettlement.Town.Workshops = OwnedWorkshops.ToArray();

            OwnedAlleys = new MBList<Alley>
            {
                CreateAlley(HomeSettlement),
                CreateAlley(HomeSettlement),
            };

            HomeSettlement.Alleys = OwnedAlleys;

            OwnedCaravans = new List<CaravanPartyComponent>
            {
                CreateCaravan(),
                CreateCaravan(),
            };

            SpecialItems = new MBList<ItemObject>
            {
                CreateItem("Special Item 1"),
                CreateItem("Special Item 2"),
            };

            Father = CreateHero("My Father");
            Mother = CreateHero("My Mother");

            ExSpouses = new MBList<Hero>()
            {
                CreateHero("My Ex 1"),
                CreateHero("My Ex 2"),
            };

            Spouse = CreateHero("My Spouse");

            Children = new MBList<Hero>
            {
                CreateHero("My Child 1"),
                CreateHero("My Child 2"),
                CreateHero("My Child 3"),
            };

            HeroDeveloper = CreateHeroDeveloper();
        }

        public MobileParty CreateMobilePartyWithData(string stringId, Clan clan)
        {
            var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
            party.StringId = stringId;

            party.Ai = new MobilePartyAi(party);

            party.LastVisitedSettlement = CreateSettlement("Last Visited Settlement");
            party._currentSettlement = CreateSettlement("Current Settlement");
            party._customHomeSettlement = CreateSettlement("Custom Home Settlement");
            party._actualClan = clan;
            party._partyComponent = (LordPartyComponent)FormatterServices.GetUninitializedObject(typeof(LordPartyComponent));

            var partyBase = CreatePartyBase(party);

            objectManager.AddExisting(stringId, party);

            return party;
        }

        #region CreationMethods
        private MobileParty CreateParty(string stringId, Hero leader, Clan clan)
        {
            var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
            party.StringId = stringId;

            party.Ai = new MobilePartyAi(party);

            party.LastVisitedSettlement = CreateSettlement("Last Visited Settlement");
            party._currentSettlement = CreateSettlement("Current Settlement");
            party._customHomeSettlement = CreateSettlement("Custom Home Settlement");
            party._actualClan = clan;
            party._partyComponent = CreateLordPartyComponent(leader);
            party.FirstMate = leader;
            party.Navigator = leader;

            var partyBase = CreatePartyBase(party);

            partyBase.Settlement = party._currentSettlement;

            objectManager.AddExisting(stringId, party);

            return party;
        }

        private LordPartyComponent CreateLordPartyComponent(Hero leader)
        {
            var lordPartyComponent = (LordPartyComponent)FormatterServices.GetUninitializedObject(typeof(LordPartyComponent));

            lordPartyComponent._leader = leader;

            return lordPartyComponent;
        }

        private PartyBase CreatePartyBase(MobileParty party)
        {
            var partyBase = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));

            party.Party = partyBase;
            partyBase.MobileParty = party;

            partyBase.MemberRoster = new TroopRoster();
            partyBase.PrisonRoster = new TroopRoster();

            partyBase.ItemRoster = new ItemRoster();

            partyBase.CustomName = party.Name;
            partyBase.CustomBanner = new Banner();

            return partyBase;
        }

        private CharacterObject CreateCharacterObject(string stringId)
        {
            var characterObject = (CharacterObject)FormatterServices.GetUninitializedObject(typeof(CharacterObject));
            characterObject.StringId = stringId;
            characterObject._basicName = new TextObject(stringId);
            characterObject.UpgradeTargets = Array.Empty<CharacterObject>();
            objectManager.AddExisting(stringId, characterObject);

            return characterObject;
        }

        private Settlement CreateSettlement(string stringId)
        {
            var settlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
            settlement.StringId = stringId;
            objectManager.AddExisting(stringId, settlement);

            return settlement;
        }

        private Clan CreateClan(string stringId)
        {
            var clan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
            clan.StringId = stringId;
            objectManager.AddExisting(stringId, clan);

            return clan;
        }

        private Town CreateTown(string stringId)
        {
            var town = (Town)FormatterServices.GetUninitializedObject(typeof(Town));
            town.StringId = stringId;
            objectManager.AddExisting(stringId, town);

            return town;
        }

        private CultureObject CreateCulture(string stringId)
        {
            var culture = (CultureObject)FormatterServices.GetUninitializedObject(typeof(CultureObject));
            culture.StringId = stringId;
            objectManager.AddExisting(stringId, culture);

            return culture;
        }


        private static readonly FieldInfo Workshop_Settlement = typeof(Workshop).GetField("_settlement", BindingFlags.NonPublic | BindingFlags.Instance);
        private Workshop CreateWorkshop(Settlement settlement)
        {
            var workshop = (Workshop)FormatterServices.GetUninitializedObject(typeof(Workshop));

            Workshop_Settlement.SetValue(workshop, settlement);

            return workshop;
        }

        private Alley CreateAlley(Settlement settlement)
        {
            var alley = (Alley)FormatterServices.GetUninitializedObject(typeof(Alley));

            alley._settlement = settlement;

            return alley;
        }

        private CaravanPartyComponent CreateCaravan()
        {
            var caravan = (CaravanPartyComponent)FormatterServices.GetUninitializedObject(typeof(CaravanPartyComponent));

            return caravan;
        }

        private ItemObject CreateItem(string stringId)
        {
            var item = (ItemObject)FormatterServices.GetUninitializedObject(typeof(ItemObject));

            objectManager.AddExisting(stringId, item);

            return item;
        }

        private Hero CreateHero(string stringId)
        {
            var hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
            hero.StringId = stringId;
            objectManager.AddExisting(stringId, hero);

            return hero;
        }

        private HeroDeveloper CreateHeroDeveloper()
        {
            var heroDeveloper = (HeroDeveloper)FormatterServices.GetUninitializedObject(typeof(HeroDeveloper));

            return heroDeveloper;
        }
        #endregion
    }
}
