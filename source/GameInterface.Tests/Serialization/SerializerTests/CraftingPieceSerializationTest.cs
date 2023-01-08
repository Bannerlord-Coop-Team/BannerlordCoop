using GameInterface.Serialization;
using GameInterface.Serialization.External;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CraftingPieceSerializationTest
    {
        [Fact]
        public void CraftingPiece_Serialize()
        {
            CraftingPiece craftingPiece = new CraftingPiece();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CraftingPieceBinaryPackage package = new CraftingPieceBinaryPackage(craftingPiece, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void CraftingPiece_Full_Serialization()
        {
            CraftingPiece craftingPiece = new CraftingPiece();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            CraftingPieceBinaryPackage package = new CraftingPieceBinaryPackage(craftingPiece, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<CraftingPieceBinaryPackage>(obj);

            CraftingPieceBinaryPackage returnedPackage = (CraftingPieceBinaryPackage)obj;

            CraftingPiece newCraftingPiece = returnedPackage.Unpack<CraftingPiece>();

            Assert.Equal(craftingPiece.ArmorBonus, newCraftingPiece.ArmorBonus);
            Assert.Equal(craftingPiece.Length, newCraftingPiece.Length);
            Assert.Equal(craftingPiece.Name, newCraftingPiece.Name);
            Assert.Equal(craftingPiece.AccuracyBonus, newCraftingPiece.AccuracyBonus);
        }
    }
}
