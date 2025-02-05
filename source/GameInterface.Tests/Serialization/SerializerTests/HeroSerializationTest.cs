using Autofac;
using Common.Extensions;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Modules;
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
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using Xunit;
using Xunit.Abstractions;
using Common.Serialization;
using TaleWorlds.CampaignSystem.Actions;
using Common.Util;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class HeroSerializationTest
    {
        private readonly ITestOutputHelper output;

        IContainer container;
        public HeroSerializationTest(ITestOutputHelper output)
        {
            this.output = output;
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void Hero_Serialize()
        {
            var objectManager = container.Resolve<IObjectManager>();
            HeroFactory.RandomHeroWithData heroData = HeroFactory.CreateRandomHero(objectManager);
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

            var factory = container.Resolve<IBinaryPackageFactory>();
            HeroBinaryPackage package = new HeroBinaryPackage(hero, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        

        [Fact]
        public void Hero_Full_Serialization()
        {
            Assert.NotNull(CampaignTime.Zero.ElapsedYearsUntilNow);

            // Create hero with partially-random values
            var objectManager = container.Resolve<IObjectManager>();
            HeroFactory.RandomHeroWithData heroData = HeroFactory.CreateRandomHero(objectManager);
            Hero hero = heroData.Hero;
            

            // Setup serialization
            var factory = container.Resolve<IBinaryPackageFactory>();
            HeroBinaryPackage package = new HeroBinaryPackage(hero, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<HeroBinaryPackage>(obj);

            HeroBinaryPackage returnedPackage = (HeroBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            Hero newHero = returnedPackage.Unpack<Hero>(deserializeFactory);

            // Verify PropertyOwner types
            CharacterAttributes newAttribues = newHero._characterAttributes;
            AssertPropertyOwnerEqual(heroData.CharacterAttributes, newAttribues);
            CharacterPerks newPerks = newHero._heroPerks;
            AssertPropertyOwnerEqual(heroData.CharacterPerks, newPerks);
            CharacterSkills newSkills = newHero._heroSkills;
            AssertPropertyOwnerEqual(heroData.CharacterSkills, newSkills);
            CharacterTraits newTraits = newHero._heroTraits;
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
            Assert.Equal(heroData.HeroDeveloper.ToString(), newHero.HeroDeveloper?.ToString());
            Assert.Equal(heroData.StaticBodyProperties, newHero.BodyProperties.StaticProperties);
            Assert.Equal(heroData.PartyBelongedToAsPrisoner.ToString(), newHero.PartyBelongedToAsPrisoner?.ToString());
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
            Dictionary<T, int> values1 = owner1._attributes;
            Dictionary<T, int> values2 = owner2._attributes;

            Assert.Equal(values1.Count, values2.Count);

            AssertValuesSame(values1.Keys, values2.Keys);
            AssertValuesEqual(values1.Values, values2.Values);
        }

        [Fact]
        public void Hero_StringId_Serialization()
        {
            var objectManager = container.Resolve<IObjectManager>();
            HeroFactory.RandomHeroWithData heroData = HeroFactory.CreateRandomHero(objectManager);
            Hero hero = heroData.Hero;
            objectManager.AddExisting(hero.StringId, hero);

            var factory = container.Resolve<IBinaryPackageFactory>();
            HeroBinaryPackage package = new HeroBinaryPackage(hero, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<HeroBinaryPackage>(obj);

            HeroBinaryPackage returnedPackage = (HeroBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            Hero newHero = returnedPackage.Unpack<Hero>(deserializeFactory);

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
            hero.EncyclopediaText = new TextObject("My EEncyclopedia Text");
            hero.IsFemale = ReflectionExtensions.Random<bool>();
            hero.HairTags = ReflectionExtensions.Random<string>();
            hero.BeardTags = ReflectionExtensions.Random<string>();
            hero.TattooTags = ReflectionExtensions.Random<string>();
            hero._battleEquipment = (Equipment)FormatterServices.GetUninitializedObject(typeof(Equipment));
            hero._civilianEquipment = (Equipment)FormatterServices.GetUninitializedObject(typeof(Equipment));
            hero.CaptivityStartTime = new CampaignTime();
            hero.PreferredUpgradeFormation = ReflectionExtensions.Random<FormationClass>();
            hero._heroState = ReflectionExtensions.Random<Hero.CharacterStates>();
            hero._heroTraits = heroData.CharacterTraits;
            hero._heroPerks = heroData.CharacterPerks;
            hero._heroSkills = heroData.CharacterSkills;
            hero._characterAttributes = heroData.CharacterAttributes;
            hero.IsNobleForOldSaves = ReflectionExtensions.Random<bool>();
            hero.IsMinorFactionHero = ReflectionExtensions.Random<bool>();
            hero.Level = ReflectionExtensions.Random<int>();
            hero._companionOf = heroData.Clan;
            hero.Occupation = ReflectionExtensions.Random<Occupation>();
            hero.DeathMark = ReflectionExtensions.Random<KillCharacterAction.KillCharacterActionDetail>();
            hero.DeathMarkKillerHero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
            hero.SpcDaysInLocation = ReflectionExtensions.Random<int>();
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
            public CharacterTraits CharacterTraits { get; private set; } = new CharacterTraits();
            public CharacterPerks CharacterPerks { get; private set; } = new CharacterPerks();
            public CharacterSkills CharacterSkills { get; private set; } = new CharacterSkills();
            public CharacterAttributes CharacterAttributes { get; private set; } = new CharacterAttributes();
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
            private void CreateParty()
            {
                HeroParty = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
                HeroParty.StringId = "My Party";
                HeroParty.SetCustomName(new TextObject("My Party"));

                HeroParty.Party = ObjectHelper.SkipConstructor<PartyBase>();
                HeroParty.Party.MemberRoster = new TroopRoster();
                HeroParty.Party.PrisonRoster = new TroopRoster();

                objectManager.AddExisting(HeroParty.StringId, HeroParty);

                PartyBelongedToAsPrisoner = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));
                PartyBelongedToAsPrisoner.MobileParty = HeroParty;
            }
            private void CreateCharacterObject()
            {
                CharacterObject = (CharacterObject)FormatterServices.GetUninitializedObject(typeof(CharacterObject));
                CharacterObject.StringId = "My CharacterObject";
                CharacterObject._basicName = new TextObject("My CharacterObject");
                CharacterObject.UpgradeTargets = new CharacterObject[0];
                objectManager.AddExisting(CharacterObject.StringId, CharacterObject);
            }

            private void CreateSettlement()
            {
                HomeSettlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
                HomeSettlement.StringId = "Home Settlement";
                objectManager.AddExisting(HomeSettlement.StringId, HomeSettlement);
            }

            private void CreateLastSeenInfo()
            {
                LastKnownClosestSettlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
                LastKnownClosestSettlement.StringId = "Last Seen Settlement";
                objectManager.AddExisting(LastKnownClosestSettlement.StringId, LastKnownClosestSettlement);
            }

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
                    character._basicName = new TextObject($"Character_{i}");
                    objectManager.AddExisting(character.StringId, character);
                }
            }
            
            private void CreateClanCultureTown()
            {
                Clan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
                Clan.StringId = "My Clan";
                objectManager.AddExisting(Clan.StringId, Clan);

                GoverningTown = (Town)FormatterServices.GetUninitializedObject(typeof(Town));
                GoverningTown.StringId = "My Town";
                objectManager.AddExisting(GoverningTown.StringId, GoverningTown);

                Culture = (CultureObject)FormatterServices.GetUninitializedObject(typeof(CultureObject));
                Culture.StringId = "My Culture";
                objectManager.AddExisting(Culture.StringId, Culture);
            }

            private static readonly FieldInfo Workshop_Settlement = typeof(Workshop).GetField("_settlement", BindingFlags.NonPublic | BindingFlags.Instance);
            private static readonly FieldInfo Workshop_tag = typeof(Workshop).GetField("_tag", BindingFlags.NonPublic | BindingFlags.Instance);
            private void CreateOwnedParties()
            {
                Town town = (Town)FormatterServices.GetUninitializedObject(typeof(Town));

                // Setup town to be referencable by StringId
                town.StringId = "myTown";

                objectManager.AddExisting(town.StringId, town);

                // Set town of workshop settlement
                HomeSettlement.Town = town;

                OwnedWorkshops = new MBList<Workshop>
                {
                    (Workshop)FormatterServices.GetUninitializedObject(typeof(Workshop)),
                    (Workshop)FormatterServices.GetUninitializedObject(typeof(Workshop)),
                    (Workshop)FormatterServices.GetUninitializedObject(typeof(Workshop)),
                    (Workshop)FormatterServices.GetUninitializedObject(typeof(Workshop)),
                };

                town.Workshops = OwnedWorkshops.ToArray();

                // Workshop requires settlement for GetHashCode
                foreach (var workshop in OwnedWorkshops)
                {
                    Workshop_Settlement.SetValue(workshop, HomeSettlement);
                    Workshop_tag.SetValue(workshop, "My Tag");
                }

                Settlement settlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));

                settlement.StringId = "My Settlement";

                objectManager.AddExisting(settlement.StringId, settlement);

                OwnedAlleys = new MBList<Alley>
                {
                    (Alley)FormatterServices.GetUninitializedObject(typeof(Alley)),
                    (Alley)FormatterServices.GetUninitializedObject(typeof(Alley)),
                };

                settlement.Alleys = OwnedAlleys;

                // Workshop requires settlement for GetHashCode
                foreach (var alley in OwnedAlleys)
                {
                    alley._settlement = settlement;
                }

                OwnedCaravans = new List<CaravanPartyComponent>
                {
                    (CaravanPartyComponent)FormatterServices.GetUninitializedObject(typeof(CaravanPartyComponent)),
                    (CaravanPartyComponent)FormatterServices.GetUninitializedObject(typeof(CaravanPartyComponent)),
                };
            }

            private void CreateItems()
            {
                SpecialItems = new MBList<ItemObject>
                {
                    (ItemObject)FormatterServices.GetUninitializedObject(typeof(ItemObject)),
                    (ItemObject)FormatterServices.GetUninitializedObject(typeof(ItemObject)),
                };

                for(int i = 0; i < SpecialItems.Count; i++)
                {
                    ItemObject item = SpecialItems[i];
                    item.StringId = $"Item_{i}";
                    objectManager.AddExisting(item.StringId, item);
                }
            }

            private void CreateParents()
            {
                Father = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
                Father.StringId = "My Father";
                objectManager.AddExisting(Father.StringId, Father);

                Mother = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
                Mother.StringId = "My Mother";
                objectManager.AddExisting(Mother.StringId, Mother);
            }

            private void CreateExSpouses()
            {
                var Ex1 = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
                Ex1.StringId = "My Ex1";
                objectManager.AddExisting(Ex1.StringId, Ex1);

                var Ex2 = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
                Ex2.StringId = "My Ex2";
                objectManager.AddExisting(Ex2.StringId, Ex2);

                ExSpouses = new MBList<Hero>()
                {
                    Ex1,
                    Ex2,
                };
            }

            private void CreateSpouse()
            {
                var Wife = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
                Wife.StringId = "My Wife";
                objectManager.AddExisting(Wife.StringId, Wife);

                Spouse = Wife;
            }

            private void CreateChildren()
            {
                var Ch1 = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
                Ch1.StringId = "My Ch1";
                objectManager.AddExisting(Ch1.StringId, Ch1);

                var Ch2 = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
                Ch2.StringId = "My Ch2";
                objectManager.AddExisting(Ch2.StringId, Ch2);

                var Ch3 = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
                Ch3.StringId = "My Ch3";
                objectManager.AddExisting(Ch3.StringId, Ch3);

                Children = new MBList<Hero>
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
