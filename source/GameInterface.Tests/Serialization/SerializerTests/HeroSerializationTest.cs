using Common.Extensions;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using Xunit;
using Xunit.Abstractions;
using static TaleWorlds.CampaignSystem.Hero;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class HeroSerializationTest
    {
        private readonly ITestOutputHelper output;

        public HeroSerializationTest(ITestOutputHelper output)
        {
            this.output = output;
            GameBootStrap.Initialize();
        }

        [Fact]
        public void Hero_Serialize()
        {
            HeroFactory.RandomHeroWithData heroData = HeroFactory.CreateRandomHero();
            Hero hero = heroData.Hero;

            foreach(FieldInfo field in typeof(Hero).GetAllInstanceFields(HeroBinaryPackage.Excludes))
            {
                object value = field.GetValue(hero);
                if(value == null)
                {
                    output.WriteLine($"{field.Name} was null.");
                }
                Assert.NotNull(value);
            }    

            BinaryPackageFactory factory = new BinaryPackageFactory();
            HeroBinaryPackage package = new HeroBinaryPackage(hero, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        

        [Fact]
        public void Hero_Full_Serialization()
        {
            // Create hero with partially-random values
            HeroFactory.RandomHeroWithData heroData = HeroFactory.CreateRandomHero();
            Hero hero = heroData.Hero;

            // Setup serialization
            BinaryPackageFactory factory = new BinaryPackageFactory();
            HeroBinaryPackage package = new HeroBinaryPackage(hero, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<HeroBinaryPackage>(obj);

            HeroBinaryPackage returnedPackage = (HeroBinaryPackage)obj;

            Hero newHero = returnedPackage.Unpack<Hero>();

            // Verify PropertyOwner types
            CharacterAttributes newAttribues = (CharacterAttributes)HeroFactory.Hero_characterAttributes.GetValue(newHero);
            AssertPropertyOwnerEqual(heroData.CharacterAttributes, newAttribues);
            CharacterPerks newPerks = (CharacterPerks)HeroFactory.Hero_heroPerks.GetValue(newHero);
            AssertPropertyOwnerEqual(heroData.CharacterPerks, newPerks);
            CharacterSkills newSkills = (CharacterSkills)HeroFactory.Hero_heroSkills.GetValue(newHero);
            AssertPropertyOwnerEqual(heroData.CharacterSkills, newSkills);
            CharacterTraits newTraits = (CharacterTraits)HeroFactory.Hero_heroTraits.GetValue(newHero);
            AssertPropertyOwnerEqual(heroData.CharacterTraits, newTraits);

            // Verify StringId resolvable list types
            AssertValuesSame(heroData.Children, newHero.Children);
            AssertValuesSame(heroData.ExSpouses, newHero.ExSpouses);
            AssertValuesSame(heroData.OwnedCaravans.Select(pc => pc.MobileParty), newHero.OwnedCaravans.Select(pc => pc.MobileParty));
            AssertValuesSame(heroData.OwnedAlleys.Select(pc => pc.Owner), newHero.OwnedAlleys.Select(pc => pc.Owner));
            AssertValuesSame(heroData.OwnedWorkshops, newHero.OwnedWorkshops);
            AssertValuesSame(heroData.SpecialItems, newHero.SpecialItems);

            // Verify StringId resolvable types
            Assert.Same(heroData.Clan, newHero.Clan);
            Assert.Same(heroData.Culture, newHero.Culture);
            Assert.Same(heroData.Father, newHero.Father);
            Assert.Same(heroData.GoverningTown, newHero.GovernorOf);
            Assert.Same(heroData.HeroParty, newHero.PartyBelongedTo);
            Assert.Same(heroData.CharacterObject, newHero.CharacterObject);
            Assert.Same(heroData.HomeSettlement, newHero.HomeSettlement);
            Assert.Same(heroData.Mother, newHero.Mother);
            Assert.Same(heroData.Spouse, newHero.Spouse);

            // Verify data types are equal
            Assert.Equal(heroData.HeroDeveloper.ToString(), newHero.HeroDeveloper.ToString());
            Assert.Equal(heroData.StaticBodyProperties, newHero.BodyProperties.StaticProperties);
            Assert.Equal(heroData.PartyBelongedToAsPrisoner.ToString(), newHero.PartyBelongedToAsPrisoner.ToString());
            Assert.Equal(heroData.LastMeetingTimeWithPlayer, newHero.LastMeetingTimeWithPlayer);
            AssertValuesEqual(heroData.VolunteerTypes, newHero.VolunteerTypes);
        }

        private void AssertValuesEqual<T>(IEnumerable<T> values1, IEnumerable<T> values2)
        {
            Assert.Equal(values1.Count(), values2.Count());

            foreach (var vals in values1.Zip(values2, (v1, v2) => (v1, v2)))
            {
                Assert.Equal(vals.v1, vals.v2);
            }
        }

        private void AssertValuesSame<T>(IEnumerable<T> values1, IEnumerable<T> values2)
        {
            Assert.Equal(values1.Count(), values2.Count());

            foreach (var vals in values1.Zip(values2, (v1, v2) => (v1, v2)))
            {
                Assert.Same(vals.v1, vals.v2);
            }
        }

        private void AssertPropertyOwnerEqual<T>(PropertyOwner<T> owner1, PropertyOwner<T> owner2) where T : MBObjectBase
        {
            var _attributes = typeof(PropertyOwner<>).MakeGenericType(typeof(T)).GetField("_attributes", BindingFlags.NonPublic | BindingFlags.Instance);

            Dictionary<T, int> values1 = (Dictionary<T, int>)_attributes.GetValue(owner1);
            Dictionary<T, int> values2 = (Dictionary<T, int>)_attributes.GetValue(owner2);

            Assert.Equal(values1.Count, values2.Count);

            AssertValuesSame(values1.Keys, values2.Keys);
            AssertValuesEqual(values1.Values, values2.Values);
        }

        [Fact]
        public void Hero_StringId_Serialization()
        {
            HeroFactory.RandomHeroWithData heroData = HeroFactory.CreateRandomHero();
            Hero hero = heroData.Hero;
            MBObjectManager.Instance.RegisterObject(hero);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            HeroBinaryPackage package = new HeroBinaryPackage(hero, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<HeroBinaryPackage>(obj);

            HeroBinaryPackage returnedPackage = (HeroBinaryPackage)obj;

            Hero newHero = returnedPackage.Unpack<Hero>();

            Assert.Same(hero, newHero);
        }
    }

    /// <summary>
    /// Factory for creating test heros
    /// </summary>
    internal class HeroFactory
    {
        public MobileParty HeroParty { get; private set; }
        public Settlement HomeSettlement { get; private set; }


        #region HeroFields
        private static readonly FieldInfo Hero_StaticBodyProperties = typeof(Hero).GetField("<StaticBodyProperties>k__BackingField", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_Weight = typeof(Hero).GetField("<Weight>k__BackingField", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_Build = typeof(Hero).GetField("<Build>k__BackingField", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_LastTimeStampForActivity = typeof(Hero).GetField("LastTimeStampForActivity", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_VolunteerTypes = typeof(Hero).GetField("VolunteerTypes", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_passedTimeAtHomeSettlement = typeof(Hero).GetField("_passedTimeAtHomeSettlement", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_characterObject = typeof(Hero).GetField("_characterObject", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_firstName = typeof(Hero).GetField("_firstName", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_name = typeof(Hero).GetField("_name", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_EncyclopediaText = typeof(Hero).GetField("<EncyclopediaText>k__BackingField", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_IsFemale = typeof(Hero).GetField("<IsFemale>k__BackingField", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_HairTags = typeof(Hero).GetField("HairTags", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_BeardTags = typeof(Hero).GetField("BeardTags", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_TattooTags = typeof(Hero).GetField("TattooTags", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_BattleEquipment = typeof(Hero).GetField("<BattleEquipment>k__BackingField", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_CivilianEquipment = typeof(Hero).GetField("<CivilianEquipment>k__BackingField", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_CaptivityStartTime = typeof(Hero).GetField("<CaptivityStartTime>k__BackingField", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_PreferredUpgradeFormation = typeof(Hero).GetField("<PreferredUpgradeFormation>k__BackingField", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_heroState = typeof(Hero).GetField("_heroState", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        public static readonly FieldInfo Hero_heroTraits = typeof(Hero).GetField("_heroTraits", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        public static readonly FieldInfo Hero_heroPerks = typeof(Hero).GetField("_heroPerks", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        public static readonly FieldInfo Hero_heroSkills = typeof(Hero).GetField("_heroSkills", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        public static readonly FieldInfo Hero_characterAttributes = typeof(Hero).GetField("_characterAttributes", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_IsNobleForOldSaves = typeof(Hero).GetField("IsNobleForOldSaves", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_IsMinorFactionHero = typeof(Hero).GetField("<IsMinorFactionHero>k__BackingField", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_LastVisitTimeOfHomeSettlement = typeof(Hero).GetField("LastVisitTimeOfHomeSettlement", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_Level = typeof(Hero).GetField("Level", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_Issue = typeof(Hero).GetField("<Issue>k__BackingField", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_companionOf = typeof(Hero).GetField("_companionOf", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_Occupation = typeof(Hero).GetField("<Occupation>k__BackingField", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_DeathMark = typeof(Hero).GetField("<DeathMark>k__BackingField", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_DeathMarkKillerHero = typeof(Hero).GetField("<DeathMarkKillerHero>k__BackingField", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_cachedLastSeenInformation = typeof(Hero).GetField("_cachedLastSeenInformation", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_lastSeenInformationKnownToPlayer = typeof(Hero).GetField("_lastSeenInformationKnownToPlayer", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_SpcDaysInLocation = typeof(Hero).GetField("SpcDaysInLocation", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_health = typeof(Hero).GetField("_health", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_defaultAge = typeof(Hero).GetField("_defaultAge", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_birthDay = typeof(Hero).GetField("_birthDay", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_deathDay = typeof(Hero).GetField("_deathDay", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_power = typeof(Hero).GetField("_power", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_LastExaminedLogEntryID = typeof(Hero).GetField("<LastExaminedLogEntryID>k__BackingField", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_clan = typeof(Hero).GetField("_clan", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_supporterOf = typeof(Hero).GetField("_supporterOf", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_governorOf = typeof(Hero).GetField("_governorOf", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_ownedWorkshops = typeof(Hero).GetField("_ownedWorkshops", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_OwnedAlleys = typeof(Hero).GetField("<OwnedAlleys>k__BackingField", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_Culture = typeof(Hero).GetField("Culture", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_OwnedCaravans = typeof(Hero).GetField("<OwnedCaravans>k__BackingField", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_partyBelongedTo = typeof(Hero).GetField("_partyBelongedTo", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_PartyBelongedToAsPrisoner = typeof(Hero).GetField("<PartyBelongedToAsPrisoner>k__BackingField", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_stayingInSettlement = typeof(Hero).GetField("_stayingInSettlement", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_SpecialItems = typeof(Hero).GetField("SpecialItems", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_hasMet = typeof(Hero).GetField("_hasMet", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly PropertyInfo Hero_LastMeetingTimeWithPlayer = typeof(Hero).GetProperty(nameof(Hero.LastMeetingTimeWithPlayer));
        private static readonly FieldInfo Hero_bornSettlement = typeof(Hero).GetField("_bornSettlement", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_homeSettlement = typeof(Hero).GetField("_homeSettlement", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_gold = typeof(Hero).GetField("_gold", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_RandomValue = typeof(Hero).GetField("<RandomValue>k__BackingField", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_father = typeof(Hero).GetField("_father", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_mother = typeof(Hero).GetField("_mother", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_exSpouses = typeof(Hero).GetField("_exSpouses", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_ExSpouses = typeof(Hero).GetField("ExSpouses", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_spouse = typeof(Hero).GetField("_spouse", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_children = typeof(Hero).GetField("_children", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_IsPregnant = typeof(Hero).GetField("IsPregnant", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo Hero_heroDeveloper = typeof(Hero).GetField("_heroDeveloper", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        #endregion

        public static RandomHeroWithData CreateRandomHero()
        {
            RandomHeroWithData heroData = new RandomHeroWithData();

            Hero hero = heroData.Hero;
            hero.StringId = "My Hero";

            #region ValueData
            Hero_StaticBodyProperties.SetValue(hero, heroData.StaticBodyProperties);
            Hero_Weight.SetRandom(hero);
            Hero_Build.SetRandom(hero);
            Hero_LastTimeStampForActivity.SetRandom(hero);
            Hero_VolunteerTypes.SetValue(hero, heroData.VolunteerTypes);
            Hero_passedTimeAtHomeSettlement.SetRandom(hero);
            Hero_characterObject.SetValue(hero, heroData.CharacterObject);
            Hero_firstName.SetValue(hero, new TextObject("My First Name"));
            Hero_name.SetValue(hero, new TextObject("My Name"));
            Hero_EncyclopediaText.SetValue(hero, new TextObject("My EEncyclopedia Text"));
            Hero_IsFemale.SetRandom(hero);
            Hero_HairTags.SetRandom(hero);
            Hero_BeardTags.SetRandom(hero);
            Hero_TattooTags.SetRandom(hero);
            Hero_BattleEquipment.SetValue(hero, (Equipment)FormatterServices.GetUninitializedObject(typeof(Equipment)));
            Hero_CivilianEquipment.SetValue(hero, (Equipment)FormatterServices.GetUninitializedObject(typeof(Equipment)));
            Hero_CaptivityStartTime.SetValue(hero, new CampaignTime());
            Hero_PreferredUpgradeFormation.SetRandom(hero);
            Hero_heroState.SetRandom(hero);
            Hero_heroTraits.SetValue(hero, heroData.CharacterTraits);
            Hero_heroPerks.SetValue(hero, heroData.CharacterPerks);
            Hero_heroSkills.SetValue(hero, heroData.CharacterSkills);
            Hero_characterAttributes.SetValue(hero, heroData.CharacterAttributes);
            Hero_IsNobleForOldSaves.SetRandom(hero);
            Hero_IsMinorFactionHero.SetRandom(hero);
            Hero_LastVisitTimeOfHomeSettlement.SetRandom(hero);
            Hero_Level.SetRandom(hero);
            Hero_companionOf.SetValue(hero, heroData.Clan);
            Hero_Occupation.SetRandom(hero);
            Hero_DeathMark.SetRandom(hero);
            Hero_DeathMarkKillerHero.SetValue(hero, (Hero)FormatterServices.GetUninitializedObject(typeof(Hero)));
            Hero_lastSeenInformationKnownToPlayer.SetValue(hero, heroData.LastMeetingTimeWithPlayer);
            Hero_SpcDaysInLocation.SetRandom(hero);
            Hero_health.SetRandom(hero);
            Hero_defaultAge.SetRandom(hero);
            Hero_birthDay.SetValue(hero, new CampaignTime());
            Hero_deathDay.SetValue(hero, new CampaignTime());
            Hero_power.SetRandom(hero);
            Hero_LastExaminedLogEntryID.SetRandom(hero);
            Hero_clan.SetValue(hero, heroData.Clan);
            Hero_supporterOf.SetValue(hero, heroData.Clan);
            Hero_governorOf.SetValue(hero, heroData.GoverningTown);
            Hero_ownedWorkshops.SetValue(hero, heroData.OwnedWorkshops);
            Hero_OwnedAlleys.SetValue(hero, heroData.OwnedAlleys);
            Hero_Culture.SetValue(hero, heroData.Culture);
            Hero_OwnedCaravans.SetValue(hero, heroData.OwnedCaravans);
            Hero_partyBelongedTo.SetValue(hero, heroData.HeroParty);
            Hero_PartyBelongedToAsPrisoner.SetValue(hero, heroData.PartyBelongedToAsPrisoner);
            Hero_stayingInSettlement.SetValue(hero, heroData.HomeSettlement);
            Hero_SpecialItems.SetValue(hero, heroData.SpecialItems);
            Hero_hasMet.SetRandom(hero);
            Hero_LastMeetingTimeWithPlayer.SetValue(hero, new CampaignTime());
            Hero_bornSettlement.SetValue(hero, heroData.HomeSettlement);
            Hero_homeSettlement.SetValue(hero, heroData.HomeSettlement);
            Hero_gold.SetRandom(hero);
            Hero_RandomValue.SetRandom(hero);
            Hero_father.SetValue(hero, heroData.Father);
            Hero_mother.SetValue(hero, heroData.Mother);
            Hero_exSpouses.SetValue(hero, heroData.ExSpouses);
            Hero_spouse.SetValue(hero, heroData.Spouse);
            Hero_children.SetValue(hero, heroData.Children);
            Hero_IsPregnant.SetRandom(hero);
            Hero_heroDeveloper.SetValue(hero, heroData.HeroDeveloper);
            #endregion

            return heroData;
        }

        /// <summary>
        /// Random Hero data used for verification comparison
        /// </summary>
        public class RandomHeroWithData
        {
            public Hero Hero { get; }
            public StaticBodyProperties StaticBodyProperties { get; private set; } = new StaticBodyProperties(1, 2, 3, 4, 5, 6, 7, 8);
            public MobileParty HeroParty { get; private set; }
            public CharacterObject CharacterObject { get; private set; }
            public Settlement HomeSettlement { get; private set; }
            public Settlement LastSeenSettlement { get; private set; }
            public CampaignTime LastMeetingTimeWithPlayer { get; private set; }
            public CharacterTraits CharacterTraits { get; private set; } = new CharacterTraits();
            public CharacterPerks CharacterPerks { get; private set; } = new CharacterPerks();
            public CharacterSkills CharacterSkills { get; private set; } = new CharacterSkills();
            public CharacterAttributes CharacterAttributes { get; private set; } = new CharacterAttributes();
            public CharacterObject[] VolunteerTypes { get; private set; }
            public Clan Clan { get; private set; }
            public Town GoverningTown { get; private set; }
            public List<Workshop> OwnedWorkshops { get; private set; }
            public List<Alley> OwnedAlleys { get; private set; }
            public CultureObject Culture { get; private set; }
            public List<CaravanPartyComponent> OwnedCaravans { get; private set; }
            public PartyBase PartyBelongedToAsPrisoner { get; private set; }
            public List<ItemObject> SpecialItems { get; private set; }
            public Hero Father { get; private set; }
            public Hero Mother { get; private set; }
            public List<Hero> ExSpouses { get; private set; }
            public Hero Spouse { get; private set; }
            public List<Hero> Children { get; private set; }
            public HeroDeveloper HeroDeveloper { get; private set; }

            public RandomHeroWithData()
            {
                Hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));

                CreateParty();
                CreateCharacterObject();
                CreateSettlement();
                CreateLastSeenInfo();
                CreateVolunteerTypes();
                CreateClanCultureTown();
                CreateOwnedParties();
                CreateItems();
                CreateParents();
                CreateExSpouses();
                CreateSpouse();
                CreateChildren();
                CreateHeroDeveloper();
            }

            #region CreationMethods
            static readonly PropertyInfo PartyBase_MobileParty = typeof(PartyBase).GetProperty(nameof(PartyBase.MobileParty));
            private void CreateParty()
            {
                HeroParty = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
                HeroParty.StringId = "My Party";
                HeroParty.SetCustomName(new TextObject("My Party"));
                MBObjectManager.Instance.RegisterObject(HeroParty);

                PartyBelongedToAsPrisoner = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));
                PartyBase_MobileParty.SetValue(PartyBelongedToAsPrisoner, HeroParty);
            }
            private void CreateCharacterObject()
            {
                CharacterObject = (CharacterObject)FormatterServices.GetUninitializedObject(typeof(CharacterObject));
                CharacterObject.StringId = "My CharacterObject";
                CharacterObject_basicName.SetValue(CharacterObject, new TextObject("My CharacterObject"));
                MBObjectManager.Instance.RegisterObject(CharacterObject);
            }

            private void CreateSettlement()
            {
                HomeSettlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
                HomeSettlement.StringId = "Home Settlement";
                MBObjectManager.Instance.RegisterObject(HomeSettlement);
            }

            private void CreateLastSeenInfo()
            {
                LastSeenSettlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
                LastSeenSettlement.StringId = "Last Seen Settlement";
                MBObjectManager.Instance.RegisterObject(LastSeenSettlement);

                LastMeetingTimeWithPlayer = new CampaignTime();
            }

            static readonly FieldInfo CharacterObject_basicName = typeof(CharacterObject).GetField("_basicName", BindingFlags.NonPublic | BindingFlags.Instance);
            private void CreateVolunteerTypes()
            {
                VolunteerTypes = new CharacterObject[]
                {
                    (CharacterObject) FormatterServices.GetUninitializedObject(typeof(CharacterObject)),
                    (CharacterObject) FormatterServices.GetUninitializedObject(typeof(CharacterObject)),
                    (CharacterObject) FormatterServices.GetUninitializedObject(typeof(CharacterObject)),
                };

                for (int i = 0; i < VolunteerTypes.Length; i++)
                {
                    CharacterObject character = VolunteerTypes[i];
                    character.StringId = $"Character_{i}";
                    CharacterObject_basicName.SetValue(character, new TextObject($"Character_{i}"));
                    MBObjectManager.Instance.RegisterObject(character);
                }
            }
            
            private void CreateClanCultureTown()
            {
                Clan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
                Clan.StringId = "My Clan";
                MBObjectManager.Instance.RegisterObject(Clan);

                GoverningTown = (Town)FormatterServices.GetUninitializedObject(typeof(Town));
                GoverningTown.StringId = "My Town";
                MBObjectManager.Instance.RegisterObject(GoverningTown);

                Culture = (CultureObject)FormatterServices.GetUninitializedObject(typeof(CultureObject));
                Culture.StringId = "My Culture";
                MBObjectManager.Instance.RegisterObject(Culture);
            }

            private static readonly FieldInfo Workshop_Settlement = typeof(Workshop).GetField("_settlement", BindingFlags.NonPublic | BindingFlags.Instance);
            private static readonly FieldInfo Workshop_tag = typeof(Workshop).GetField("_tag", BindingFlags.NonPublic | BindingFlags.Instance);
            private static readonly PropertyInfo Town_Workshops = typeof(Town).GetProperty(nameof(Town.Workshops));
            private void CreateOwnedParties()
            {
                Town town = (Town)FormatterServices.GetUninitializedObject(typeof(Town));

                // Setup town to be referencable by StringId
                town.StringId = "myTown";

                MBObjectManager.Instance.RegisterObject(town);

                // Set town of workshop settlement
                HomeSettlement.Town = town;

                OwnedWorkshops = new List<Workshop>
                {
                    (Workshop)FormatterServices.GetUninitializedObject(typeof(Workshop)),
                    (Workshop)FormatterServices.GetUninitializedObject(typeof(Workshop)),
                    (Workshop)FormatterServices.GetUninitializedObject(typeof(Workshop)),
                    (Workshop)FormatterServices.GetUninitializedObject(typeof(Workshop)),
                };

                Town_Workshops.SetValue(town, OwnedWorkshops.ToArray());

                // Workshop requires settlement for GetHashCode
                foreach (var workshop in OwnedWorkshops)
                {
                    Workshop_Settlement.SetValue(workshop, HomeSettlement);
                    Workshop_tag.SetValue(workshop, "My Tag");
                }

                OwnedAlleys = new List<Alley>
                {
                    (Alley)FormatterServices.GetUninitializedObject(typeof(Alley)),
                    (Alley)FormatterServices.GetUninitializedObject(typeof(Alley)),
                };

                OwnedCaravans = new List<CaravanPartyComponent>
                {
                    (CaravanPartyComponent)FormatterServices.GetUninitializedObject(typeof(CaravanPartyComponent)),
                    (CaravanPartyComponent)FormatterServices.GetUninitializedObject(typeof(CaravanPartyComponent)),
                };
            }

            private void CreateItems()
            {
                SpecialItems = new List<ItemObject>
                {
                    (ItemObject)FormatterServices.GetUninitializedObject(typeof(ItemObject)),
                    (ItemObject)FormatterServices.GetUninitializedObject(typeof(ItemObject)),
                };

                for(int i = 0; i < SpecialItems.Count; i++)
                {
                    ItemObject item = SpecialItems[i];
                    item.StringId = $"Item_{i}";
                    MBObjectManager.Instance.RegisterObject(item);
                }
            }

            private void CreateParents()
            {
                Father = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
                Father.StringId = "My Father";
                MBObjectManager.Instance.RegisterObject(Father);

                Mother = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
                Mother.StringId = "My Mother";
                MBObjectManager.Instance.RegisterObject(Mother);
            }

            private void CreateExSpouses()
            {
                var Ex1 = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
                Ex1.StringId = "My Ex1";
                MBObjectManager.Instance.RegisterObject(Ex1);

                var Ex2 = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
                Ex2.StringId = "My Ex2";
                MBObjectManager.Instance.RegisterObject(Ex2);

                ExSpouses = new List<Hero>
                {
                    Ex1,
                    Ex2,
                };
            }

            private void CreateSpouse()
            {
                var Wife = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
                Wife.StringId = "My Wife";
                MBObjectManager.Instance.RegisterObject(Wife);

                Spouse = Wife;
            }

            private void CreateChildren()
            {
                var Ch1 = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
                Ch1.StringId = "My Ch1";
                MBObjectManager.Instance.RegisterObject(Ch1);

                var Ch2 = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
                Ch2.StringId = "My Ch2";
                MBObjectManager.Instance.RegisterObject(Ch2);

                var Ch3 = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
                Ch3.StringId = "My Ch3";
                MBObjectManager.Instance.RegisterObject(Ch3);

                Children = new List<Hero>
                {
                    Ch1,
                    Ch2,
                    Ch3,
                };
            }

            private void CreateHeroDeveloper()
            {
                HeroDeveloper = (HeroDeveloper)FormatterServices.GetUninitializedObject(typeof(HeroDeveloper));
            }
            #endregion
        }
    }
}
