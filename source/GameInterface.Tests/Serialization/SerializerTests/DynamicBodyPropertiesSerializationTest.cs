﻿using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using TaleWorlds.Core;
using Xunit;
using Common.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class DynamicBodyPropertiesBinaryPackageSerializationTest
    {
        IContainer container;
        public DynamicBodyPropertiesBinaryPackageSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void DynamicBodyProperties_Serialize()
        {
            DynamicBodyProperties DynamicBodyProperties = new DynamicBodyProperties();

            var factory = container.Resolve<IBinaryPackageFactory>();
            DynamicBodyPropertiesBinaryPackage package = new DynamicBodyPropertiesBinaryPackage(DynamicBodyProperties, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void DynamicBodyProperties_Full_Serialization()
        {
            DynamicBodyProperties DynamicBodyProperties = new DynamicBodyProperties(37, 17, 43);

            var factory = container.Resolve<IBinaryPackageFactory>();
            DynamicBodyPropertiesBinaryPackage package = new DynamicBodyPropertiesBinaryPackage(DynamicBodyProperties, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<DynamicBodyPropertiesBinaryPackage>(obj);

            DynamicBodyPropertiesBinaryPackage returnedPackage = (DynamicBodyPropertiesBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            DynamicBodyProperties newStaticBodyProperties = returnedPackage.Unpack<DynamicBodyProperties>(deserializeFactory);

            Assert.Equal(DynamicBodyProperties.Age, newStaticBodyProperties.Age);
            Assert.Equal(DynamicBodyProperties.Weight, newStaticBodyProperties.Weight);
            Assert.Equal(DynamicBodyProperties.Build, newStaticBodyProperties.Build);
        }
    }
}
