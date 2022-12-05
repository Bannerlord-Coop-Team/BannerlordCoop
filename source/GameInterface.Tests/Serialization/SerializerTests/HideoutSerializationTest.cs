using GameInterface.Serialization.Impl;
using GameInterface.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class HideoutSerializationTest
    {
        private static readonly PropertyInfo Campaign_Current = typeof(Campaign).GetProperty("Current");
        private static readonly FieldInfo Campaign_hideouts = typeof(Campaign).GetField("_hideouts", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        [Fact]
        public void Hideout_Serialize()
        {
            Campaign_Current.SetValue(null, FormatterServices.GetUninitializedObject(typeof(Campaign)));
            List<Hideout> allhideouts = new List<Hideout>();
            Hideout item = new Hideout();
            allhideouts.Add(item);
            Campaign_hideouts.SetValue(Campaign.Current,allhideouts);
            BinaryPackageFactory factory = new BinaryPackageFactory();
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
            Campaign_Current.SetValue(null, FormatterServices.GetUninitializedObject(typeof(Campaign)));
            List<Hideout> allhideouts = new List<Hideout>();
            Hideout item = new Hideout();
            item.IsSpotted = true;
            Hideout_nextPossibleAttackTime.SetValue(item, new CampaignTime());
            Hideout_SceneName.SetValue(item, "something");
            allhideouts.Add(item);
            Campaign_hideouts.SetValue(Campaign.Current, allhideouts);
            BinaryPackageFactory factory = new BinaryPackageFactory();
            HideoutBinaryPackage package = new HideoutBinaryPackage(item, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<HideoutBinaryPackage>(obj);

            HideoutBinaryPackage returnedPackage = (HideoutBinaryPackage)obj;

            Hideout hideout = returnedPackage.Unpack<Hideout>();

            Assert.Equal(hideout, item);
            Assert.Equal(hideout.SceneName, item.SceneName);
            Assert.Equal(item.IsSpotted, hideout.IsSpotted);
            Assert.NotNull(Hideout_nextPossibleAttackTime.GetValue(hideout));
        }
    }
}
