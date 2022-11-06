//using Common.Serialization;
//using GameInterface.Serializers;
//using System;
//using System.Reflection;
//using TaleWorlds.Core;

//namespace GameInterface.Serializers.CustomSerializers
//{
    
//    public class BannerSerializer : CustomSerializerBase<Banner>
//    {
        

//        public BannerSerializer(SerializableFactory serializableFactory, ReferenceRepository referenceRepository) : base(serializableFactory, referenceRepository)
//        {
//        }

//        public byte[] Serialize(Banner obj)
//        {
//            return BinaryFormatterSerializer.Serialize(this);
//        }

//        public Banner Deserialize()
//        {
//            var package = BinaryFormatterSerializer.Deserialize<BannerBinaryPackage>(bytes);

//            return new Banner(data);
//        }

//        public override void ResolveReferences(object obj)
//        {
//            // No references
//        }

//        public override ICustomSerializer<Banner> Pack(Banner obj)
//        {
//            throw new NotImplementedException();
//        }

//        public override Banner Unpack()
//        {
//            throw new NotImplementedException();
//        }
//    }

//    [Serializable]
//    public class BannerBinaryPackage : IBinaryPackage<Banner>
//    {
//        private string data;

//        public IBinaryPackage<Banner> Pack(Banner obj)
//        {
//            data = obj.Serialize();
//            return this;
//        }

//        public Banner Unpack()
//        {
//            return new Banner(data);
//        }
//    }

//    public interface IBinaryPackage<T>
//    {
//        IBinaryPackage<T> Pack(T obj);
//        T Unpack();
//    }
//}