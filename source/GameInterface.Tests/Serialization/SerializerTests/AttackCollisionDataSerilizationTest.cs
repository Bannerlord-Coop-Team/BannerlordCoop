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
    public class AttackCollisionDataSerilizationTest
    {
        [Fact]
        public void TestPack()
        {
            AttackCollisionData acd = new AttackCollisionData();
            acd.SetCollisionBoneIndexForAreaDamage(100);
            
            BinaryPackageFactory factory = new BinaryPackageFactory();
            AttackCollisionDataBinaryPackage package = new AttackCollisionDataBinaryPackage(acd, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            var f = new BinaryPackageFactory();
            var bf = BinaryFormatterSerializer.Deserialize<AttackCollisionDataBinaryPackage>(bytes);
            bf.BinaryPackageFactory = f;

            AttackCollisionData b = bf.Unpack<AttackCollisionData>();
            Assert.Equal(b.CollisionBoneIndex, acd.CollisionBoneIndex);
        }
    }
}
