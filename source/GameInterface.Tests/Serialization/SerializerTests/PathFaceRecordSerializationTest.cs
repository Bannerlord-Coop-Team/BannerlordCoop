using Autofac.Features.Indexed;
using Common.Extensions;
using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using Xunit;
using Xunit.Abstractions;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class PathFaceRecordSerializationTest
    {

        [Fact]
        public void PathFaceRecord_Serialize()
        {
            PathFaceRecord pfrObject = new PathFaceRecord();      

            BinaryPackageFactory factory = new BinaryPackageFactory();
            PathFaceRecordBinaryPackage package = new PathFaceRecordBinaryPackage(pfrObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void PathFaceRecord_Full_Serialization()
        {
            PathFaceRecord pfrObject = new PathFaceRecord(7,12,13);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            PathFaceRecordBinaryPackage package = new PathFaceRecordBinaryPackage(pfrObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);
            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<PathFaceRecordBinaryPackage>(obj);

            PathFaceRecordBinaryPackage returnedPackage = (PathFaceRecordBinaryPackage)obj;

            PathFaceRecord newPFRObject = returnedPackage.Unpack<PathFaceRecord>();

            Assert.Equal(pfrObject.FaceIndex, newPFRObject.FaceIndex);
            Assert.Equal(pfrObject.FaceGroupIndex, newPFRObject.FaceGroupIndex);
            Assert.Equal(pfrObject.FaceIslandIndex, newPFRObject.FaceIslandIndex);

        }
    }
}
