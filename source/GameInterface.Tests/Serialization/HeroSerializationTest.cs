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
using GameInterface.Tests.Serialization.Helpers;

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

        private void EnsureAllMemberCoverage(Hero hero)
        {
            foreach (FieldInfo field in typeof(Hero).GetAllInstanceFields(HeroBinaryPackage.Excludes))
            {
                object? value = field.GetValue(hero);
                if (value == null)
                {
                    output.WriteLine($"{field.Name} was null.");
                }
                Assert.NotNull(value);
            }
        }

        [Fact]
        public void Hero_Serialize()
        {
            var objectManager = container.Resolve<IObjectManager>();
            HeroHelper.RandomHeroWithData heroData = HeroHelper.CreateRandomHero(objectManager);
            Hero hero = heroData.Hero;

            EnsureAllMemberCoverage(hero);

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
            HeroHelper.RandomHeroWithData heroData = HeroHelper.CreateRandomHero(objectManager);
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
            //CharacterAttributes newAttribues = newHero._characterAttributes;
            //AssertPropertyOwnerEqual(heroData.CharacterAttributes, newAttribues);
            //CharacterPerks newPerks = newHero._heroPerks;
            //AssertPropertyOwnerEqual(heroData.CharacterPerks, newPerks);
            //CharacterSkills newSkills = newHero._heroSkills;
            //AssertPropertyOwnerEqual(heroData.CharacterSkills, newSkills);
            //CharacterTraits newTraits = newHero._heroTraits;
            //AssertPropertyOwnerEqual(heroData.CharacterTraits, newTraits);

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
            Assert.Same(heroData.PartyBelongedToAsPrisoner, newHero.PartyBelongedToAsPrisoner);

            // Verify data types are equal
            Assert.Equal(heroData.HeroDeveloper.ToString(), newHero.HeroDeveloper?.ToString());
            Assert.Equal(heroData.StaticBodyProperties, newHero.BodyProperties.StaticProperties);
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
            HeroHelper.RandomHeroWithData heroData = HeroHelper.CreateRandomHero(objectManager);
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
}
