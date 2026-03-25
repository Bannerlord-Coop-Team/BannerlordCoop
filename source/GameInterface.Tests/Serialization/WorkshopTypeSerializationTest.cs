using GameInterface.Serialization.External;
using GameInterface.Serialization;
using Xunit;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using Autofac;
using Common.Serialization;
using GameInterface.Tests.Bootstrap.Modules;
using GameInterface.Tests.Bootstrap;
using GameInterface.Services.ObjectManager;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class WorkshopTypeSerializationTest
    {
        IContainer container;
        public WorkshopTypeSerializationTest()
        {
            GameBootStrap.Initialize();

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void WorkshopType_Serialize()
        {
            WorkshopType testWorkshopType = (WorkshopType)FormatterServices.GetUninitializedObject(typeof(WorkshopType));

            var factory = container.Resolve<IBinaryPackageFactory>();
            WorkshopTypeBinaryPackage package = new WorkshopTypeBinaryPackage(testWorkshopType, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void WorkshopType_Full_Serialization()
        {
            var objectManager = container.Resolve<IObjectManager>();
            WorkshopType workshopType = new WorkshopType();

            workshopType.StringId = "myWorkshop";

            objectManager.AddExisting(workshopType.StringId, workshopType);

            var factory = container.Resolve<IBinaryPackageFactory>();
            WorkshopTypeBinaryPackage package = new WorkshopTypeBinaryPackage(workshopType, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<WorkshopTypeBinaryPackage>(obj);

            WorkshopTypeBinaryPackage returnedPackage = (WorkshopTypeBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            WorkshopType newWorkshopType = returnedPackage.Unpack<WorkshopType>(deserializeFactory);

            Assert.Same(workshopType, newWorkshopType);
        }
    }
}
