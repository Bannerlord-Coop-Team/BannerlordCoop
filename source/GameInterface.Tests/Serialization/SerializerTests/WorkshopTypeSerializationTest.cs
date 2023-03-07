using GameInterface.Serialization.External;
using GameInterface.Serialization;
using Xunit;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class WorkshopTypeSerializationTest
    {
        public WorkshopTypeSerializationTest()
        {
            MBObjectManager.Init();
            MBObjectManager.Instance.RegisterType<WorkshopType>("WorkshopType", "WorkshopTypes", 4U, true, false);
        }

        [Fact]
        public void WorkshopType_Serialize()
        {
            WorkshopType testWorkshopType = (WorkshopType)FormatterServices.GetUninitializedObject(typeof(WorkshopType));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            WorkshopTypeBinaryPackage package = new WorkshopTypeBinaryPackage(testWorkshopType, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void WorkshopType_Full_Serialization()
        {
            WorkshopType workshopType = new WorkshopType();

            workshopType.StringId = "myWorkshop";

            MBObjectManager.Instance.RegisterObject(workshopType);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            WorkshopTypeBinaryPackage package = new WorkshopTypeBinaryPackage(workshopType, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<WorkshopTypeBinaryPackage>(obj);

            WorkshopTypeBinaryPackage returnedPackage = (WorkshopTypeBinaryPackage)obj;

            WorkshopType newWorkshopType = returnedPackage.Unpack<WorkshopType>();

            Assert.Same(workshopType, newWorkshopType);
        }
    }
}
