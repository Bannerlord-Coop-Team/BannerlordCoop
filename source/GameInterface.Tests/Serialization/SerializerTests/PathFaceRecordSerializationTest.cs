using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class PathFaceRecordSerializationTest
    {
        IContainer container;
        public PathFaceRecordSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void PathFaceRecord_Serialize()
        {
            PathFaceRecord pfrObject = new PathFaceRecord();      

            var factory = container.Resolve<IBinaryPackageFactory>();
            PathFaceRecordBinaryPackage package = new PathFaceRecordBinaryPackage(pfrObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void PathFaceRecord_Full_Serialization()
        {
            PathFaceRecord pfrObject = new PathFaceRecord(7,12,13);

            var factory = container.Resolve<IBinaryPackageFactory>();
            PathFaceRecordBinaryPackage package = new PathFaceRecordBinaryPackage(pfrObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);
            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<PathFaceRecordBinaryPackage>(obj);

            PathFaceRecordBinaryPackage returnedPackage = (PathFaceRecordBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            PathFaceRecord newPFRObject = returnedPackage.Unpack<PathFaceRecord>(deserializeFactory);

            Assert.Equal(pfrObject.FaceIndex, newPFRObject.FaceIndex);
            Assert.Equal(pfrObject.FaceGroupIndex, newPFRObject.FaceGroupIndex);
            Assert.Equal(pfrObject.FaceIslandIndex, newPFRObject.FaceIslandIndex);

        }
    }
}
