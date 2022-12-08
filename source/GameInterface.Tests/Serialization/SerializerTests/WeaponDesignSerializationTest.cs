using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class WeaponDesignSerializationTest
    {

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

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<WeaponDesignBinaryPackage>(obj);

            WeaponDesignBinaryPackage returnedPackage = (WeaponDesignBinaryPackage)obj;

            WeaponDesign newWeaponDesign = returnedPackage.Unpack<WeaponDesign>();

            Assert.Equal(WeaponDesign.CraftedWeaponLength, newWeaponDesign.CraftedWeaponLength);
            Assert.Equal(WeaponDesign.HolsterShiftAmount, newWeaponDesign.HolsterShiftAmount);
        }
    }
}
