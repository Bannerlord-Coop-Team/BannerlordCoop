//using GameInterface.Serializers;
//using GameInterface.Serializers.CustomSerializers;
//using System.Linq;
//using TaleWorlds.Core;
//using Xunit;

//namespace GameInterface.Tests.Serialization.SerializerTests
//{
//    public class BannerSerializerTests
//    {
//        [Fact]
//        public void Banner_Serialize()
//        {
//            ReferenceRepository referenceRepository = new ReferenceRepository();
//            SerializableFactory serializableFactory = new SerializableFactory(referenceRepository);

//            Banner testBanner = new Banner();

//            BannerSerializer bannerSerializer = new BannerSerializer(serializableFactory, referenceRepository);

//            byte[] bytes = bannerSerializer.Serialize(testBanner);

//            Assert.NotEmpty(bytes);
//        }

//        [Fact]
//        public void Banner_Full_Serialization()
//        {
//            Banner testBanner = new Banner();

//            BannerBinaryPackage package = new BannerBinaryPackage();

//            package.Pack(testBanner);

//            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

//            Assert.NotEmpty(bytes);

//            object obj = BinaryFormatterSerializer.Deserialize(bytes);

//            Assert.IsType<BannerBinaryPackage>(obj);

//            BannerBinaryPackage returnedPackage = (BannerBinaryPackage)obj;

//            Banner newBanner = returnedPackage.Unpack();

//            foreach (var data in testBanner.BannerDataList.Zip(newBanner.BannerDataList, (a, b) => new { a, b }))
//            {
//                Assert.Equal(data.a, data.b);
//            }
//        }
//    }
//}
