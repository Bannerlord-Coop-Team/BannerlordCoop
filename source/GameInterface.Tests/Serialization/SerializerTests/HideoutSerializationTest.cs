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
        [Fact]
        public void Hideout_Serialize()
        {
            typeof(Campaign).GetProperty("Current").SetValue(null, FormatterServices.GetUninitializedObject(typeof(Campaign)));
            List<Hideout> allhideouts = new List<Hideout>();
            Hideout item = new Hideout();
            allhideouts.Add(item);
            typeof(Campaign).GetField("_hideouts", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(Campaign.Current,allhideouts);
            BinaryPackageFactory factory = new BinaryPackageFactory();
            HideoutBinaryPackage package = new HideoutBinaryPackage(item, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Hideout_Full_Serialization()
        {
            FieldInfo _nextPossibleAttackTime = typeof(Hideout).GetField("_nextPossibleAttackTime", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            typeof(Campaign).GetProperty("Current").SetValue(null, FormatterServices.GetUninitializedObject(typeof(Campaign)));
            List<Hideout> allhideouts = new List<Hideout>();
            Hideout item = new Hideout();
            item.IsSpotted = true;
            _nextPossibleAttackTime.SetValue(item, new CampaignTime());
            item.GetType().GetProperty("SceneName", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(item, "something");
            allhideouts.Add(item);
            typeof(Campaign).GetField("_hideouts", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(Campaign.Current, allhideouts);
            BinaryPackageFactory factory = new BinaryPackageFactory();
            HideoutBinaryPackage package = new HideoutBinaryPackage(item, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<HideoutBinaryPackage>(obj);

            HideoutBinaryPackage returnedPackage = (HideoutBinaryPackage)obj;

            Hideout hideout = returnedPackage.Unpack<Hideout>();

            Assert.True(hideout == item && hideout.SceneName == item.SceneName && item.IsSpotted == hideout.IsSpotted && _nextPossibleAttackTime.GetValue(hideout) != null);
        }
    }
}
