using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using GameInterface.Tests.Bootstrap;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CommonAreaPartyComponentSerializationTest
    {
        public CommonAreaPartyComponentSerializationTest()
        {
            GameBootStrap.Initialize();
        }

        [Fact]
        public void CommonAreaPartyComponent_Serialize()
        {
            CommonAreaPartyComponent item = (CommonAreaPartyComponent)FormatterServices.GetUninitializedObject(typeof(CommonAreaPartyComponent));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CommonAreaPartyComponentBinaryPackage package = new CommonAreaPartyComponentBinaryPackage(item, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        private static readonly FieldInfo _settlement = typeof(CommonArea).GetField("_settlement", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly PropertyInfo CommonAreas = typeof(Settlement).GetProperty(nameof(Settlement.CommonAreas));
        private static readonly PropertyInfo CommonAreaPartyComponent_Settlement = typeof(CommonAreaPartyComponent).GetProperty(nameof(CommonAreaPartyComponent.Settlement));
        private static readonly PropertyInfo CommonAreaPartyComponent_Owner = typeof(CommonAreaPartyComponent).GetProperty(nameof(CommonAreaPartyComponent.Owner));
        private static readonly PropertyInfo CommonAreaPartyComponent_CommonArea = typeof(CommonAreaPartyComponent).GetProperty(nameof(CommonAreaPartyComponent.CommonArea));

        [Fact]
        public void CommonAreaPartyComponent_Full_Serialization()
        {            
            CommonAreaPartyComponent commonAreaPartyComponent = (CommonAreaPartyComponent)FormatterServices.GetUninitializedObject(typeof(CommonAreaPartyComponent));
            CommonArea commonArea = (CommonArea)FormatterServices.GetUninitializedObject(typeof(CommonArea));
            Settlement settlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
            Hero owner = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));

            // Register owner with MBObjectManager
            owner.StringId = "My Hero";
            MBObjectManager.Instance.RegisterObject(owner);

            // Register settlement with MBObjectManager
            settlement.StringId = "My Settlement";
            MBObjectManager.Instance.RegisterObject(settlement);

            // Attach settlement and commonArea
            _settlement.SetValue(commonArea, settlement);

            List<CommonArea> commonAreas = new List<CommonArea>
            {
                commonArea,
            };

            CommonAreas.SetValue(settlement, commonAreas);

            // Assign common area with setup values
            CommonAreaPartyComponent_Settlement.SetValue(commonAreaPartyComponent, settlement);
            CommonAreaPartyComponent_CommonArea.SetValue(commonAreaPartyComponent, commonArea);
            CommonAreaPartyComponent_Owner.SetValue(commonAreaPartyComponent, owner);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CommonAreaPartyComponentBinaryPackage package = new CommonAreaPartyComponentBinaryPackage(commonAreaPartyComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<CommonAreaPartyComponentBinaryPackage>(obj);

            CommonAreaPartyComponentBinaryPackage returnedPackage = (CommonAreaPartyComponentBinaryPackage)obj;

            CommonAreaPartyComponent newCommonAreaPartyComponent = returnedPackage.Unpack<CommonAreaPartyComponent>();

            Assert.Same(commonAreaPartyComponent.CommonArea, newCommonAreaPartyComponent.CommonArea);
            Assert.Same(commonAreaPartyComponent.Settlement, newCommonAreaPartyComponent.Settlement);
            Assert.Same(commonAreaPartyComponent.Owner, newCommonAreaPartyComponent.Owner);
        }
    }
}
