using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using System.Linq;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class BladeDataSerializationTest
    {
        [Fact]
        public void BladeData_Serialize()
        {
            BladeData BladeData = new BladeData(CraftingPiece.PieceTypes.Blade, 1.1f);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            BladeDataBinaryPackage package = new BladeDataBinaryPackage(BladeData, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void BladeData_Full_Serialization()
        {
            BladeData BladeData = new BladeData(CraftingPiece.PieceTypes.Blade, 1.1f);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            BladeDataBinaryPackage package = new BladeDataBinaryPackage(BladeData, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<BladeDataBinaryPackage>(obj);

            BladeDataBinaryPackage returnedPackage = (BladeDataBinaryPackage)obj;

            BladeData newBladeData = returnedPackage.Unpack<BladeData>();

            Assert.Equal(BladeData.PieceType, newBladeData.PieceType);
            Assert.Equal(BladeData.BladeLength, newBladeData.BladeLength);
            Assert.Equal(BladeData.ThrustDamageType, newBladeData.ThrustDamageType);
            Assert.Equal(BladeData.SwingDamageType, newBladeData.SwingDamageType);
        }
    }
}
