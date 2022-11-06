//using GameInterface.Serializers.CustomSerializers;
//using ProtoBuf;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Reflection;
//using System.Runtime.Serialization.Formatters.Binary;
//using System.Text;
//using System.Threading.Tasks;
//using TaleWorlds.CampaignSystem;

//namespace GameInterface.Serializers
//{
//    public class SerializableFactory
//    {
//        readonly ReferenceRepository referenceRepository;

//        readonly Dictionary<Type, CustomSerializerBase> Serializers = new Dictionary<Type, CustomSerializerBase>();
        

//        private static readonly BinaryFormatter binaryFormatter = new BinaryFormatter();

//        public SerializableFactory(ReferenceRepository referenceRepository)
//        {
//            this.referenceRepository = referenceRepository;

//            CollectSerializers();
//        }

//        public void CollectSerializers()
//        {
//            foreach(var type in Assembly.GetExecutingAssembly().GetTypes())
//            {
//                if(type.IsAbstract == false &&
//                   typeof(CustomSerializerBase).IsAssignableFrom(type))
//                {
//                    CustomSerializerBase customSerializer = (CustomSerializerBase)Activator
//                        .CreateInstance(type, new object[] { this, referenceRepository });
//                    Serializers.Add(customSerializer.CustomType, customSerializer);
//                }
//            }
//        }

//        public CustomSerializerBase GetSerializer(object obj)
//        {
//            if (Serializers.TryGetValue(obj.GetType(), out CustomSerializerBase serializer))
//            {
//                return serializer;
//            }
//            throw new SerializationException($"Serializer for type {obj.GetType()} does not exist");
//        }

//        public byte[] Serialize(object obj)
//        {
//            ICustomSerializer serializer = GetSerializer(obj);
//            return serializer.Serialize(obj);
//        }

//        public object Deserialize(byte[] bytes)
//        {
//            using (MemoryStream ms = new MemoryStream(bytes))
//            {
//                ICustomSerializer serializer = (ICustomSerializer)binaryFormatter.Deserialize(ms);
//                object newObj = serializer.Deserialize(bytes);
//                serializer.ResolveReferences(newObj);
//                return newObj;
//            }
//        }
//    }

//    public class SerializationException : Exception
//    {
//        public SerializationException()
//        {
//        }

//        public SerializationException(string message) : base(message)
//        {
//        }

//        public SerializationException(string message, Exception innerException) : base(message, innerException)
//        {
//        }

//        protected SerializationException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
//        {
//        }
//    }
//}
