﻿using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.Generics;
using GameInterface.Tests.Bootstrap.Modules;
using System.Collections.Generic;
using TaleWorlds.Library;
using Xunit;
using Common.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class MBReadOnlyListSerializationTest
    {
        IContainer container;
        public MBReadOnlyListSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void MBReadOnlyList_Serialize()
        {
            List<int> ints = new List<int> { 1, 2, 3, 4, 5 };

            MBReadOnlyList<int> MBReadOnlyList = new MBReadOnlyList<int>(ints);

            var factory = container.Resolve<IBinaryPackageFactory>();
            MBReadOnlyListBinaryPackage package = new MBReadOnlyListBinaryPackage(MBReadOnlyList, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void MBReadOnlyList_Full_Serialization()
        {
            List<int> ints = new List<int> { 1, 2, 3, 4, 5 };

            MBReadOnlyList<int> MBReadOnlyList = new MBReadOnlyList<int>(ints);

            var factory = container.Resolve<IBinaryPackageFactory>();
            MBReadOnlyListBinaryPackage package = new MBReadOnlyListBinaryPackage(MBReadOnlyList, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<MBReadOnlyListBinaryPackage>(obj);

            MBReadOnlyListBinaryPackage returnedPackage = (MBReadOnlyListBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            MBReadOnlyList<int> newMBReadOnlyList = returnedPackage.Unpack<MBReadOnlyList<int>>(deserializeFactory);

            Assert.Equal(MBReadOnlyList, newMBReadOnlyList);
        }
    }
}
