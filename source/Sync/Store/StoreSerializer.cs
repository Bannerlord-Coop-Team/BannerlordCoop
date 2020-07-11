using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using JetBrains.Annotations;

namespace Sync.Store
{
    public class StoreSerializer
    {
        public StoreSerializer([CanBeNull] ISerializableFactory factory)
        {
            Factory = factory;
        }

        public ISerializableFactory Factory { get; }

        public byte[] Serialize(object obj)
        {
            object serializable = Factory != null ? Factory.Wrap(obj) : obj;
            IFormatter formatter = new BinaryFormatter();
            MemoryStream stream = new MemoryStream();
            formatter.Serialize(stream, serializable);
            return stream.ToArray();
        }

        public object Deserialize(byte[] raw)
        {
            MemoryStream buffer = new MemoryStream(raw);
            return Factory == null ?
                new BinaryFormatter().Deserialize(buffer) :
                Factory.Unwrap(new BinaryFormatter().Deserialize(buffer));
        }
    }
}
