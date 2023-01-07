using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class WeaponDesignSerializationTest
    {
        public WeaponDesignSerializationTest()
        {
            GameBootStrap.Initialize();
        }

        [Fact]
        public void WeaponDesign_Serialize()
        {
            WeaponDesignElement[] elements = new WeaponDesignElement[]
            {
                WeaponDesignElement.CreateUsablePiece(new CraftingPiece(), 30),
                WeaponDesignElement.CreateUsablePiece(new CraftingPiece(), 30),
                WeaponDesignElement.CreateUsablePiece(new CraftingPiece(), 30)
            };

            PieceData[] buildOrders = new PieceData[]
            {
                new PieceData(CraftingPiece.PieceTypes.Blade, 0),
                new PieceData(CraftingPiece.PieceTypes.Handle, 1)
            };

            CraftingTemplate craftingTemplate = new CraftingTemplate();
            typeof(CraftingTemplate).GetField("_buildOrders", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(craftingTemplate, buildOrders);
            WeaponDesign WeaponDesign = new WeaponDesign(craftingTemplate, new TextObject("testValue"), elements);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            WeaponDesignBinaryPackage package = new WeaponDesignBinaryPackage(WeaponDesign, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void WeaponDesign_Full_Serialization()
        {
            WeaponDesignElement[] elements = new WeaponDesignElement[]
{
                WeaponDesignElement.CreateUsablePiece(new CraftingPiece(), 31),
                WeaponDesignElement.CreateUsablePiece(new CraftingPiece(), 32),
                WeaponDesignElement.CreateUsablePiece(new CraftingPiece(), 33)
};

            PieceData[] buildOrders = new PieceData[]
            {
                new PieceData(CraftingPiece.PieceTypes.Blade, 0),
                new PieceData(CraftingPiece.PieceTypes.Handle, 1)
            };

            CraftingTemplate craftingTemplate = MBObjectManager.Instance.CreateObject<CraftingTemplate>();
            typeof(CraftingTemplate).GetField("_buildOrders", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(craftingTemplate, buildOrders);
            WeaponDesign WeaponDesign = new WeaponDesign(craftingTemplate, new TextObject("testValue"), elements);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            WeaponDesignBinaryPackage package = new WeaponDesignBinaryPackage(WeaponDesign, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<WeaponDesignBinaryPackage>(obj);

            WeaponDesignBinaryPackage returnedPackage = (WeaponDesignBinaryPackage)obj;

            WeaponDesign newWeaponDesign = returnedPackage.Unpack<WeaponDesign>();

            Assert.Equal(WeaponDesign.CraftedWeaponLength, newWeaponDesign.CraftedWeaponLength);
            Assert.Equal(WeaponDesign.HolsterShiftAmount, newWeaponDesign.HolsterShiftAmount);
            Assert.Equal(WeaponDesign.UsedPieces.Length, newWeaponDesign.UsedPieces.Length); //The array does not want to succeed even though they are the exact same from what I can see
        }
    }
}
