﻿using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using GameInterface.Tests.Bootstrap;
using TaleWorlds.Core;
using Xunit;
using Common.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class BodyPropertiesSerializationTest
    {
        IContainer container;
        public BodyPropertiesSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void BodyProperties_Serialize()
        {
            BodyProperties BodyProperties = new BodyProperties(new DynamicBodyProperties(1f, 2f, 3f), new StaticBodyProperties(1, 2, 3, 4, 5, 6, 7, 8));

            var factory = container.Resolve<IBinaryPackageFactory>();
            BodyPropertiesBinaryPackage package = new BodyPropertiesBinaryPackage(BodyProperties, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void BodyProperties_Full_Serialization()
        {
            BodyProperties BodyProperties = new BodyProperties(new DynamicBodyProperties(1f, 2f, 3f), new StaticBodyProperties(1, 2, 3, 4, 5, 6, 7, 8));

            var factory = container.Resolve<IBinaryPackageFactory>();
            BodyPropertiesBinaryPackage package = new BodyPropertiesBinaryPackage(BodyProperties, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<BodyPropertiesBinaryPackage>(obj);

            BodyPropertiesBinaryPackage returnedPackage = (BodyPropertiesBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            BodyProperties newBodyProperties = returnedPackage.Unpack<BodyProperties>(deserializeFactory);

            Assert.Equal(BodyProperties.Age, newBodyProperties.Age);
            Assert.Equal(BodyProperties.Build, newBodyProperties.Build);
            Assert.Equal(BodyProperties.Weight, newBodyProperties.Weight);
            Assert.Equal(BodyProperties.KeyPart1, newBodyProperties.KeyPart1);
            Assert.Equal(BodyProperties.KeyPart2, newBodyProperties.KeyPart2);
            Assert.Equal(BodyProperties.KeyPart3, newBodyProperties.KeyPart3);
            Assert.Equal(BodyProperties.KeyPart4, newBodyProperties.KeyPart4);
            Assert.Equal(BodyProperties.KeyPart5, newBodyProperties.KeyPart5);
            Assert.Equal(BodyProperties.KeyPart6, newBodyProperties.KeyPart6);
            Assert.Equal(BodyProperties.KeyPart7, newBodyProperties.KeyPart7);
            Assert.Equal(BodyProperties.KeyPart8, newBodyProperties.KeyPart8);
        }
    }
}
