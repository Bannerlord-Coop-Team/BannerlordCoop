using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using GameInterface.Tests.Bootstrap;
using System;
using Xunit;
using static TaleWorlds.Core.HorseComponent;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class MaterialPropertySerializationTest
    {
        IContainer container;
        public MaterialPropertySerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void MaterialProperty_Serialize()
        {
            MaterialProperty MaterialProperty = new MaterialProperty("mat1");
            MaterialProperty.MeshMultiplier.Add(new Tuple<uint, float>(1, .5f));

            var factory = container.Resolve<IBinaryPackageFactory>();
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

            var factory = container.Resolve<IBinaryPackageFactory>();
            MaterialPropertyBinaryPackage package = new MaterialPropertyBinaryPackage(MaterialProperty, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<MaterialPropertyBinaryPackage>(obj);

            MaterialPropertyBinaryPackage returnedPackage = (MaterialPropertyBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            MaterialProperty newMaterialProperty = returnedPackage.Unpack<MaterialProperty>(deserializeFactory);

            Assert.Equal(MaterialProperty.Name, newMaterialProperty.Name);
            Assert.Equal(MaterialProperty.MeshMultiplier, newMaterialProperty.MeshMultiplier);
            Assert.Equal(MaterialProperty.ToString(), newMaterialProperty.ToString());
        }
    }
}
