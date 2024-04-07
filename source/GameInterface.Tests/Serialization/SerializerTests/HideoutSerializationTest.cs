using GameInterface.Serialization.External;
using GameInterface.Serialization;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using Autofac;
using Common.Serialization;
using GameInterface.Tests.Bootstrap.Modules;
using GameInterface.Tests.Bootstrap;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class HideoutSerializationTest
    {
        IContainer container;
        public HideoutSerializationTest()
        {
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void Hideout_Serialize()
        {
            MBList<Hideout> allhideouts = new MBList<Hideout>();
            Hideout item = new Hideout();
            allhideouts.Add(item);
            Campaign.Current._hideouts = allhideouts;
            var factory = container.Resolve<IBinaryPackageFactory>();
            HideoutBinaryPackage package = new HideoutBinaryPackage(item, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Hideout_Full_Serialization()
        {
            lock (Hideout.All)
            {
                Hideout hideout = new Hideout
                {
                    IsSpotted = true
                };

                hideout._nextPossibleAttackTime = new CampaignTime();
                hideout.SceneName = "something";

                MBList<Hideout> allhideouts = Campaign.Current?._hideouts as MBList<Hideout> ?? new MBList<Hideout>();

                allhideouts.Add(hideout);

                Campaign.Current._hideouts = allhideouts;

                var factory = container.Resolve<IBinaryPackageFactory>();
                HideoutBinaryPackage package = new HideoutBinaryPackage(hideout, factory);

                package.Pack();

                byte[] bytes = BinaryFormatterSerializer.Serialize(package);

                Assert.NotEmpty(bytes);

                object obj = BinaryFormatterSerializer.Deserialize(bytes);

                Assert.IsType<HideoutBinaryPackage>(obj);

                HideoutBinaryPackage returnedPackage = (HideoutBinaryPackage)obj;

                var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
                Hideout newHideout = returnedPackage.Unpack<Hideout>(deserializeFactory);

                Assert.Equal(hideout, newHideout);
                Assert.Equal(hideout.SceneName, newHideout.SceneName);
                Assert.Equal(hideout.IsSpotted, newHideout.IsSpotted);
                Assert.Equal(hideout._nextPossibleAttackTime,
                             newHideout._nextPossibleAttackTime);
            }
        }
    }
}
