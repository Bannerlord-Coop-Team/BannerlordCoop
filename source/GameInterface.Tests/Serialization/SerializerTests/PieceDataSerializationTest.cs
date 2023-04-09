using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class PieceDataSerializationTest
    {
        IContainer container;
        public PieceDataSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void PieceData_Serialize()
        {
            PieceData PieceData = new PieceData(CraftingPiece.PieceTypes.Pommel, 1);

            var factory = container.Resolve<IBinaryPackageFactory>();
            PieceDataBinaryPackage package = new PieceDataBinaryPackage(PieceData, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void PieceData_Full_Serialization()
        {
            PieceData PieceData = new PieceData(CraftingPiece.PieceTypes.Guard, 2);

            var factory = container.Resolve<IBinaryPackageFactory>();
            PieceDataBinaryPackage package = new PieceDataBinaryPackage(PieceData, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<PieceDataBinaryPackage>(obj);

            PieceDataBinaryPackage returnedPackage = (PieceDataBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            PieceData newPieceData = returnedPackage.Unpack<PieceData>(deserializeFactory);

            Assert.Equal(PieceData.PieceType, newPieceData.PieceType);
            Assert.Equal(PieceData.Order, newPieceData.Order);
        }
    }
}
