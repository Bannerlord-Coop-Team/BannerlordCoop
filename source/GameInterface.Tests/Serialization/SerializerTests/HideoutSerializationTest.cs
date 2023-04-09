using GameInterface.Serialization.External;
using GameInterface.Serialization;
using System.Collections.Generic;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using Autofac;
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

        private static readonly FieldInfo Campaign_hideouts = typeof(Campaign).GetField("_hideouts", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        [Fact]
        public void Hideout_Serialize()
        {
            MBList<Hideout> allhideouts = new MBList<Hideout>();
            Hideout item = new Hideout();
            allhideouts.Add(item);
            Campaign_hideouts.SetValue(Campaign.Current, allhideouts);
            var factory = container.Resolve<IBinaryPackageFactory>();
            HideoutBinaryPackage package = new HideoutBinaryPackage(item, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        private static readonly FieldInfo Hideout_nextPossibleAttackTime = typeof(Hideout).GetField("_nextPossibleAttackTime", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly PropertyInfo Hideout_SceneName = typeof(Hideout).GetProperty("SceneName", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        [Fact]
        public void Hideout_Full_Serialization()
        {
            Hideout hideout = new Hideout
            {
                IsSpotted = true
            };

            Hideout_nextPossibleAttackTime.SetValue(hideout, new CampaignTime());
            Hideout_SceneName.SetValue(hideout, "something");

            MBList<Hideout> allhideouts = (MBList<Hideout>)Campaign_hideouts.GetValue(Campaign.Current) ?? new MBList<Hideout>();
            
            allhideouts.Add(hideout);

            Campaign_hideouts.SetValue(Campaign.Current, allhideouts);

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
            Assert.Equal(Hideout_nextPossibleAttackTime.GetValue(hideout),
                         Hideout_nextPossibleAttackTime.GetValue(newHideout));
        }
    }
}
