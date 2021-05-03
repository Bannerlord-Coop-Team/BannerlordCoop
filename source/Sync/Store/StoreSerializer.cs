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
            var serializable = Factory != null ? Factory.Wrap(obj) : obj;
            IFormatter formatter = new BinaryFormatter();
            var stream = new MemoryStream();
            formatter.Serialize(stream, serializable);
            return stream.ToArray();
        }

        public object Deserialize(byte[] raw)
        {
            var buffer = new MemoryStream(raw);
            return Factory == null
                ? new BinaryFormatter().Deserialize(buffer)
                : Factory.Unwrap(new BinaryFormatter().Deserialize(buffer));
        }
    }
}