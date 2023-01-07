using GameInterface.Serialization;
using GameInterface.Serialization.External;
using System;
using Xunit;
using static TaleWorlds.Core.HorseComponent;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class MaterialPropertySerializationTest
    {
        [Fact]
        public void MaterialProperty_Serialize()
        {
            MaterialProperty MaterialProperty = new MaterialProperty("mat1");
            MaterialProperty.MeshMultiplier.Add(new Tuple<uint, float>(1, .5f));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            MaterialPropertyBinaryPackage package = new MaterialPropertyBinaryPackage(MaterialProperty, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void MaterialProperty_Full_Serialization()
        {
            MaterialProperty MaterialProperty = new MaterialProperty("mat1");
            MaterialProperty.MeshMultiplier.Add(new Tuple<uint, float>(1, .5f));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            MaterialPropertyBinaryPackage package = new MaterialPropertyBinaryPackage(MaterialProperty, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<MaterialPropertyBinaryPackage>(obj);

            MaterialPropertyBinaryPackage returnedPackage = (MaterialPropertyBinaryPackage)obj;

            MaterialProperty newMaterialProperty = returnedPackage.Unpack<MaterialProperty>();

            Assert.Equal(MaterialProperty.Name, newMaterialProperty.Name);
            Assert.Equal(MaterialProperty.MeshMultiplier, newMaterialProperty.MeshMultiplier);
            Assert.Equal(MaterialProperty.ToString(), newMaterialProperty.ToString());
        }
    }
}
