using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Serialization
{


    public class CommonSerializer
    {
        static CommonSerializer()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (typeof(ISerializer).IsAssignableFrom(type) && type.IsClass)
                        {
                            TryRegisterSerializer(Activator.CreateInstance(type) as ISerializer);
                        }
                    }
                }
                catch (System.Reflection.ReflectionTypeLoadException) { }
            }
        }

        static Enum[] idToProtocol = new Enum[0];
        static readonly Dictionary<Enum, int> protocolToId = new Dictionary<Enum, int>();
        static readonly Dictionary<Enum, ISerializer> serializers = new Dictionary<Enum, ISerializer>();
        

        public static int ProtocolToId(Enum protocol)
        {
            return protocolToId.ContainsKey(protocol) ? protocolToId[protocol] : -1;
        }

        public static Enum IdToProtocol(int id)
        {
            return idToProtocol[id];
        }

        public static byte[] Serialize(object obj)
        {
            return Serialize(obj, SerializationMethod.BinaryFormatter);
        }
        public static byte[] Serialize(object obj, Enum protocol)
        {
            if (!serializers.ContainsKey(protocol)) throw new InvalidOperationException($"Serializer for {protocol} was not registered.");

            try
            {
                return serializers[protocol].Serialize(obj);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            
        }

        private static bool TryRegisterSerializer(ISerializer serializer)
        {
            if (serializer == null) return false;
            if (serializers.ContainsKey(serializer.Protocol)) return false;

            serializers.Add(serializer.Protocol, serializer);

            protocolToId.Add(serializer.Protocol, idToProtocol.Length);
            idToProtocol = idToProtocol.Append(serializer.Protocol).ToArray();

            return true;
        }

        private static object DeserializeBytes(byte[] bytes, Enum protocol = null)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            };

            if (bytes.Length <= 0)
            {
                return null;
            }

            if (protocol == null) protocol = SerializationMethod.BinaryFormatter;

            if (!serializers.ContainsKey(protocol))
            {
                throw new InvalidOperationException($"{protocol} does not have a register serializer.");
            }

            try
            {
                return serializers[protocol].Deserialize(bytes);
            }
            catch (Exception ex)
            {
                throw ex;
            }        }

        public static T Deserialize<T>(ArraySegment<byte> bytes, Enum protocol = null)
        {
            return (T)Deserialize(bytes.Array, protocol);
        }

        public static T Deserialize<T>(byte[] bytes, Enum protocol = null)
        {
            return (T)DeserializeBytes(bytes, protocol);
        }

        public static object Deserialize(ArraySegment<byte> bytes, Enum protocol = null)
        {
            return Deserialize(bytes.Array, protocol);
        }

        public static object Deserialize(byte[] bytes, Enum protocol = null)
        {
            return DeserializeBytes(bytes, protocol);
        }
    }
}
