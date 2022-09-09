using System.IO;
using GameInterface.Serialization;
using ProtoBuf;
using ProtoBuf.Meta;

namespace Coop.Serialization
{
    public class ProtobufSerializer : ISerializer
    {

        /// <summary>
        ///     Serializes the message into a byte array for sending over the network.
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <returns>Byte array representing the message</returns>
        public byte[] Serialize(object message)
        {
            if (message == null) return new byte[1] { byte.MinValue };

            using (var memoryStream = new MemoryStream())
            {
                // IsNull bit
                memoryStream.WriteByte(byte.MaxValue);

                Serializer.Serialize(memoryStream, message);

                return memoryStream.ToArray();
            }
        }
        
        /// <summary>
        ///     Deserializes the message into the represented object.
        /// </summary>
        /// <typeparam name="T">Type of object the message should look like</typeparam>
        /// <returns>The object deserialized</returns>
        public T Deserialize<T>(byte[] message)
        {
            using (var memoryStream = new MemoryStream(message))
            {
                var isNullByte = memoryStream.ReadByte();

                if (isNullByte == byte.MinValue) return default;

                return Serializer.Deserialize<T>(memoryStream);
            }
        }
    }
}