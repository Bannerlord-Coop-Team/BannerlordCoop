using Autofac;
using Common.Extensions;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Bootstrap.Modules;
using GameInterface.Tests.Serialization.Helpers;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit;
using Xunit.Abstractions;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class MobilePartySerializationTest
    {
        IContainer container;
        ITestOutputHelper outputHelper;
        public MobilePartySerializationTest(ITestOutputHelper outputHelper)
        {
            this.outputHelper = outputHelper;

            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        private void EnsureAllMemberCoverage<T>(T obj, HashSet<string> excludes)
        {
            var nullValueCount = 0;
            foreach (FieldInfo field in typeof(T).GetAllInstanceFields(excludes))
            {
                object? value = field.GetValue(obj);
                if (value == null)
                {
                    outputHelper.WriteLine($"{field.Name} requires a value.");
                    nullValueCount++;
                }
            }


            Assert.Equal(0, nullValueCount);
        }

        [Fact]
        public void MobileParty_Serialize()
        {
            var objectManager = container.Resolve<IObjectManager>();
            HeroHelper.RandomHeroWithData heroData = HeroHelper.CreateRandomHero(objectManager);

            var mobileParty = heroData.HeroParty;

            EnsureAllMemberCoverage(mobileParty, MobilePartyBinaryPackage.Excludes);
            EnsureAllMemberCoverage(mobileParty.Party, PartyBaseBinaryPackage.Excludes);

            var factory = container.Resolve<IBinaryPackageFactory>();
            MobilePartyBinaryPackage package = new MobilePartyBinaryPackage(mobileParty, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void MobileParty_Full_Serialization()
        {
            MobileParty mobilePartyObject = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));

            // Assign non default values to mobile party
            //mobilePartyObject.SetCustomName(new TextObject("Custom Name"));
            mobilePartyObject.Aggressiveness = 56;
            mobilePartyObject.IsActive = true;

            mobilePartyObject.Party = new PartyBase(mobilePartyObject);

            Hero surgeon = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
            surgeon.StringId = "My Surgeon";
            MBObjectManager.Instance.RegisterObject(surgeon);

            mobilePartyObject.Surgeon = surgeon;

            // Setup serialization for mobilePartyObject
            var factory = container.Resolve<IBinaryPackageFactory>();
            MobilePartyBinaryPackage package = new MobilePartyBinaryPackage(mobilePartyObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<MobilePartyBinaryPackage>(obj);

            MobilePartyBinaryPackage returnedPackage = (MobilePartyBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            MobileParty newMobilePartyObject = returnedPackage.Unpack<MobileParty>(deserializeFactory);

            // Verify party values
            //Assert.True(mobilePartyObject.Name.Equals(newMobilePartyObject.Name));
            Assert.Equal(mobilePartyObject.Aggressiveness, newMobilePartyObject.Aggressiveness);
            Assert.Equal(mobilePartyObject.IsActive, newMobilePartyObject.IsActive);
            Assert.Same(mobilePartyObject.EffectiveSurgeon, newMobilePartyObject.EffectiveSurgeon);
        }
    }
}
