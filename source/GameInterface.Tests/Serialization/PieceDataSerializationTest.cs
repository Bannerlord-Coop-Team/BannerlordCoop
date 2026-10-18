using Autofac;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class PieceDataSerializationTest
    {
        ILifetimeScope container;
        public PieceDataSerializationTest()
        {
            container = SerializationTestContainer.BeginScope();
        }

        [Fact]
        public void PieceData_Full_Serialization()
        {
            PieceData PieceData = new PieceData(CraftingPiece.PieceTypes.Guard, 2);

            var factory = container.Resolve<IBinaryPackageFactory>();
            PieceDataBinaryPackage package = new PieceDataBinaryPackage(PieceData, factory);

            package.Pack();

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryPackageSerializer.Deserialize(bytes);

            Assert.IsType<PieceDataBinaryPackage>(obj);

            PieceDataBinaryPackage returnedPackage = (PieceDataBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            PieceData newPieceData = returnedPackage.Unpack<PieceData>(deserializeFactory);

            Assert.Equal(PieceData.PieceType, newPieceData.PieceType);
            Assert.Equal(PieceData.Order, newPieceData.Order);
        }
    }
}
