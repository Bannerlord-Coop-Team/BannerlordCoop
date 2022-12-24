using Common.Extensions;
using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using GameInterface.Tests.Bootstrap;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.Launcher.Library;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class LordPartyComponentSerializationTest
    {
        public LordPartyComponentSerializationTest()
        {
            GameBootStrap.Initialize();
        }

        [Fact]
        public void LordPartyComponent_Serialize()
        {
            LordPartyComponent LordPartyComponent = (LordPartyComponent)FormatterServices.GetUninitializedObject(typeof(LordPartyComponent));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            LordPartyComponentBinaryPackage package = new LordPartyComponentBinaryPackage(LordPartyComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        private static readonly FieldInfo _leader = typeof(LordPartyComponent).GetField("_leader", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly PropertyInfo Owner = typeof(LordPartyComponent).GetProperty(nameof(LordPartyComponent.Owner));
        [Fact]
        public void LordPartyComponent_Full_Serialization()
        {
            Hero h1 = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));

            h1.StringId = "myHero";

            MBObjectManager.Instance.RegisterObject(h1);

            LordPartyComponent LordPartyComponent = (LordPartyComponent)FormatterServices.GetUninitializedObject(typeof(LordPartyComponent));

            _leader.SetValue(LordPartyComponent, h1);
            Owner.SetValue(LordPartyComponent, h1);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            LordPartyComponentBinaryPackage package = new LordPartyComponentBinaryPackage(LordPartyComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<LordPartyComponentBinaryPackage>(obj);

            LordPartyComponentBinaryPackage returnedPackage = (LordPartyComponentBinaryPackage)obj;

            LordPartyComponent newLordPartyComponent = returnedPackage.Unpack<LordPartyComponent>();

            Assert.Equal(_leader.GetValue(LordPartyComponent), _leader.GetValue(newLordPartyComponent));
            Assert.Equal(LordPartyComponent.Owner, newLordPartyComponent.Owner);
            Assert.Equal(LordPartyComponent.Party, newLordPartyComponent.Party);
        }
    }
}
