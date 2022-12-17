using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using GameInterface.Tests.Bootstrap;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CommonAreaSerializationTest
    {
        public CommonAreaSerializationTest()
        {
            GameBootStrap.Initialize();
        }

        private static readonly FieldInfo _settlement = typeof(CommonArea).GetField("_settlement", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly PropertyInfo CommonAreas = typeof(Settlement).GetProperty(nameof(Settlement.CommonAreas));

        [Fact]
        public void CommonArea_Serialize()
        {
            CommonArea commonArea = (CommonArea)FormatterServices.GetUninitializedObject(typeof(CommonArea));
            Settlement settlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));

            // CommonArea packing requires settlement
            _settlement.SetValue(commonArea, settlement);

            // Packing requires settlement to have CommonAreas
            List<CommonArea> commonAreas = new List<CommonArea>
            {
                commonArea,
            };

            CommonAreas.SetValue(settlement, commonAreas);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CommonAreaBinaryPackage package = new CommonAreaBinaryPackage(commonArea, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        
        [Fact]
        public void CommonArea_Full_Serialization()
        {
            CommonArea commonArea = (CommonArea)FormatterServices.GetUninitializedObject(typeof(CommonArea));
            CommonArea commonArea2 = (CommonArea)FormatterServices.GetUninitializedObject(typeof(CommonArea));
            Settlement settlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));

            // CommonArea packing requires settlement
            _settlement.SetValue(commonArea, settlement);

            // Register settlement with MBObject Manager
            settlement.StringId = "My Settlement";

            MBObjectManager.Instance.RegisterObject(settlement);

            // Add CommonAreas to settlement
            
            List<CommonArea> commonAreas = new List<CommonArea>
            {
                commonArea,
                commonArea2,
            };

            CommonAreas.SetValue(settlement, commonAreas);

            // Setup serializers
            BinaryPackageFactory factory = new BinaryPackageFactory();
            CommonAreaBinaryPackage package = new CommonAreaBinaryPackage(commonArea, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<CommonAreaBinaryPackage>(obj);

            CommonAreaBinaryPackage returnedPackage = (CommonAreaBinaryPackage)obj;

            CommonArea newCommonArea = returnedPackage.Unpack<CommonArea>();

            Assert.Same(commonArea, newCommonArea);
        }
    }
}
