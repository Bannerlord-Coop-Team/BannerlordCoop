using GameInterface.Serialization;
using GameInterface.Serialization.External;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    /// <summary>
    /// Binary package for Blow Serialization
    /// </summary>
    public class BlowSerializationTest
    {
        [Fact]
        public void TestPack()
        {
            Blow blow = new Blow();

            blow.OwnerId = 20;
            BinaryPackageFactory factory = new BinaryPackageFactory();
            BlowBinaryPackage package = new BlowBinaryPackage(blow, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            var f = new BinaryPackageFactory();
            var bf = BinaryFormatterSerializer.Deserialize<BlowBinaryPackage>(bytes);
            bf.BinaryPackageFactory = f;

            Blow b  = bf.Unpack<Blow>();
            Assert.Equal(b.OwnerId, blow.OwnerId);
        }
    }
}
