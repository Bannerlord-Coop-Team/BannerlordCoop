﻿using GameInterface.Serialization;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Tests.Serialization
{
    internal class TestProtobufSerializer : ISerializer
    {
        private readonly RuntimeTypeModel _typeModel;

        public TestProtobufSerializer(RuntimeTypeModel typeModel)
        {
            _typeModel = typeModel;
        }

        /// <summary>
        ///     Serializes the message into a byte array for sending over the network.
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <returns>Byte array representing the message</returns>
        public byte[] Serialize(object message)
        {
            if (message == null) return new byte[0];

            var memoryStream = new MemoryStream();
            _typeModel.Serialize(memoryStream, message);

            return memoryStream.ToArray();
        }

        /// <summary>
        ///     Deserializes the message into the represented object.
        /// </summary>
        /// <typeparam name="T">Type of object the message should look like</typeparam>
        /// <returns>The object deserialized</returns>
        public T Deserialize<T>(byte[] message)
        {
            if (message.Length == 0) return default(T);

            var memoryStream = new MemoryStream(message);
            return _typeModel.Deserialize<T>(memoryStream);
        }
    }
}
