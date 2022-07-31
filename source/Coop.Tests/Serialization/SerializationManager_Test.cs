using Common;
using Coop.Mod.Missions;
using Coop.Mod.Missions.Messages;
using Coop.Mod.Serializers;
using Coop.Mod.Serializers.Surrogates;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Serialization
{
    class DummyObj
    {
        static Random Random = new Random();
        public int RanVal = Random.Next();
    }

    [Serializable]
    class DummyObjSerializer : CustomSerializer
    {
        public DummyObjSerializer(DummyObj obj) : base(obj)
        {

        }

        public override object Deserialize()
        {
            DummyObj obj = new DummyObj();
            return base.Deserialize(obj);
        }

        public override void ResolveReferenceGuids()
        {
            // No instance Ids
        }
    }

    public class SerializationManager_Test
    {
        ISerializationManager serializationManager = new SerializationManager();

        [Fact]
        public void ValidSerialize_Test()
        {
            DummyObj dummyObj = new DummyObj();

            // Create object serializer
            DummyObjSerializer serializer = new DummyObjSerializer(dummyObj);

            // Pass serializer to serialization manager
            byte[] data = serializationManager.Serialize(serializer);
            Assert.True(data.Length > 0);

            // Deserialize manager will deserialize ICustomSerializer types automatically
            DummyObj deserializedObj = serializationManager.Deserialize<DummyObj>(data);
            Assert.NotNull(deserializedObj);

            // Verify references are different
            Assert.False(ReferenceEquals(dummyObj, deserializedObj));
            // Verify values are the same
            Assert.Equal(dummyObj.RanVal, deserializedObj.RanVal);
        }

        [Fact]
        public void ValidTrySerialize_Test()
        {

            DummyObj dummyObj = new DummyObj();

            // Create object serializer
            DummyObjSerializer serializer = new DummyObjSerializer(dummyObj);

            // Pass serializer to serialization manager
            Assert.True(serializationManager.TrySerialize(serializer, out byte[] data));
            Assert.True(data.Length > 0);

            // Deserialize manager will deserialize ICustomSerializer types automatically
            Assert.True(serializationManager.TryDeserialize(data, out DummyObj deserializedObj));
            Assert.NotNull(deserializedObj);

            // Verify references are different
            Assert.False(ReferenceEquals(dummyObj, deserializedObj));
            // Verify values are the same
            Assert.Equal(dummyObj.RanVal, deserializedObj.RanVal);
        }

        [Fact]
        public void InvalidSerialize_Test()
        {
            DummyObj dummyObj = new DummyObj();
            DummyObjSerializer serializer = new DummyObjSerializer(dummyObj);

            // Nonserializable object cannot be serialized
            Assert.Throws<SerializationException>(() => { serializationManager.Serialize(dummyObj); });

            // Get valid serialized data
            byte[] data = serializationManager.Serialize(serializer);
            Assert.True(data.Length > 0);

            // Generate bad data
            byte[] badData = new byte[5];
            Random random = new Random();
            random.NextBytes(badData);

            // Bad data throws serialization exception on deserialize
            Assert.Throws<SerializationException>(() => { serializationManager.Deserialize<DummyObj>(badData); });

            // Cast will fail if deserialized type does not match given type
            Assert.Throws<InvalidCastException>(() => { serializationManager.Deserialize<int>(data); });

            // Test serialization still works with good data
            DummyObj deserializedObj = serializationManager.Deserialize<DummyObj>(data);
            Assert.NotNull(deserializedObj);

            Assert.False(ReferenceEquals(dummyObj, deserializedObj));
            Assert.Equal(dummyObj.RanVal, deserializedObj.RanVal);
        }

        [Fact]
        public void InvalidTrySerialize_Test()
        {
            DummyObj dummyObj = new DummyObj();
            DummyObjSerializer serializer = new DummyObjSerializer(dummyObj);

            // Nonserializable object cannot be serialized
            Assert.False(serializationManager.TrySerialize(dummyObj, out byte[] invalidSerialize));
            Assert.Null(invalidSerialize);

            // Get valid serialized data
            byte[] data = serializationManager.Serialize(serializer);
            Assert.True(data.Length > 0);

            // Generate bad data
            byte[] badData = new byte[5];
            Random random = new Random();
            random.NextBytes(badData);

            // Bad data throws serialization exception on deserialize
            Assert.False(serializationManager.TryDeserialize(badData, out DummyObj badObj));
            Assert.Null(badObj);

            // Cast will fail if deserialized type does not match given type
            Assert.False(serializationManager.TryDeserialize(data, out int badCastObj));
            Assert.Equal(default, badCastObj);

            // Test serialization still works with good data
            DummyObj deserializedObj = serializationManager.Deserialize<DummyObj>(data);
            Assert.NotNull(deserializedObj);

            Assert.False(ReferenceEquals(dummyObj, deserializedObj));
            Assert.Equal(dummyObj.RanVal, deserializedObj.RanVal);
        }

        [ProtoContract]
        class DummyProtoClass<T>
        {
            [ProtoMember(1)]
            public int RanVal { get; set; }
            [ProtoMember(2)]
            public T RanObj { get; set; }
            [ProtoMember(3)]
            public Enum Enum { get; set; }
        }

        [Fact]
        public void ProtoSerialize_Test()
        {

            SurrogateCollector.CollectSurrogates();
            Vec2 vec = Vec2.One;

            byte[] data;
            using (var ms = new MemoryStream())
            {
                Serializer.Serialize(ms, vec);
                data = ms.ToArray();
            }

            Vec2 newVec;
            using (var ms = new MemoryStream(data))
            {
                newVec = Serializer.Deserialize<Vec2>(ms);
            }

            Assert.Equal(vec, newVec);


        }
    }
}
