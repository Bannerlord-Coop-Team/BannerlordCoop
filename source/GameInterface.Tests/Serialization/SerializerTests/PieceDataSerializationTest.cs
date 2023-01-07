using GameInterface.Serialization;
using GameInterface.Serialization.External;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class PieceDataSerializationTest
    {
        [Fact]
        public void PieceData_Serialize()
        {
            PieceData PieceData = new PieceData(CraftingPiece.PieceTypes.Pommel, 1);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            PieceDataBinaryPackage package = new PieceDataBinaryPackage(PieceData, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void PieceData_Full_Serialization()
        {
            PieceData PieceData = new PieceData(CraftingPiece.PieceTypes.Guard, 2);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            PieceDataBinaryPackage package = new PieceDataBinaryPackage(PieceData, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<PieceDataBinaryPackage>(obj);

            PieceDataBinaryPackage returnedPackage = (PieceDataBinaryPackage)obj;

            PieceData newPieceData = returnedPackage.Unpack<PieceData>();

            Assert.Equal(PieceData.PieceType, newPieceData.PieceType);
            Assert.Equal(PieceData.Order, newPieceData.Order);
        }
    }
}
