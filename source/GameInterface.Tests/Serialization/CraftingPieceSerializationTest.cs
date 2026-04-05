using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using GameInterface.Tests.Bootstrap;
using TaleWorlds.Core;
using Xunit;
using Common.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class CraftingPieceSerializationTest
    {
        IContainer container;
        public CraftingPieceSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void CraftingPiece_Serialize()
        {
            CraftingPiece craftingPiece = new CraftingPiece();

            var factory = container.Resolve<IBinaryPackageFactory>();
            CraftingPieceBinaryPackage package = new CraftingPieceBinaryPackage(craftingPiece, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void CraftingPiece_Full_Serialization()
        {
            CraftingPiece craftingPiece = new CraftingPiece();

            var factory = container.Resolve<IBinaryPackageFactory>();
            CraftingPieceBinaryPackage package = new CraftingPieceBinaryPackage(craftingPiece, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<CraftingPieceBinaryPackage>(obj);

            CraftingPieceBinaryPackage returnedPackage = (CraftingPieceBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            CraftingPiece newCraftingPiece = returnedPackage.Unpack<CraftingPiece>(deserializeFactory);

            Assert.Equal(craftingPiece.ArmorBonus, newCraftingPiece.ArmorBonus);
            Assert.Equal(craftingPiece.Length, newCraftingPiece.Length);
            Assert.Equal(craftingPiece.Name, newCraftingPiece.Name);
            Assert.Equal(craftingPiece.AccuracyBonus, newCraftingPiece.AccuracyBonus);
        }
    }
}
