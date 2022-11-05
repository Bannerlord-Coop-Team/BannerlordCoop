using GameInterface.Serialization.Dynamic;
using HarmonyLib;
using ProtoBuf.Meta;
using System;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace GameInterface.Tests.Serialization.Dynamic
{
    public class BodyPropertiesObjectSerializationTests
    {
        private readonly ITestOutputHelper output;
        public BodyPropertiesObjectSerializationTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void NominalBodyPropertiesObjectSerialization()
        {
            var testModel = MakeBodyPropertiesSerializable();

            

            DynamicBodyProperties dynBodyProps = new DynamicBodyProperties(5, 5, 5);
            BodyProperties bodyProperties = new BodyProperties(dynBodyProps, default);

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);

            byte[] data = ser.Serialize(bodyProperties);

            BodyProperties newBodyProperties = ser.Deserialize<BodyProperties>(data);

            Assert.NotEqual(default, newBodyProperties);
        }

        [Fact]
        public void NullBodyPropertiesObjectSerialization()
        {
            var testModel = MakeBodyPropertiesSerializable();

            TestProtobufSerializer ser = new TestProtobufSerializer(testModel);
            byte[] data = ser.Serialize(null);

            BodyProperties newBodyProperties = ser.Deserialize<BodyProperties>(data);

            Assert.Equal(default, newBodyProperties);
        }

        private RuntimeTypeModel MakeBodyPropertiesSerializable()
        {
            RuntimeTypeModel testModel = RuntimeTypeModel.Create();

            IDynamicModelGenerator generator = new DynamicModelGenerator(testModel);

            generator.CreateDynamicSerializer<BodyProperties>();
            generator.CreateDynamicSerializer<DynamicBodyProperties>();
            generator.CreateDynamicSerializer<StaticBodyProperties>();

            generator.Compile();

            // Verify the type ItemObject can be serialized
            Assert.True(testModel.CanSerialize(typeof(BodyProperties)));

            return testModel;
        }
    }
}
