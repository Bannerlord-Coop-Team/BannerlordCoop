﻿using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using TaleWorlds.Library;
using Xunit;
using Common.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class Mat3SerializationTest
    {
        IContainer container;
        public Mat3SerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void Mat3_Serialize()
        {
            Mat3 Mat3 = new Mat3(new Vec3(1,2,3), new Vec3(4,5,6), new Vec3(7,8,9));

            var factory = container.Resolve<IBinaryPackageFactory>();
            Mat3BinaryPackage package = new Mat3BinaryPackage(Mat3, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Mat3_Full_Serialization()
        {
            Mat3 Mat3 = new Mat3(new Vec3(11.001f, 2.001f, 3.001f), new Vec3(4.001f, 5.001f, 6.001f), new Vec3(7.001f, 8.001f, 0));

            var factory = container.Resolve<IBinaryPackageFactory>();
            Mat3BinaryPackage package = new Mat3BinaryPackage(Mat3, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<Mat3BinaryPackage>(obj);

            Mat3BinaryPackage returnedPackage = (Mat3BinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            Mat3 newMat3 = returnedPackage.Unpack<Mat3>(deserializeFactory);

            Assert.Equal(Mat3.f, newMat3.f);
            Assert.Equal(Mat3.s, newMat3.s);
            Assert.Equal(Mat3.u, newMat3.u);
        }
    }
}
