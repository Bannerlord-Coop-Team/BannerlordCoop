using GameInterface.Serialization.Impl;
using GameInterface.Serialization;
using TaleWorlds.Core;
using Xunit;
using System.Linq;
using TaleWorlds.CampaignSystem;
using System.Runtime.Serialization;
using Common.Extensions;
using System.Reflection;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ClanSerializationTest
    {
        [Fact]
        public void Clan_Serialize()
        {
            Clan testClan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            ClanBinaryPackage package = new ClanBinaryPackage(testClan, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Clan_Full_Serialization()
        {
            Clan testClan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            ClanBinaryPackage package = new ClanBinaryPackage(testClan, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<ClanBinaryPackage>(obj);

            ClanBinaryPackage returnedPackage = (ClanBinaryPackage)obj;

            Clan newClan = returnedPackage.Unpack<Clan>();

            foreach(FieldInfo field in typeof(Clan).GetAllInstanceFields())
            {
                Assert.Equal(field.GetValue(testClan), field.GetValue(newClan));
            }
        }
    }
}
